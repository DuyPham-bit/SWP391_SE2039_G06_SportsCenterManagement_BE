namespace SportsCenterManagement.BLL.DTOs.Auth;

/// <summary>
/// Kết quả phản hồi sau khi xác thực đăng nhập thành công.
/// </summary>
/// <param name="AccessToken">Chuỗi Bearer Token xác thực người dùng.</param>
/// <param name="ExpiresAt">Thời điểm token hết hạn sử dụng.</param>
/// <param name="Username">Tên đăng nhập của người dùng.</param>
/// <param name="Role">Tên vai trò tài khoản.</param>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, string Role);
