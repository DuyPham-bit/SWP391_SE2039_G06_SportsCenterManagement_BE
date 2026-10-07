using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.API.DTOs.Auth
{
    /// <summary>Credentials used to authenticate a user.</summary>
    public sealed record LoginRequest
    {
        // The historic JSON field is named Email, but it accepts either a username or an email address.
        [Required, StringLength(150)]
        public required string Email { get; init; }

        [Required, StringLength(150)]
        public required string Password { get; init; }
    }

    /// <summary>Current and replacement password for an authenticated user.</summary>
    public sealed record ChangePasswordRequest
    {
        [Required, StringLength(150)]
        public required string CurrentPassword { get; init; }

        [Required, MinLength(12), MaxLength(128)]
        public required string NewPassword { get; init; }
    }

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

        [Required, RegularExpression("^\\d{6}$")]
        public required string Otp { get; init; }

        [Required, MinLength(12), MaxLength(128)]
        public required string NewPassword { get; init; }
    }
}
