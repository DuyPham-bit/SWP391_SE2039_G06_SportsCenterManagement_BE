using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.DTOs.Auth;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

/// <summary>Authentication and account security endpoints.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    SportsCenterDbContext dbContext,
    IConfiguration configuration,
    IHostEnvironment environment,
    IAuthService? authService = null,
    BearerTokenIssuer? tokenIssuer = null) : ControllerBase
{
    private readonly BearerTokenIssuer? _tokenIssuer = tokenIssuer;

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] SportsCenterManagement.BLL.DTOs.Auth.Requests.RegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (authService is null)
        {
            return StatusCode(StatusCodes.Status501NotImplemented);
        }

        try
        {
            var profile = await authService.RegisterAsync(request, cancellationToken: cancellationToken);
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

    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var loginIdentifier = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(loginIdentifier))
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ." });
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == loginIdentifier || item.Username == loginIdentifier, cancellationToken);

        if (user is null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ." });
        }

        if (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Tài khoản đang bị khóa tạm thời." });
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ." });
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await CreateAuthResponseAsync(user, cancellationToken));
    }

    [HttpPost("request-password-reset")]
    [ProducesResponseType(typeof(PasswordResetRequestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return Ok(new PasswordResetRequestResponse(
                "Nếu email tồn tại, mã xác thực đã được gửi.",
                null));
        }

        var activeTokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        await dbContext.PasswordResetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new PasswordResetRequestResponse(
            "Nếu email tồn tại, mã xác thực đã được gửi.",
            environment.IsDevelopment() ? otp : null));
    }

    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == request.Email.Trim(), cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var tokens = await dbContext.PasswordResetTokens
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
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AuthResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(new { message = "JWT không chứa UserID hợp lệ." });
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return Unauthorized(new { message = "Tài khoản không tồn tại." });
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!VerifyPassword(request.CurrentPassword, user.PasswordHash))
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
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await CreateAuthResponseAsync(user, cancellationToken));
    }

    private bool TryGetUserId(out long userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(value, out userId);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles
            .Where(item => item.Id == user.RoleId)
            .Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "USER";
        role = role.ToUpperInvariant();

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(configuration.GetValue("Jwt:ExpiresMinutes", 60));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetJwtKey()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AuthResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            user.Id,
            role,
            expiresAt);
    }

    private string GetJwtKey() => configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is not configured.");

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (storedHash.StartsWith("$2", StringComparison.Ordinal))
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        if (storedHash.StartsWith("pbkdf2", StringComparison.Ordinal))
        {
            return PasswordHashing.Verify(password, storedHash);
        }
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, storedHash);
        }
        catch
        {
            return false;
        }
    }

    private static string? ValidateNewPassword(string password, User user)
    {
        if (VerifyPassword(password, user.PasswordHash))
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
