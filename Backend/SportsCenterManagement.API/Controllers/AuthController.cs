using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
    JwtTokenIssuer tokenIssuer,
    IAuthService authService,
    PasswordResetEmailSender resetEmail,
    IMemberActivationService activationService,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>Validates an activation link and returns the greeting details for the verify page.</summary>
    [HttpPost("verify-email/preview")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SportsCenterManagement.BLL.DTOs.Members.Activation.ActivationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewActivation(
        [FromBody] SportsCenterManagement.BLL.DTOs.Members.Activation.ActivationPreviewRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            return Ok(await activationService.PreviewAsync(request.Token, cancellationToken));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { code = "ACTIVATION_LINK_INVALID", message = exception.Message });
        }
    }

    /// <summary>Verifies the member's email and replaces the default (phone) password in one step.</summary>
    [HttpPost("verify-and-change-password")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyAndChangePassword(
        [FromBody] SportsCenterManagement.BLL.DTOs.Members.Activation.VerifyAndChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            await activationService.CompleteAsync(request, cancellationToken);
            return NoContent();
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    /// <summary>First-login fallback for accounts still using the default password.</summary>
    [Authorize]
    [HttpPost("initial-password")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeInitialPassword(
        [FromBody] SportsCenterManagement.BLL.DTOs.Members.Activation.InitialPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "JWT không chứa UserID hợp lệ." });
        try
        {
            var user = await activationService.ChangeInitialPasswordAsync(userId, request, cancellationToken);
            // AuthVersion was bumped, so the old session is revoked: issue a fresh one.
            return Ok(await CreateAuthResponseAsync(user, cancellationToken));
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpPost("register")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
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
            return Conflict(new { message = "Email hoặc số điện thoại đã được sử dụng cho một tài khoản khác." });
        }
    }

    [HttpPost("request-email-verification")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
    public async Task<IActionResult> RequestEmailVerification(
        [FromBody] RequestRegistrationEmailVerificationRequest request, CancellationToken cancellationToken)
    {
        if (!resetEmail.IsConfigured)
            return StatusCode(503, new { message = "Hiện chưa thể gửi email xác thực. Vui lòng thử lại sau hoặc liên hệ trung tâm." });

        var email = request.Email.Trim().ToLowerInvariant();
        var phone = PhoneNumberNormalization.Normalize(request.Phone);
        if (phone is not null && !System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[0-9]{8,15}$"))
            return BadRequest(new { message = "Số điện thoại phải gồm từ 8 đến 15 chữ số." });

        // Product decision: report duplicates before sending an OTP (rate-limited by "password-reset").
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            return Conflict(new { field = "email", message = "Email đã được sử dụng cho một tài khoản khác." });
        if (phone is not null && await dbContext.Users.AnyAsync(user => user.Phone == phone, cancellationToken))
            return Conflict(new { field = "phone", message = "Số điện thoại đã được sử dụng cho một tài khoản khác." });

        var now = DateTime.UtcNow;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var latest = await dbContext.EmailVerificationTokens
            .Where(token => token.Email == email && token.UsedAt == null)
            .OrderByDescending(token => token.Id).FirstOrDefaultAsync(cancellationToken);
        if (latest is not null && latest.CreatedAt > now.AddMinutes(-1))
        {
            Response.Headers.RetryAfter = "60";
            return StatusCode(429, new { message = "Vui lòng chờ 60 giây trước khi yêu cầu mã mới." });
        }
        await dbContext.EmailVerificationTokens.Where(token => token.Email == email && token.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.UsedAt, now), cancellationToken);
        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var verification = new EmailVerificationToken
        {
            Email = email, Token = BCrypt.Net.BCrypt.HashPassword(otp),
            CreatedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        dbContext.EmailVerificationTokens.Add(verification);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (!await resetEmail.SendVerificationAsync(verification, otp, cancellationToken, RequestLocation.Describe(Request, configuration)))
            return StatusCode(503, new { message = "Chưa gửi được email xác thực. Vui lòng thử lại sau." });
        return Ok(new { message = "Vui lòng kiểm tra hộp thư và thư rác để tiếp tục đăng ký. Mã xác thực có hạn 5 phút." });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(423)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var loginIdentifier = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(loginIdentifier))
        {
            return Unauthorized(new { message = "Email/số điện thoại hoặc mật khẩu không chính xác." });
        }

        SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser authenticated;
        try
        {
            authenticated = await authService.LoginAsync(
                new SportsCenterManagement.BLL.DTOs.Auth.Requests.LoginRequest(loginIdentifier, request.Password),
                cancellationToken);
        }
        catch (InvalidOperationException exception) when (exception.Data["StatusCode"] is 423)
        {
            return StatusCode(423, new
            {
                code = exception.Data["Code"] as string,
                message = exception.Message
            });
        }
        catch (InvalidOperationException)
        {
            // Keep unknown accounts and invalid credentials indistinguishable to callers.
            return Unauthorized(new { message = "Email/số điện thoại hoặc mật khẩu không chính xác." });
        }

        return Ok(CreateAuthResponse(authenticated));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        BrowserSessionCookie.Clear(HttpContext);
        return NoContent();
    }

    [HttpPost("request-password-reset")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasswordResetRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<PasswordResetRequestResponse>> RequestPasswordReset(
        [FromBody] RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        if (!resetEmail.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Hiện chưa thể gửi mã đặt lại mật khẩu. Vui lòng liên hệ trung tâm để được hỗ trợ."
            });

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.Status == "Active", cancellationToken);
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
        var reset = new PasswordResetToken
        {
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow
        };
        await dbContext.PasswordResetTokens.AddAsync(reset, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (resetEmail.IsConfigured && !await resetEmail.SendAsync(user, reset, otp, cancellationToken, location: RequestLocation.Describe(Request, configuration)))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Chưa gửi được mã xác thực. Vui lòng thử lại sau." });

        return Ok(new PasswordResetRequestResponse(
            "Nếu email tồn tại, mã xác thực đã được gửi.",
            null));
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("password-reset")]
    [AllowAnonymous]
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
            .SingleOrDefaultAsync(item => item.Email == normalizedEmail && item.Status == "Active", cancellationToken);
        if (user is null)
        {
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var token = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null && token.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(token => token.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null || token.FailedAttempts >= 5)
        {
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Otp, token.Token))
        {
            token.FailedAttempts += 1;
            if (token.FailedAttempts >= 5) token.UsedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return BadRequest(new { message = "Mã xác thực không hợp lệ hoặc đã hết hạn." });
        }

        var passwordError = ValidateNewPassword(request.NewPassword, user);
        if (passwordError is not null)
        {
            return BadRequest(new { message = passwordError });
        }

        user.PasswordHash = PasswordHashing.Hash(request.NewPassword);
        user.AuthVersion += 1;
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.UpdatedAt = DateTime.UtcNow;
        token.UsedAt = DateTime.UtcNow;
        await dbContext.PasswordResetTokens
            .Where(item => item.UserId == user.Id && item.Id != token.Id && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, token.UsedAt), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpPost("request-change-password-otp")]
    [EnableRateLimiting("password-reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RequestChangePasswordOtp(
        [FromBody] RequestChangePasswordOtpRequest request,
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

        // 1. KIỂM TRA MẬT KHẨU HIỆN TẠI TRƯỚC TIÊN - NẾU SAI KHÔNG GỬI MAIL
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || !VerifyPassword(request.CurrentPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        }

        // 2. KIỂM TRA MẬT KHẨU MỚI (NẾU CÓ TRUYỀN LÊN)
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var passwordError = ValidateNewPassword(request.NewPassword, user);
            if (passwordError is not null)
            {
                return BadRequest(new { message = passwordError });
            }
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return BadRequest(new { message = "Tài khoản chưa có email để nhận mã xác thực." });
        }

        if (!resetEmail.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Hiện chưa thể gửi mã xác thực. Vui lòng liên hệ trung tâm để được hỗ trợ."
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var activeTokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && token.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.UsedAt = DateTime.UtcNow;
        }

        var otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var reset = new PasswordResetToken
        {
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow
        };
        await dbContext.PasswordResetTokens.AddAsync(reset, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (!await resetEmail.SendAsync(user, reset, otp, cancellationToken, "approve password change", RequestLocation.Describe(Request, configuration)))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa gửi được mã xác thực. Vui lòng thử lại sau."
            });
        }

        return Ok(new { message = "Mã xác thực đã được gửi tới email của bạn." });
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
            return BadRequest(new { message = "Mật khẩu hiện tại không đúng." });
        }

        var passwordError = ValidateNewPassword(request.NewPassword, user);
        if (passwordError is not null)
        {
            return BadRequest(new { message = passwordError });
        }

        if (string.IsNullOrWhiteSpace(request.EmailOtp))
        {
            return BadRequest(new { message = "Vui lòng nhập mã xác thực OTP." });
        }

        var normalizedOtp = request.EmailOtp.Trim();
        var otpToken = await dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (otpToken is null || otpToken.FailedAttempts >= 5 || !BCrypt.Net.BCrypt.Verify(normalizedOtp, otpToken.Token))
        {
            if (otpToken is not null)
            {
                otpToken.FailedAttempts += 1;
                if (otpToken.FailedAttempts >= 5)
                {
                    otpToken.UsedAt = DateTime.UtcNow;
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            return BadRequest(new { message = "Mã xác thực không chính xác hoặc đã hết hạn." });
        }

        otpToken.UsedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var newHash = PasswordHashing.Hash(request.NewPassword);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var now = DateTime.UtcNow;
        var updated = await dbContext.Users
            .Where(item => item.Id == user.Id && item.PasswordHash == user.PasswordHash && item.Status == "Active")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PasswordHash, newHash)
                .SetProperty(item => item.FailedLoginAttempts, 0)
                .SetProperty(item => item.LockedUntil, (DateTime?)null)
                .SetProperty(item => item.AuthVersion, item => item.AuthVersion + 1)
                .SetProperty(item => item.UpdatedAt, now), cancellationToken);
        if (updated == 0)
        {
            return Unauthorized(new { message = "Mật khẩu hiện tại không còn hợp lệ." });
        }

        await dbContext.PasswordResetTokens.Where(item => item.UserId == user.Id && item.UsedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.UsedAt, now), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        user.AuthVersion += 1;
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
            user.Id, user.Username, role, centerId, user.Email, user.AuthVersion));
    }

    private AuthResponse CreateAuthResponse(
        SportsCenterManagement.BLL.DTOs.Auth.Responses.AuthenticatedUser user)
    {
        var (token, expiresAt) = tokenIssuer.Issue(user);
        return BrowserSessionCookie.Issue(HttpContext, token, user.UserId, user.Role.ToUpperInvariant(), expiresAt);
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
