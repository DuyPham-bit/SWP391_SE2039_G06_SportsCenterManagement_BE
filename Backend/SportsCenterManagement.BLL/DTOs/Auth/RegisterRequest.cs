using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Auth;

/// <summary>
/// Yêu cầu đăng ký tài khoản hội viên mới.
/// </summary>
/// <param name="Username">Tên tài khoản (tối thiểu 3 ký tự).</param>
/// <param name="Email">Địa chỉ thư điện tử hợp lệ.</param>
/// <param name="Password">Mật khẩu bảo mật (tối thiểu 12 ký tự).</param>
/// <param name="FullName">Họ và tên đầy đủ của hội viên.</param>
/// <param name="Phone">Số điện thoại liên lạc.</param>
/// <param name="DateOfBirth">Ngày tháng năm sinh.</param>
/// <param name="CenterId">Mã trung tâm trực thuộc đăng ký (tùy chọn).</param>
public sealed record RegisterRequest(
    [property: Required, StringLength(100, MinimumLength = 3)] string Username,
    [property: Required, StringLength(150)] string Email,
    [property: Required, StringLength(150, MinimumLength = 12)] string Password,
    [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [property: StringLength(20)] string? Phone,
    DateOnly? DateOfBirth,
    [property: Range(1, long.MaxValue)] long? CenterId = null);
