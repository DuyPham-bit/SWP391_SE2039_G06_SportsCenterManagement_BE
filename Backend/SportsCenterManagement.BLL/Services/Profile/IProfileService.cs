using SportsCenterManagement.BLL.DTOs.Profile;

namespace SportsCenterManagement.BLL.Services.Profile;

public interface IProfileService
{
    Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken ct = default);
    Task<UserProfileResponse> UpdateProfileAsync(long userId, UpdateUserProfileRequest request, CancellationToken ct = default);
}

