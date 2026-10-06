using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Authorization;

internal static class CenterScope
{
    public static async Task EnsureCenterAccessAsync(
        SportsCenterDbContext db, ClaimsPrincipal principal, long centerId, CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var activeUser = userId > 0 && await db.Users.AnyAsync(user => user.Id == userId
            && user.Status == "Active" && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow),
            cancellationToken);
        if (!activeUser)
            throw FlowException.Forbidden("Tài khoản hiện tại không ở trạng thái hoạt động.");
        if (principal.IsInRole("ADMIN")) return;

        var activeStaff = await db.StaffProfiles.AnyAsync(staff =>
            staff.UserId == userId && staff.CenterId == centerId && staff.Status == "Active",
            cancellationToken);
        if (!activeStaff)
            throw FlowException.Forbidden("Bạn không có quyền thao tác tại cơ sở này.");
    }

    public static async Task EnsureClassAccessAsync(
        SportsCenterDbContext db, ClaimsPrincipal principal, long classId, CancellationToken cancellationToken)
    {
        var centerId = await db.Classes.Where(entity => entity.Id == classId)
            .Select(entity => (long?)entity.CenterId).SingleOrDefaultAsync(cancellationToken)
            ?? throw FlowException.NotFound("Lớp học không tồn tại.");
        await EnsureCenterAccessAsync(db, principal, centerId, cancellationToken);
    }

    public static long GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : 0;
    }
}
