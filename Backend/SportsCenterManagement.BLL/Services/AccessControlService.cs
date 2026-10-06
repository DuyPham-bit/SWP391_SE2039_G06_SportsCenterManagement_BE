using System.Data;
using Microsoft.EntityFrameworkCore;
using AccessControlRequests = SportsCenterManagement.BLL.DTOs.AccessControl.Requests;
using AccessControlResponses = SportsCenterManagement.BLL.DTOs.AccessControl.Responses;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class AccessControlService(IUnitOfWork unitOfWork) : IAccessControlService
{
    public async Task<IReadOnlyList<AccessControlResponses.RolePermissionMatrixResponse>> GetRolePermissionMatrixAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await unitOfWork.Context.Roles.AsNoTracking()
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);
        var grants = await (
            from rolePermission in unitOfWork.Context.RolePermissions.AsNoTracking()
            join permission in unitOfWork.Context.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            select new { rolePermission.RoleId, permission.Code, permission.Name, permission.Description })
            .ToListAsync(cancellationToken);

        return roles.Select(role => new AccessControlResponses.RolePermissionMatrixResponse(
                role.Id,
                role.Name,
                role.Description,
                grants.Where(grant => grant.RoleId == role.Id)
                    .OrderBy(grant => grant.Code)
                    .Select(grant => new AccessControlResponses.PermissionEntry(
                        grant.Code, grant.Name, grant.Description))
                    .ToArray()))
            .ToArray();
    }

    public async Task<AccessControlResponses.RolePermissionMatrixResponse> ReplaceRolePermissionsAsync(
        long actorUserId,
        long roleId,
        AccessControlRequests.ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PermissionCodes is null)
        {
            throw new InvalidOperationException("Permission codes are required.");
        }
        var actor = await unitOfWork.Context.Users.AsNoTracking()
            .Where(user => user.Id == actorUserId && user.Status == "Active"
                           && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow))
            .Select(user => new { user.RoleId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new UnauthorizedAccessException("Tài khoản không có quyền quản lý ma trận role.");
        var systemAdminRole = await unitOfWork.Context.Roles.AsNoTracking()
            .SingleOrDefaultAsync(role => role.Name == RoleNames.SystemAdmin, cancellationToken)
            ?? throw new InvalidOperationException("Role SystemAdmin chưa được khởi tạo.");
        if (actor.RoleId != systemAdminRole.Id)
        {
            throw new UnauthorizedAccessException("Chỉ System Admin được quản lý ma trận role.");
        }

        var codes = request.PermissionCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (codes.Length != request.PermissionCodes.Count || codes.Length > 100)
        {
            throw new InvalidOperationException("Permission codes must be unique and limited to 100 entries.");
        }

        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var role = await unitOfWork.Context.Roles.SingleOrDefaultAsync(
            item => item.Id == roleId, cancellationToken)
            ?? throw new KeyNotFoundException("Role not found.");
        var selectedPermissions = await unitOfWork.Context.Permissions
            .Where(permission => codes.Contains(permission.Code))
            .OrderBy(permission => permission.Code)
            .ToListAsync(cancellationToken);
        if (selectedPermissions.Count != codes.Length)
        {
            throw new InvalidOperationException("One or more permission codes are unknown.");
        }

        var rolePermissionManageId = await unitOfWork.Context.Permissions
            .Where(permission => permission.Code == PermissionCodes.RolePermissionManage)
            .Select(permission => (long?)permission.Id)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Permission quản lý role chưa được khởi tạo.");
        if (selectedPermissions.Any(permission => permission.Id == rolePermissionManageId)
            && roleId != systemAdminRole.Id)
        {
            throw new InvalidOperationException("Chỉ role SystemAdmin mới được giữ quyền quản lý ma trận role.");
        }
        if (roleId == systemAdminRole.Id
            && selectedPermissions.All(permission => permission.Id != rolePermissionManageId))
        {
            throw new InvalidOperationException("Không thể gỡ quyền quản lý ma trận khỏi role SystemAdmin.");
        }

        var existing = await unitOfWork.Context.RolePermissions
            .Where(link => link.RoleId == roleId)
            .ToListAsync(cancellationToken);
        var existingPermissionIds = existing.Select(link => link.PermissionId).ToHashSet();
        var selectedPermissionIds = selectedPermissions.Select(permission => permission.Id).ToHashSet();
        var oldCodes = await (
            from link in unitOfWork.Context.RolePermissions.AsNoTracking()
            join permission in unitOfWork.Context.Permissions.AsNoTracking()
                on link.PermissionId equals permission.Id
            where link.RoleId == roleId
            select permission.Code).ToArrayAsync(cancellationToken);
        unitOfWork.Context.RolePermissions.RemoveRange(
            existing.Where(link => !selectedPermissionIds.Contains(link.PermissionId)));
        unitOfWork.Context.RolePermissions.AddRange(selectedPermissions
            .Where(permission => !existingPermissionIds.Contains(permission.Id))
            .Select(permission => new RolePermission { RoleId = roleId, PermissionId = permission.Id }));
        AuditLogWriter.Add(unitOfWork, actorUserId, null, "role.permissions.replaced", "Role", role.Id,
            new { PermissionCodes = oldCodes }, new { PermissionCodes = codes });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AccessControlResponses.RolePermissionMatrixResponse(
            role.Id,
            role.Name,
            role.Description,
            selectedPermissions.Select(permission => new AccessControlResponses.PermissionEntry(
                permission.Code, permission.Name, permission.Description)).ToArray());
    }
}
