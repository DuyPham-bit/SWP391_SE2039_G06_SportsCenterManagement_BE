using System.Net;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed record MediaAccessScope(long UserId, long? CenterId, bool IsAdmin, bool IsManager)
{
    public const string R2FileIdPrefix = "r2:";

    public static async Task<MediaAccessScope> ForUser(SportsCenterDbContext db, long userId, CancellationToken ct)
    {
        var role = await (from u in db.Users.AsNoTracking() join r in db.Roles on u.RoleId equals r.Id
            where u.Id == userId && u.Status == "Active" select r.Name).SingleOrDefaultAsync(ct);
        if (role is null) throw new BusinessException(HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
        long? centerId = role.ToUpperInvariant() switch
        {
            "MANAGER" or "RECEPTIONIST" => await db.StaffProfiles.Where(p => p.UserId == userId && p.Status == "Active")
                .Select(p => (long?)p.CenterId).SingleOrDefaultAsync(ct),
            "COACH" => await db.CoachProfiles.Where(p => p.UserId == userId && p.Status == "Active")
                .Select(p => (long?)p.CenterId).SingleOrDefaultAsync(ct),
            "MEMBER" => await db.MemberProfiles.Where(p => p.UserId == userId).Select(p => p.CenterId).SingleOrDefaultAsync(ct),
            _ => null
        };
        return new(userId, centerId,
            role.Equals("Admin", StringComparison.OrdinalIgnoreCase), role.Equals("Manager", StringComparison.OrdinalIgnoreCase));
    }

    public IQueryable<UploadedMedia> Library(SportsCenterDbContext db)
    {
        // Refund evidence, dashboards and personal avatars are not gallery assets.
        // Legacy records remain visible until the administrator migrates them to R2.
        var query = db.UploadedMedias.AsNoTracking().Where(m =>
            !m.DriveFileId.StartsWith(R2FileIdPrefix) ||
            m.DriveFileId.StartsWith(R2FileIdPrefix + "media/"));
        if (IsAdmin) return query;
        return CenterId.HasValue
            ? query.Where(m => m.CenterId == CenterId)
            : query.Where(m => m.CenterId == null && m.UploadedByUserId == UserId);
    }
}
