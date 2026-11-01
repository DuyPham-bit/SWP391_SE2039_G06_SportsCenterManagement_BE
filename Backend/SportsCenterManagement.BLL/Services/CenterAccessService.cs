using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class CenterAccessService(IUnitOfWork unitOfWork) : ICenterAccessService
{
    public Task<bool> IsActiveCenterAsync(long centerId, CancellationToken cancellationToken = default) =>
        unitOfWork.Repository<Center>().AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken);

    public async Task EnsureCenterAccessAsync(long actorUserId, bool isSystemAdmin, long centerId, CancellationToken cancellationToken = default)
    {
        var activeUser = actorUserId > 0 && await unitOfWork.Repository<User>().AnyAsync(
            user => user.Id == actorUserId && user.Status == "Active" && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow), cancellationToken);
        if (!activeUser) throw Forbidden("Tài khoản hiện tại không ở trạng thái hoạt động.");
        if (isSystemAdmin) return;

        var activeStaff = await unitOfWork.Repository<StaffProfile>().AnyAsync(
            staff => staff.UserId == actorUserId && staff.CenterId == centerId && staff.Status == "Active", cancellationToken);
        if (!activeStaff) throw Forbidden("Bạn không có quyền thao tác tại cơ sở này.");
    }

    public async Task EnsureClassAccessAsync(long actorUserId, bool isSystemAdmin, long classId, CancellationToken cancellationToken = default)
    {
        var centerId = await unitOfWork.Repository<ClassEntity>().Find(entity => entity.Id == classId)
            .Select(entity => (long?)entity.CenterId).SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFound("Lớp học không tồn tại.");
        await EnsureCenterAccessAsync(actorUserId, isSystemAdmin, centerId, cancellationToken);
    }

    private static InvalidOperationException Forbidden(string message) => new(message) { Data = { ["StatusCode"] = 403 } };
    private static InvalidOperationException NotFound(string message) => new(message) { Data = { ["StatusCode"] = 404 } };
}
