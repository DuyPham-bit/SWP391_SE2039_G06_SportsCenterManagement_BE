using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

public abstract class CenterScopedControllerBase(SportsCenterDbContext db) : FlowControllerBase
{
    protected async Task<bool> CanAccessCenterAsync(long centerId, CancellationToken cancellationToken)
    {
        if (User.IsInRole("ADMIN")) return true;
        if (!User.IsInRole("MANAGER") && !User.IsInRole("RECEPTIONIST")) return true;

        return await db.StaffProfiles.AnyAsync(profile =>
            profile.UserId == CurrentUserId && profile.CenterId == centerId && profile.Status == "Active",
            cancellationToken);
    }

    protected async Task<long?> GetClassCenterIdAsync(long classId, CancellationToken cancellationToken) =>
        await db.Classes.Where(item => item.Id == classId)
            .Select(item => (long?)item.CenterId)
            .SingleOrDefaultAsync(cancellationToken);

    protected async Task<bool> CanAccessClassAsync(long classId, CancellationToken cancellationToken)
    {
        var centerId = await GetClassCenterIdAsync(classId, cancellationToken);
        return centerId is not null && await CanAccessCenterAsync(centerId.Value, cancellationToken);
    }
}
