using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    BearerTokenIssuer tokenIssuer,
    IHostEnvironment environment,
    IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(
        [FromBody] SportsCenterManagement.BLL.DTOs.Auth.Requests.RegisterRequest request,
        CancellationToken cancellationToken)
    {
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
        var loginIdentifier = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(loginIdentifier))
        {
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ." });
        }

        SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser authenticated;
        try
        {
            authenticated = await authService.LoginAsync(
                new SportsCenterManagement.BLL.DTOs.Auth.Requests.LoginRequest(loginIdentifier, request.Password),
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Keep unknown, disabled and locked accounts indistinguishable to callers.
            return Unauthorized(new { message = "Thông tin đăng nhập không hợp lệ." });
        }

        return Ok(CreateAuthResponse(authenticated));
    }

    [HttpPost("request-password-reset")]
    [ProducesResponseType(typeof(PasswordResetRequestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
        if (user is null)
        {
            await transaction.CommitAsync(cancellationToken);
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
        await transaction.CommitAsync(cancellationToken);

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
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail, cancellationToken);
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
        await transaction.CommitAsync(cancellationToken);
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

        var now = DateTime.UtcNow;
        var updated = await dbContext.Users
            .Where(item => item.Id == user.Id && item.PasswordHash == user.PasswordHash && item.Status == "Active")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PasswordHash, BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12))
                .SetProperty(item => item.FailedLoginAttempts, 0)
                .SetProperty(item => item.LockedUntil, (DateTime?)null)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            return Unauthorized(new { message = "Mật khẩu hiện tại không còn hợp lệ." });
        }

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
        var centerId = await dbContext.StaffProfiles
            .Where(profile => profile.UserId == user.Id && profile.Status == "Active")
            .Select(profile => (long?)profile.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

        return CreateAuthResponse(new SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser(
            user.Id, user.Username, role, centerId, user.Email));
    }

    private AuthResponse CreateAuthResponse(
        SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser user)
    {
        var role = user.Role.ToUpperInvariant();
        var (token, expiresAt) = tokenIssuer.Issue(user);

        return new AuthResponse(
            token,
            user.UserId,
            role,
            new DateTimeOffset(expiresAt));
    }

    private static bool VerifyPassword(string password, string storedHash) => PasswordHashing.Verify(password, storedHash);

    private static string? ValidateNewPassword(string password, User user)
    {
        if (VerifyPassword(password, user.PasswordHash))
        {
            return "Mật khẩu mới phải khác mật khẩu hiện tại.";
        }

        return PasswordPolicy.GetValidationError(password, user.Email, user.Phone);
    }
}
