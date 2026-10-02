using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Roles;

/// <summary>
/// Thông tin vai trò (Role) trong hệ thống phân quyền.
/// </summary>
/// <param name="Id">Mã định danh vai trò.</param>
/// <param name="Name">Tên vai trò (Admin, Manager, Member,...).</param>
/// <param name="Description">Mô tả chức năng của vai trò.</param>
/// <param name="CreatedAt">Thời điểm tạo vai trò.</param>
/// <param name="UserCount">Số lượng người dùng đang được gán vai trò này.</param>
/// <param name="PermissionCount">Số lượng quyền hạn được gán cho vai trò này.</param>
public sealed record RoleResponse(
    long Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int UserCount,
    int PermissionCount);

/// <summary>
/// Thông tin quyền hạn (Permission) trong hệ thống.
/// </summary>
/// <param name="Id">Mã định danh quyền hạn.</param>
/// <param name="Code">Mã code phân quyền (ví dụ: "USER_VIEW", "PAYMENT_PROCESS").</param>
/// <param name="Name">Tên hiển thị của quyền hạn.</param>
/// <param name="Description">Mô tả chi tiết phạm vi quyền hạn.</param>
public sealed record PermissionResponse(
    long Id,
    string Code,
    string Name,
    string? Description);

/// <summary>
/// Danh sách các quyền hạn được gán cho một vai trò cụ thể.
/// </summary>
/// <param name="RoleId">Mã định danh vai trò.</param>
/// <param name="RoleName">Tên vai trò.</param>
/// <param name="Permissions">Danh sách các quyền hạn thuộc vai trò.</param>
public sealed record RolePermissionsResponse(
    long RoleId,
    string RoleName,
    IReadOnlyList<PermissionResponse> Permissions);

/// <summary>
/// Yêu cầu cập nhật danh sách quyền hạn cho một vai trò.
/// </summary>
public sealed record UpdateRolePermissionsRequest
{
    /// <summary>
    /// Danh sách các ID quyền hạn mới được gán cho vai trò.
    /// </summary>
    [Required]
    public required List<long> PermissionIds { get; init; } = [];
}

/// <summary>
/// Yêu cầu tạo mới một vai trò người dùng trong hệ thống.
/// </summary>
public sealed record CreateRoleRequest
{
    /// <summary>
    /// Tên vai trò duy nhất (tối đa 50 ký tự).
    /// </summary>
    [Required, MaxLength(50)]
    public required string Name { get; init; }

    /// <summary>
    /// Mô tả chi tiết chức năng hoặc mục đích của vai trò.
    /// </summary>
    [MaxLength(255)]
    public string? Description { get; init; }
}

/// <summary>
/// Yêu cầu cập nhật thông tin vai trò người dùng.
/// </summary>
public sealed record UpdateRoleRequest
{
    /// <summary>
    /// Tên vai trò mới (tối đa 50 ký tự).
    /// </summary>
    [Required, MaxLength(50)]
    public required string Name { get; init; }

    /// <summary>
    /// Mô tả chi tiết vai trò.
    /// </summary>
    [MaxLength(255)]
    public string? Description { get; init; }
}
