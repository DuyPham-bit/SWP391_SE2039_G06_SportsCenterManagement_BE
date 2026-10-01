using SportsCenterManagement.BLL.DTOs.Roles;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IRolePermissionService
{
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    Task<RolePermissionsResponse> GetRolePermissionsAsync(long roleId, CancellationToken cancellationToken = default);

    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleResponse> UpdateRoleAsync(long roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default);

    Task<RolePermissionsResponse> UpdateRolePermissionsAsync(
        long roleId,
        UpdateRolePermissionsRequest request,
        long? currentUserId = null,
        CancellationToken cancellationToken = default);
}
