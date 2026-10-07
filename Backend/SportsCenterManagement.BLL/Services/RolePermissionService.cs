using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class RolePermissionService(IUnitOfWork unitOfWork) : IRolePermissionService
{
    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var db = unitOfWork.Context;

        var roles = await db.Roles.OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var userCounts = await db.Users
            .GroupBy(u => u.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var permissionCounts = await db.RolePermissions
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        return roles.Select(role => new RoleResponse(
            role.Id,
            role.Name,
            role.Description,
            role.CreatedAt,
            userCounts.TryGetValue(role.Id, out var uCount) ? uCount : 0,
            permissionCounts.TryGetValue(role.Id, out var pCount) ? pCount : 0
        )).ToList();
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await unitOfWork.Repository<Permission>().GetAllAsync(cancellationToken);
        return permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionResponse(p.Id, p.Code, p.Name, p.Description, p.Module))
            .ToList();
    }

    public async Task<RolePermissionsResponse> GetRolePermissionsAsync(long roleId, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

        var db = unitOfWork.Context;
        var permissions = await (
            from rp in db.RolePermissions
            join p in db.Permissions on rp.PermissionId equals p.Id
            where rp.RoleId == roleId
            orderby p.Code
            select new PermissionResponse(p.Id, p.Code, p.Name, p.Description, p.Module)
        ).ToListAsync(cancellationToken);

        return new RolePermissionsResponse(role.Id, role.Name, permissions);
    }

    private static readonly HashSet<string> ProtectedSystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Super Admin", RoleNames.SystemAdmin, "Manager", "Receptionist", "Coach", "Member"
    };

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Tên vai trò không được để trống.");
        }

        var trimmedName = request.Name.Trim();
        var exists = await unitOfWork.Repository<Role>()
            .AnyAsync(r => r.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Tên vai trò '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        var role = new Role
        {
            Name = trimmedName,
            Description = request.Description?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await unitOfWork.Repository<Role>().AddAsync(role, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RoleResponse(role.Id, role.Name, role.Description, role.CreatedAt, 0, 0);
    }

    public async Task<RoleResponse> UpdateRoleAsync(long roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Tên vai trò không được để trống.");
        }

        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

        var trimmedName = request.Name.Trim();

        if (ProtectedSystemRoles.Contains(role.Name))
        {
            throw new InvalidOperationException($"Không thể sửa vai trò hệ thống mặc định '{role.Name}'.");
        }

        var exists = await unitOfWork.Repository<Role>()
            .AnyAsync(r => r.Id != roleId && r.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"Tên vai trò '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        role.Name = trimmedName;
        role.Description = request.Description?.Trim();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var db = unitOfWork.Context;
        var userCount = await db.Users.CountAsync(u => u.RoleId == roleId, cancellationToken);
        var permCount = await db.RolePermissions.CountAsync(rp => rp.RoleId == roleId, cancellationToken);

        return new RoleResponse(role.Id, role.Name, role.Description, role.CreatedAt, userCount, permCount);
    }

    public async Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default)
    {
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

        if (ProtectedSystemRoles.Contains(role.Name))
        {
            throw new InvalidOperationException("Các vai trò hệ thống mặc định (System Roles) được bảo vệ và không thể bị xóa.");
        }

        var db = unitOfWork.Context;
        var userCount = await db.Users.CountAsync(u => u.RoleId == roleId, cancellationToken);
        if (userCount > 0)
        {
            throw new InvalidOperationException(
                $"Không thể xóa vai trò '{role.Name}' vì đang có {userCount} người dùng đang được gán vai trò này. " +
                $"Vui lòng chuyển đổi vai trò cho các người dùng đó trước khi xóa.");
        }

        var rolePermissions = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(rolePermissions);

        unitOfWork.Repository<Role>().Remove(role);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<RolePermissionsResponse> UpdateRolePermissionsAsync(
        long roleId,
        UpdateRolePermissionsRequest request,
        long? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!currentUserId.HasValue)
            throw new UnauthorizedAccessException("Chỉ System Admin được quản lý quyền vai trò.");
        var actorRoleId = await unitOfWork.Context.Users
            .Where(user => user.Id == currentUserId.Value && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow))
            .Select(user => (long?)user.RoleId)
            .SingleOrDefaultAsync(cancellationToken);
        var systemAdminRoleId = await unitOfWork.Context.Roles
            .Where(role => role.Name == RoleNames.SystemAdmin)
            .Select(role => (long?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (actorRoleId is null || systemAdminRoleId is null || actorRoleId.Value != systemAdminRoleId.Value)
            throw new UnauthorizedAccessException("Chỉ System Admin được quản lý quyền vai trò.");

        if (request?.PermissionIds is null || request.PermissionIds.Count == 0)
        {
            throw new InvalidOperationException("Phải chọn ít nhất một quyền trước khi lưu.");
        }

        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

        if (ProtectedSystemRoles.Contains(role.Name))
        {
            throw new InvalidOperationException($"Không thể thay đổi quyền của vai trò hệ thống mặc định '{role.Name}'.");
        }

        var distinctPermissionIds = request.PermissionIds.Distinct().ToList();
        var db = unitOfWork.Context;

        if (distinctPermissionIds.Count > 0)
        {
            var existingPerms = await db.Permissions
                .Where(p => distinctPermissionIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            if (existingPerms.Count != distinctPermissionIds.Count)
            {
                throw new InvalidOperationException("Một hoặc nhiều ID quyền hạn không tồn tại trong hệ thống.");
            }

            // Tự động bổ sung quyền cha nếu gán quyền con (Parent-Child Dependency Resolution)
            var currentCodes = existingPerms.Select(p => p.Code).ToHashSet();
            if (currentCodes.Contains(PermissionCodes.RolePermissionManage))
            {
                throw new InvalidOperationException("Quyền quản lý ma trận chỉ được gán cho SystemAdmin qua API quản trị ma trận.");
            }
            var requiredParentCodes = new HashSet<string>();

            if (currentCodes.Contains("CLASS_MANAGE") || currentCodes.Contains("COACH_ASSIGN"))
            {
                requiredParentCodes.Add("CLASS_VIEW");
            }
            if (currentCodes.Contains("MEMBER_MANAGE"))
            {
                requiredParentCodes.Add("MEMBER_VIEW");
            }

            foreach (var parentCode in requiredParentCodes)
            {
                if (!currentCodes.Contains(parentCode))
                {
                    var parentPerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == parentCode, cancellationToken);
                    if (parentPerm is not null && !distinctPermissionIds.Contains(parentPerm.Id))
                    {
                        distinctPermissionIds.Add(parentPerm.Id);
                    }
                }
            }
        }

        // Tự bảo vệ không để Self-Lockout (Nếu user hiện tại đang giữ role này và định gỡ bỏ ROLE_MANAGE)
        if (currentUserId.HasValue)
        {
            var currentUser = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUserId.Value, cancellationToken);
            if (currentUser is not null && currentUser.RoleId == roleId)
            {
                var roleManagePerm = await db.Permissions.FirstOrDefaultAsync(
                    p => p.Code == PermissionCodes.RolePermissionManage, cancellationToken);
                if (roleManagePerm is not null && !distinctPermissionIds.Contains(roleManagePerm.Id))
                {
                    throw new InvalidOperationException(
                        "Bạn không thể gỡ bỏ quyền Quản lý vai trò & quyền hạn (ROLE_MANAGE) khỏi vai trò hiện tại của chính mình.");
                }
            }
        }

        var existingRolePermissions = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(cancellationToken);

        db.RolePermissions.RemoveRange(existingRolePermissions);

        foreach (var permId in distinctPermissionIds)
        {
            await db.RolePermissions.AddAsync(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permId
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetRolePermissionsAsync(roleId, cancellationToken);
    }
}
