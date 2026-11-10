using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CenterScopeService(
    IUserRepository userRepository,
    IClassRepository classRepository,
    ICatalogRepository catalogRepository) : ICenterScopeService
{
    public Task<bool> IsActiveCenterAsync(long centerId, CancellationToken ct = default) =>
        catalogRepository.IsActiveCenterAsync(centerId, ct);

    private const string NoCenterAccessMessage = "Bạn không có quyền thao tác tại cơ sở này.";

    public async Task EnsureCenterAccessAsync(long userId, bool isAdmin, long centerId, CancellationToken ct = default)
    {
        await EnsureActiveUserAsync(userId, ct);
        if (isAdmin) return;
        if (!await userRepository.IsStaffActiveAtCenterAsync(userId, centerId, ct))
            throw BusinessException.Forbidden(NoCenterAccessMessage);
    }

    public async Task EnsureClassAccessAsync(long userId, bool isAdmin, long classId, CancellationToken ct = default)
    {
        await EnsureActiveUserAsync(userId, ct);
        var centerId = await classRepository.GetCenterIdByClassIdAsync(classId, ct);
        if (centerId is null)
        {
            // Non-admins get the same 403 as a foreign class so class IDs cannot be enumerated.
            throw isAdmin ? BusinessException.NotFound("Lớp học không tồn tại.") : BusinessException.Forbidden(NoCenterAccessMessage);
        }
        if (isAdmin) return;
        if (!await userRepository.IsStaffActiveAtCenterAsync(userId, centerId.Value, ct))
            throw BusinessException.Forbidden(NoCenterAccessMessage);
    }

    private async Task EnsureActiveUserAsync(long userId, CancellationToken ct)
    {
        if (userId <= 0 || !await userRepository.IsUserActiveAsync(userId, ct))
            throw BusinessException.Forbidden("Tài khoản hiện tại không ở trạng thái hoạt động.");
    }
}

