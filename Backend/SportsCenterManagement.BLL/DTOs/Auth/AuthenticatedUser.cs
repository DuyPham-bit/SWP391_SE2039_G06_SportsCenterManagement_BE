namespace SportsCenterManagement.BLL.DTOs.Auth;

/// <summary>
/// Đại diện cho thông tin tài khoản người dùng sau khi xác thực thành công.
/// </summary>
/// <param name="UserId">ID định danh người dùng trong hệ thống.</param>
/// <param name="Username">Tên tài khoản người dùng.</param>
/// <param name="Role">Tên vai trò (Role) chính của người dùng (Admin, Manager, Member, Coach, Staff).</param>
/// <param name="CenterId">Mã trung tâm trực thuộc (nếu có, ví dụ dành cho Nhân viên/HLV).</param>
public sealed record AuthenticatedUser(long UserId, string Username, string Role, long? CenterId);
