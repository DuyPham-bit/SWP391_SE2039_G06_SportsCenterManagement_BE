namespace SportsCenterManagement.BLL.DTOs.Members;

/// <summary>
/// Thông tin chi tiết hồ sơ cá nhân của Hội viên.
/// </summary>
/// <param name="MemberId">Mã định danh hồ sơ hội viên (ID bảng member_profiles).</param>
/// <param name="UserId">Mã định danh tài khoản người dùng (ID bảng users).</param>
/// <param name="MemberCode">Mã số thẻ hội viên (ví dụ: "MB00001").</param>
/// <param name="Username">Tên tài khoản người dùng.</param>
/// <param name="Email">Địa chỉ email liên hệ.</param>
/// <param name="Phone">Số điện thoại liên lạc.</param>
/// <param name="FullName">Họ và tên đầy đủ của hội viên.</param>
/// <param name="DateOfBirth">Ngày tháng năm sinh.</param>
/// <param name="Gender">Giới tính (Nam, Nữ, Khác).</param>
/// <param name="Address">Địa chỉ cư trú.</param>
/// <param name="CreatedAt">Thời gian khởi tạo hồ sơ.</param>
public sealed record MemberProfileResponse(
    long MemberId,
    long UserId,
    string MemberCode,
    string Username,
    string Email,
    string? Phone,
    string FullName,
    DateOnly? DateOfBirth,
    string? Gender,
    string? Address,
    DateTime CreatedAt);
