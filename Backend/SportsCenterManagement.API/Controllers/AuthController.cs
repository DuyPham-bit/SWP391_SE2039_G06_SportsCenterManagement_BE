using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller xử lý xác thực người dùng: Đăng ký, đăng nhập, đổi mật khẩu và quên mật khẩu.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly BearerTokenIssuer _tokenIssuer;
    private readonly SportsCenterDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public AuthController(
        IAuthService authService,
        BearerTokenIssuer tokenIssuer,
        SportsCenterDbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _authService = authService;
        _tokenIssuer = tokenIssuer;
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
    }

    public AuthController(
        SportsCenterDbContext dbContext,
        IConfiguration configuration,
        IHostEnvironment environment)
        : this(
            new AuthService(new UnitOfWork(dbContext)),
            new BearerTokenIssuer(new EphemeralDataProtectionProvider()),
            dbContext,
            configuration,
            environment)
    {
    }

    /// <summary>
    /// Đăng ký tài khoản hội viên mới vào hệ thống.
    /// </summary>
    /// <param name="request">Thông tin đăng ký hội viên (Username, Email, Mật khẩu, Họ tên,...)</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Hồ sơ cá nhân của hội viên vừa tạo</returns>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        BLL.DTOs.Auth.RegisterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _authService.RegisterAsync(request, cancellationToken: cancellationToken);
            return Created("/api/members/me", profile);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Username, email, or phone number is already in use." });
        }
    }

    /// <summary>
    /// Đăng nhập hệ thống bằng email và mật khẩu, nhận về JWT Access Token.
    /// </summary>
    /// <param name="request">Email và mật khẩu</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Chuỗi Token xác thực và thông tin cơ bản người dùng</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _authService.LoginAsync(
                new BLL.DTOs.Auth.LoginRequest(request.Email, request.Password),
                cancellationToken);
            var (token, expiresAt) = _tokenIssuer.Issue(user);
            return Ok(new AuthResponse(token, user.UserId, user.Role.ToUpperInvariant(), expiresAt));
        }
        catch (InvalidOperationException)
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ hoặc tài khoản đang bị khóa." });
        }
    }

    /// <summary>
    /// Đổi mật khẩu cho người dùng hiện tại đang đăng nhập.
    /// </summary>
    /// <param name="request">Mật khẩu cũ và mật khẩu mới</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Token xác thực mới cùng thời hạn mới</returns>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "JWT không chứa UserID hợp lệ." });
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Tài khoản không tồn tại." });
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { message = "Mật khẩu hiện tại không đúng." });
        }

        var passwordError = ValidateNewPassword(request.NewPassword, user);
        if (passwordError is not null)
        {
            return BadRequest(new { message = passwordError });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var role = await _dbContext.Roles
            .Where(r => r.Id == user.RoleId)
            .Select(r => r.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "Member";

        var authUser = new BLL.DTOs.Auth.AuthenticatedUser(user.Id, user.Username, role, null);
        var (token, expiresAt) = _tokenIssuer.Issue(authUser);

        return Ok(new AuthResponse(token, user.Id, role.ToUpperInvariant(), expiresAt));
    }

    /// <summary>
    /// Yêu cầu gửi mã OTP để đặt lại mật khẩu khi bị quên.
    /// </summary>
    /// <param name="request">Địa chỉ email đã đăng ký tài khoản</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Thông báo xác nhận gửi mã OTP (và mã OTP trong môi trường Development)</returns>
    [HttpPost("request-password-reset")]
    [ProducesResponseType(typeof(PasswordResetRequestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return Ok(new PasswordResetRequestResponse(
                "Nếu email tồn tại, mã xác thực đã được gửi.",
                null));
        }

        var activeTokens = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await _dbContext.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new PasswordResetRequestResponse(
            "Nếu email tồn tại, mã xác thực đã được gửi.",
            _environment.IsDevelopment() ? otp : null));
    }

    /// <summary>
    /// Xác thực mã OTP và tiến hành thiết lập mật khẩu mới.
    /// </summary>
    /// <param name="request">Email, mã OTP và mật khẩu mới</param>
    /// <param name="cancellationToken">Token hủy request</param>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var tokens = await _dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null && token.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(token => token.CreatedAt)
            .ToListAsync(cancellationToken);
        var token = tokens.FirstOrDefault(item => BCrypt.Net.BCrypt.Verify(request.Otp, item.Token));
        if (token is null)
        {
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var passwordError = ValidateNewPassword(request.NewPassword, user);
        if (passwordError is not null)
        {
            return BadRequest(new { message = passwordError });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private bool TryGetUserId(out long userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out userId);
    }

    private static string? ValidateNewPassword(string password, User user)
    {
        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return "Mật khẩu mới phải khác mật khẩu hiện tại.";
        }

        if (password.Any(char.IsWhiteSpace))
        {
            return "Mật khẩu mới không được chứa khoảng trắng.";
        }

        if (!password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit)
            || password.All(char.IsLetterOrDigit))
        {
            return "Mật khẩu mới phải gồm chữ hoa, chữ thường, số và ký tự đặc biệt.";
        }

        if (password.Contains(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            return "Mật khẩu mới không được chứa email đăng nhập.";
        }

        if (!string.IsNullOrWhiteSpace(user.Phone) && password.Contains(user.Phone, StringComparison.Ordinal))
        {
            return "Mật khẩu mới không được chứa số điện thoại.";
        }

        return null;
    }
}
