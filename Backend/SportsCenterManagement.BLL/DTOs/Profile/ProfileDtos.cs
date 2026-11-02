namespace SportsCenterManagement.BLL.DTOs.Profile;

public sealed record UserProfileResponse(
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
    string? AvatarKey = null);

public sealed record UpdateUserProfileRequest(
    string FullName,
    string? Phone = null,
    string? AvatarKey = null);

