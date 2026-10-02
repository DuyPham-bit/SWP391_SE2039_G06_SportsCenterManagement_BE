using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Interfaces;
<<<<<<< Updated upstream
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
            .Select(p => new PermissionResponse(p.Id, p.Code, p.Name, p.Description))
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
            select new PermissionResponse(p.Id, p.Code, p.Name, p.Description)
        ).ToListAsync(cancellationToken);

        return new RolePermissionsResponse(role.Id, role.Name, permissions);
    }

    private static readonly HashSet<string> ProtectedSystemRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Super Admin"
    };

    public async Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
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
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

        var trimmedName = request.Name.Trim();

        if (ProtectedSystemRoles.Contains(role.Name) && !string.Equals(role.Name, trimmedName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Không thể đổi tên vai trò hệ thống mặc định '{role.Name}'.");
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
=======
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed class RolePermissionService(SportsCenterDbContext db) : IRolePermissionService
{
    private static readonly HashSet<string> SystemRoleNames = new(StringComparer.OrdinalIgnoreCase)
        { "Admin", "Manager", "Receptionist", "Coach", "Member", "Super Admin" };

    public async Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        var permissionRows = await (from link in db.RolePermissions
                                    join permission in db.Permissions on link.PermissionId equals permission.Id
                                    select new { link.RoleId, permission.Code }).ToListAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().GroupBy(x => x.RoleId)
            .Select(x => new { RoleId = x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.RoleId, x => x.Count,
                cancellationToken);
        return roles.Select(role => new RoleResponse(role.Id, role.Name, role.Description,
            IsSystemRole(role.Name), permissionRows.Where(x => x.RoleId == role.Id).Select(x => x.Code).ToArray(),
            users.GetValueOrDefault(role.Id))).ToArray();
    }

    public async Task<IReadOnlyList<PermissionResponse>> GetPermissionsAsync(CancellationToken cancellationToken = default) =>
        await db.Permissions.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new PermissionResponse(x.Id, x.Code, x.Name, x.Description))
            .ToListAsync(cancellationToken);

    public async Task<RoleResponse> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken = default)
    {
        var name = ValidateRequest(request);
        if (await db.Roles.AnyAsync(x => x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Tên vai trò đã tồn tại.");
        var permissions = await ValidatePermissionsAsync(request.PermissionCodes, cancellationToken);
        var role = new Role { Name = name, Description = request.Description?.Trim(), CreatedAt = DateTime.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var permission in permissions)
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync(cancellationToken);
        return new RoleResponse(role.Id, role.Name, role.Description, false,
            permissions.Select(x => x.Code).ToArray(), 0);
    }

    public async Task<RoleResponse> UpdateRoleAsync(long roleId, SaveRoleRequest request, long actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");
        if (IsSystemRole(role.Name))
            throw new InvalidOperationException("Không thể sửa vai trò hệ thống.");
        var name = ValidateRequest(request);
        if (await db.Roles.AnyAsync(x => x.Id != roleId && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Tên vai trò đã tồn tại.");
        var permissions = await ValidatePermissionsAsync(request.PermissionCodes, cancellationToken);

        var actor = actorUserId > 0
            ? await db.Users.SingleOrDefaultAsync(x => x.Id == actorUserId, cancellationToken)
            : null;
        if (actor?.RoleId == roleId)
        {
            var currentCodes = await (from link in db.RolePermissions
                                      join permission in db.Permissions on link.PermissionId equals permission.Id
                                      where link.RoleId == roleId
                                      select permission.Code).ToListAsync(cancellationToken);
            if (currentCodes.Any(IsRoleManagementCode) && !request.PermissionCodes.Any(IsRoleManagementCode))
                throw new InvalidOperationException("Không thể tự gỡ quyền quản lý phân quyền khỏi vai trò đang sử dụng.");
        }

        role.Name = name;
        role.Description = request.Description?.Trim();
        var oldLinks = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        var requestedIds = permissions.Select(x => x.Id).ToHashSet();
        db.RolePermissions.RemoveRange(oldLinks.Where(x => !requestedIds.Contains(x.PermissionId)));
        var existingIds = oldLinks.Select(x => x.PermissionId).ToHashSet();
        db.RolePermissions.AddRange(permissions.Where(x => !existingIds.Contains(x.Id)).Select(x => new RolePermission
            { RoleId = roleId, PermissionId = x.Id }));
        await db.SaveChangesAsync(cancellationToken);
        var assignedUsers = await db.Users.CountAsync(x => x.RoleId == roleId, cancellationToken);
        return new RoleResponse(role.Id, role.Name, role.Description, false,
            permissions.Select(x => x.Code).ToArray(), assignedUsers);
>>>>>>> Stashed changes
    }

    public async Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default)
    {
<<<<<<< Updated upstream
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
        var role = await unitOfWork.Repository<Role>().GetByIdAsync(roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");

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
                var roleManagePerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "ROLE_MANAGE", cancellationToken);
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
=======
        var role = await db.Roles.SingleOrDefaultAsync(x => x.Id == roleId, cancellationToken)
            ?? throw new InvalidOperationException("Vai trò không tồn tại.");
        if (IsSystemRole(role.Name))
            throw new InvalidOperationException("Không thể xóa vai trò hệ thống.");
        var usersCount = await db.Users.CountAsync(x => x.RoleId == roleId, cancellationToken);
        if (usersCount > 0)
            throw new InvalidOperationException($"Không thể xóa vai trò đang được gán cho {usersCount} người dùng; hãy chuyển họ sang vai trò khác trước.");
        var links = await db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(links);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<Permission>> ValidatePermissionsAsync(IReadOnlyList<string> codes,
        CancellationToken cancellationToken)
    {
        var requested = codes.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (requested.Length == 0)
            throw new InvalidOperationException("Phải chọn ít nhất một quyền.");
        var permissions = await db.Permissions.Where(x => requested.Contains(x.Code)).ToListAsync(cancellationToken);
        if (permissions.Count != requested.Length)
            throw new InvalidOperationException("Danh sách quyền có mã không tồn tại.");
        ValidatePermissionDependencies(permissions.Select(x => x.Code).ToArray());
        return permissions;
    }

    private static void ValidatePermissionDependencies(IReadOnlyList<string> codes)
    {
        var set = codes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var actions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "CREATE", "ADD", "EDIT", "UPDATE", "DELETE", "REMOVE", "MANAGE", "WRITE" };
        foreach (var code in codes)
        {
            var separator = code.LastIndexOf('_');
            if (separator < 1 || !actions.Contains(code[(separator + 1)..])) continue;
            var module = code[..separator];
            if (!new[] { $"{module}_VIEW", $"{module}_READ", $"{module}_LIST" }.Any(set.Contains))
                throw new InvalidOperationException($"Quyền '{code}' yêu cầu quyền xem '{module}_VIEW' (hoặc READ/LIST) cùng module.");
        }
    }

    private static string ValidateRequest(SaveRoleRequest request)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên vai trò là bắt buộc.");
        if (request.PermissionCodes is null || request.PermissionCodes.Count == 0 ||
            request.PermissionCodes.All(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Phải chọn ít nhất một quyền.");
        return name;
    }

    private static bool IsSystemRole(string name) => SystemRoleNames.Contains(name.Trim());

    private static bool IsRoleManagementCode(string code) =>
        code.Contains("ROLE", StringComparison.OrdinalIgnoreCase) &&
        (code.Contains("MANAGE", StringComparison.OrdinalIgnoreCase) ||
         code.Contains("PERMISSION", StringComparison.OrdinalIgnoreCase));

>>>>>>> Stashed changes
}
