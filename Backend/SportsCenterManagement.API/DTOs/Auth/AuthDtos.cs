using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.API.DTOs.Auth;

/// <summary>Credentials used to authenticate a user.</summary>
public sealed record LoginRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required]
    public required string Password { get; init; }
}

/// <summary>Current and replacement password for an authenticated user.</summary>
public sealed record ChangePasswordRequest
{
    [Required]
    public required string CurrentPassword { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>JWT authentication result returned after login or password change.</summary>
public sealed record AuthResponse(string AccessToken, long UserId, string Role, DateTimeOffset ExpiresAt);

/// <summary>Profile data returned for the authenticated user.</summary>
public sealed record ProfileResponse(
    long UserId,
    string Email,
    string FullName,
    string? Phone,
    string Role,
    string Status,
    string? MemberCode,
    DateTimeOffset CreatedAt,
    long? CenterId = null);

/// <summary>Editable profile fields for the authenticated user.</summary>
public sealed record UpdateProfileRequest
{
    [Required, MaxLength(150)]
    public required string FullName { get; init; }

    [MaxLength(20)]
    public string? Phone { get; init; }
}

/// <summary>Requests a one-time password reset code.</summary>
public sealed record RequestPasswordResetRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }
}

/// <summary>Confirms a password reset code and sets a new password.</summary>
public sealed record ResetPasswordRequest
{
    [Required, EmailAddress]
    public required string Email { get; init; }

    [Required, MinLength(4), MaxLength(10)]
    public required string Otp { get; init; }

    [Required, MinLength(8), MaxLength(128)]
    public required string NewPassword { get; init; }
}

/// <summary>Result of requesting a password reset code.</summary>
public sealed record PasswordResetRequestResponse(string Message, string? DevelopmentOtp);
