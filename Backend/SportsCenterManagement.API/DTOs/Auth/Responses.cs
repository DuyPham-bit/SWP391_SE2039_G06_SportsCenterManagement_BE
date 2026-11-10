namespace SportsCenterManagement.API.DTOs.Auth
{
/// <summary>JWT authentication result returned after login or password change.</summary>
public sealed record AuthResponse(string? AccessToken, long UserId, string Role, DateTimeOffset ExpiresAt);

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
    long? CenterId = null,
    string? Avatar = null,
    string? AvatarKey = null,
    bool MustChangePassword = false,
    bool IsEmailVerified = true);

/// <summary>Result of requesting a password reset code.</summary>
public sealed record PasswordResetRequestResponse(string Message, string? DevelopmentOtp);
}
