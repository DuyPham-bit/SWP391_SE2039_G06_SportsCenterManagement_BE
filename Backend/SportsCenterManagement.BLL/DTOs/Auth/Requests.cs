using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Auth
{
    public static class Requests
    {
        public sealed record LoginRequest(
            [param: Required, StringLength(150)] string Login,
            [param: Required, StringLength(150)] string Password);

        public sealed record RegisterRequest(
            // Optional: when omitted (web self-registration) the server generates a unique login name.
            [param: StringLength(100, MinimumLength = 3)] string? Username,
            [param: Required, StringLength(150)] string Email,
            [param: Required, StringLength(128, MinimumLength = 6)] string Password,
            [param: Required, StringLength(150, MinimumLength = 1)] string FullName,
            [param: StringLength(20)] string? Phone,
            DateOnly? DateOfBirth,
            [param: Range(1, long.MaxValue)] long? CenterId = null,
            [param: RegularExpression("^[0-9]{6}$")] string? EmailOtp = null);

        /// <summary>Credentials used to authenticate a user (HTTP body of POST login).</summary>
        public sealed record LoginCredentialsRequest
        {
            // The historic JSON field is named Email, but it accepts either a username or an email address.
            [Required, StringLength(150)]
            public required string Email { get; init; }

            [Required, StringLength(150)]
            public required string Password { get; init; }
        }

        /// <summary>Requests an OTP to change password after verifying current password is correct.</summary>
        public sealed record RequestChangePasswordOtpRequest
        {
            [Required, StringLength(150)]
            [JsonPropertyName("currentPassword")]
            public required string CurrentPassword { get; init; }

            [JsonPropertyName("newPassword")]
            public string? NewPassword { get; init; }
        }

        /// <summary>Current and replacement password for an authenticated user.</summary>
        public sealed record ChangePasswordRequest
        {
            [Required, StringLength(150)]
            public required string CurrentPassword { get; init; }

            [Required, MinLength(6), MaxLength(128)]
            public required string NewPassword { get; init; }

            [JsonPropertyName("emailOtp")]
            public string? EmailOtp { get; init; }
        }

        /// <summary>Editable profile fields for the authenticated user.</summary>
        public sealed record UpdateProfileRequest
        {
            [Required, MaxLength(150)]
            public required string FullName { get; init; }

            [MaxLength(20)]
            public string? Phone { get; init; }

            [MaxLength(500)]
            public string? AvatarKey { get; init; }
        }

        /// <summary>Requests a one-time password reset code.</summary>
        public sealed record RequestPasswordResetRequest
        {
            [Required, EmailAddress, StringLength(150)]
            public required string Email { get; init; }
        }

        /// <summary>Requests a registration email verification code after validating the contact details are available.</summary>
        public sealed record RequestRegistrationEmailVerificationRequest
        {
            [Required, EmailAddress, StringLength(150)]
            public required string Email { get; init; }

            [StringLength(20)]
            public string? Phone { get; init; }
        }

        /// <summary>Confirms a password reset code and sets a new password.</summary>
        public sealed record ResetPasswordRequest
        {
            [Required, EmailAddress, StringLength(150)]
            public required string Email { get; init; }

            [Required, RegularExpression("^\\d{6}$")]
            public required string Otp { get; init; }

            [Required, MinLength(6), MaxLength(128)]
            public required string NewPassword { get; init; }

            public string? EmailOtp { get; init; }
        }
    }
}
