using SportsCenterManagement.DAL.Context;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.BLL.Services.AccessControl;

/// <summary>
/// Center / class access checks used by the API controllers (moved verbatim from API Authorization/CenterScope).
/// Failures are <see cref="InvalidOperationException"/> carrying Data["StatusCode"] (403 / 404), exactly as before.
/// <see cref="ICenterScopeService"/> is the newer BusinessException-based variant, which answers 403 for unknown
/// classes; merging the two would change API responses, so it is left as a separate decision.
/// </summary>
public sealed class CenterAccessGuard(IUnitOfWork unitOfWork)
{
    private readonly SportsCenterDbContext db = unitOfWork.Context;

    public async Task EnsureCenterAccessAsync(long userId, bool isAdmin, long centerId, CancellationToken cancellationToken)
    {
        var activeUser = userId > 0 && await db.Users.AnyAsync(user => user.Id == userId
            && user.Status == "Active" && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow),
            cancellationToken);
        if (!activeUser)
            throw new InvalidOperationException("Tài khoản hiện tại không ở trạng thái hoạt động.") { Data = { ["StatusCode"] = 403 } };
        if (isAdmin) return;

        var activeStaff = await db.StaffProfiles.AnyAsync(staff =>
            staff.UserId == userId && staff.CenterId == centerId && staff.Status == "Active",
            cancellationToken);
        if (!activeStaff)
            throw new InvalidOperationException("Bạn không có quyền thao tác tại cơ sở này.") { Data = { ["StatusCode"] = 403 } };
    }

    public async Task EnsureClassAccessAsync(long userId, bool isAdmin, long classId, CancellationToken cancellationToken)
    {
        var centerId = await db.Classes.Where(entity => entity.Id == classId)
            .Select(entity => (long?)entity.CenterId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        await EnsureCenterAccessAsync(userId, isAdmin, centerId, cancellationToken);
    }
}
