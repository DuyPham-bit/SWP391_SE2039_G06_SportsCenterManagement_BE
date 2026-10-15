using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Repositories.Interfaces;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Authorization;

public sealed class PermissionAuthorizationHandler(IUnitOfWork unitOfWork)
    : AuthorizationHandler<PermissionRequirement>
{
    private static readonly HashSet<string> ManagerCenterPermissions =
    [
        PermissionCodes.MemberCenterRead,
        PermissionCodes.MemberCenterCreate,
        PermissionCodes.MemberCenterUpdate,
        PermissionCodes.MemberCenterManageStatus,
        PermissionCodes.StaffCenterRead,
        PermissionCodes.StaffCenterCreate,
        PermissionCodes.StaffCenterUpdate,
        PermissionCodes.MembershipPackagesManage,
        PermissionCodes.SubscriptionRead
    ];

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdText = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(userIdText, out var userId))
        {
            return;
        }

        var user = await unitOfWork.Repository<SportsCenterManagement.DAL.Entities.User>()
            .GetByIdAsync(userId);
        if (user is null || user.Status != "Active" || user.LockedUntil > DateTime.UtcNow)
        {
            return;
        }

        var role = await unitOfWork.Repository<SportsCenterManagement.DAL.Entities.Role>()
            .GetByIdAsync(user.RoleId);
        if (role?.Name == RoleNames.Manager && ManagerCenterPermissions.Contains(requirement.PermissionCode))
        {
            var assignedCenter = await CenterRolePolicy.CenterForUser(unitOfWork.Context, userId, CancellationToken.None);
            if (assignedCenter.HasValue && await unitOfWork.Context.StaffProfiles.AsNoTracking().AnyAsync(
                    profile => profile.UserId == userId && profile.CenterId == assignedCenter.Value && profile.Status == "Active"))
                context.Succeed(requirement);
            return;
        }

        var hasPermission = await (
            from rolePermission in unitOfWork.Context.RolePermissions.AsNoTracking()
            join permission in unitOfWork.Context.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where rolePermission.RoleId == user.RoleId && permission.Code == requirement.PermissionCode
            select permission.Id).AnyAsync();

        if (hasPermission)
        {
            var centerId = await CenterRolePolicy.CenterForUser(unitOfWork.Context, userId, CancellationToken.None);
            var policy = centerId.HasValue ? await CenterRolePolicy.Read(unitOfWork.Context, centerId.Value, user.RoleId, CancellationToken.None) : null;
            if (policy is not null && !policy.PermissionCodes.Contains(requirement.PermissionCode)) return;
            context.Succeed(requirement);
        }
    }
}
