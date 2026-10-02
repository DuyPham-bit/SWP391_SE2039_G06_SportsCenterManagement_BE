using SportsCenterManagement.BLL.DTOs.Roles;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ quản lý vai trò và phân quyền (Roles &amp; Permissions - UC-15).
/// </summary>
public interface IRolePermissionService
{
    /// <summary>
    /// Lấy danh sách toàn bộ các vai trò người dùng hiện có trong hệ thống.
    /// </summary>
    Task<IReadOnlyList<RoleResponse>> GetRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách toàn bộ các quyền hạn được định nghĩa trong hệ thống.
    /// </summary>
    Task<IReadOnlyList<PermissionResponse>> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy danh sách các quyền hạn được gán cho một vai trò cụ thể.
    /// </summary>
    Task<RolePermissionsResponse> GetRolePermissionsAsync(long roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tạo mới một vai trò người dùng trong hệ thống.
    /// </summary>
    Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật tên và mô tả của một vai trò người dùng.
    /// </summary>
    Task<RoleResponse> UpdateRoleAsync(long roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xóa một vai trò người dùng khỏi hệ thống.
    /// </summary>
    Task DeleteRoleAsync(long roleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cập nhật danh sách quyền hạn cho một vai trò cụ thể.
    /// </summary>
    Task<RolePermissionsResponse> UpdateRolePermissionsAsync(
        long roleId,
        UpdateRolePermissionsRequest request,
        long? currentUserId = null,
        CancellationToken cancellationToken = default);
}
