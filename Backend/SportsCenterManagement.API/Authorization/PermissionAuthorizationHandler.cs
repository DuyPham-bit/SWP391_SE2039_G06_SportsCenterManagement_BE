using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.API.Authorization;

public sealed class PermissionAuthorizationHandler(IUnitOfWork unitOfWork)
    : AuthorizationHandler<PermissionRequirement>
{
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

        var hasPermission = await (
            from rolePermission in unitOfWork.Context.RolePermissions.AsNoTracking()
            join permission in unitOfWork.Context.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where rolePermission.RoleId == user.RoleId && permission.Code == requirement.PermissionCode
            select permission.Id).AnyAsync();

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
