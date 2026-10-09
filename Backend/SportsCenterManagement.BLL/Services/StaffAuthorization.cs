using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

internal sealed record StaffAccess(
    long UserId,
    string RoleName,
    long? CenterId);

internal static class StaffAuthorization
{
    public static async Task<StaffAccess> RequireAsync(
        IUnitOfWork unitOfWork,
        long actorUserId,
        long centerId,
        IEnumerable<string> allowedRoles,
        CancellationToken cancellationToken)
    {
        if (actorUserId <= 0)
        {
            throw new UnauthorizedAccessException("A valid staff user is required.");
        }

        var allowed = allowedRoles
            .Select(NormalizeRole)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var db = unitOfWork.Context;
        var actor = await (
            from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where user.Id == actorUserId
            select new
            {
                user.Status,
                user.LockedUntil,
                RoleName = role.Name
            }).SingleOrDefaultAsync(cancellationToken);

        if (actor is null || !actor.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)
            || actor.LockedUntil > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Staff account was not found or is inactive.");
        }

        var roleKey = NormalizeRole(actor.RoleName);
        if (!allowed.Contains(roleKey))
        {
            throw new UnauthorizedAccessException("The current staff account is not allowed to perform this action.");
        }

        if (roleKey == "admin")
        {
            return new StaffAccess(actorUserId, actor.RoleName, null);
        }

        var staffProfile = await db.StaffProfiles.AsNoTracking()
            .Where(profile => profile.UserId == actorUserId)
            .SingleOrDefaultAsync(cancellationToken);

        if (staffProfile is null || !staffProfile.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Active staff profile is required for center-scoped operations.");
        }

        if (staffProfile.CenterId != centerId)
        {
            throw new UnauthorizedAccessException("The current staff account does not belong to the requested center.");
        }

        return new StaffAccess(actorUserId, actor.RoleName, staffProfile.CenterId);
    }

    private static string NormalizeRole(string roleName) =>
        roleName.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
