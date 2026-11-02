using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CenterScopeService(
    IUserRepository userRepository,
    IClassRepository classRepository) : ICenterScopeService
{
    public async Task EnsureCenterAccessAsync(long userId, bool isAdmin, long centerId, CancellationToken ct = default)
    {
        if (userId <= 0 || !await userRepository.IsUserActiveAsync(userId, ct))
        {
            throw BusinessException.Forbidden("Tài khoản hiện tại không ở trạng thái hoạt động.");
        }

        if (isAdmin) return;

        var activeStaff = await userRepository.IsStaffActiveAtCenterAsync(userId, centerId, ct);
        if (!activeStaff)
        {
            throw BusinessException.Forbidden("Bạn không có quyền thao tác tại cơ sở này.");
        }
    }

    public async Task EnsureClassAccessAsync(long userId, bool isAdmin, long classId, CancellationToken ct = default)
    {
        var centerId = await classRepository.GetCenterIdByClassIdAsync(classId, ct);
        await EnsureCenterAccessAsync(userId, isAdmin, centerId, ct);
    }
}

