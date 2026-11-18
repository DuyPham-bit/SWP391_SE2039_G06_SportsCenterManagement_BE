namespace SportsCenterManagement.BLL.DTOs.Profile;

public sealed record UpdateUserProfileRequest(
    string FullName,
    string? Phone = null,
    string? AvatarKey = null);

