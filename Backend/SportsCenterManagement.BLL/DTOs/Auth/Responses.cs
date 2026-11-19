namespace SportsCenterManagement.BLL.DTOs.Auth;

public static class Responses
{
    public sealed record AuthenticatedUser(long UserId, string Username, string Role, long? CenterId, string Email = "", int AuthVersion = 0);


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
        bool IsEmailVerified = true,
        string? StaffCode = null,
        string? CenterName = null);

    /// <summary>Result of requesting a password reset code.</summary>
    public sealed record PasswordResetRequestResponse(string Message, string? DevelopmentOtp);
}
