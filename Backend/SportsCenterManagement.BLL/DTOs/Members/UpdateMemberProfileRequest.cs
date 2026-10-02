using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Members;

/// <summary>
/// Yêu cầu cập nhật thông tin hồ sơ Hội viên.
/// </summary>
/// <param name="FullName">Họ và tên mới của hội viên.</param>
/// <param name="Phone">Số điện thoại liên lạc mới.</param>
/// <param name="DateOfBirth">Ngày tháng năm sinh.</param>
/// <param name="Gender">Giới tính (Nam, Nữ, Khác).</param>
/// <param name="Address">Địa chỉ nơi ở.</param>
public sealed record UpdateMemberProfileRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string FullName,
    [property: StringLength(20)] string? Phone,
    DateOnly? DateOfBirth,
    [property: StringLength(20)] string? Gender,
    [property: StringLength(255)] string? Address);
