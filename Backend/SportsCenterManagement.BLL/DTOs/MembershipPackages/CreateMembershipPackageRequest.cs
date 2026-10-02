using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

/// <summary>
/// Yêu cầu tạo mới một gói tập thể thao tại trung tâm.
/// </summary>
/// <param name="Name">Tên gói tập (ví dụ: "Gói Gym 1 Tháng").</param>
/// <param name="Description">Mô tả chi tiết quyền lợi gói tập.</param>
/// <param name="DurationDays">Thời hạn sử dụng tính theo số ngày (1 - 3650 ngày).</param>
/// <param name="Price">Giá bán của gói tập (VNĐ).</param>
/// <param name="MaxClasses">Số buổi tập hoặc lớp học tối đa được tham gia (nếu có giới hạn).</param>
/// <param name="AccessType">Loại hình quyền truy cập (All, Standard, VIP,...).</param>
/// <param name="Status">Trạng thái phát hành (Draft hoặc Active).</param>
public sealed record CreateMembershipPackageRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: StringLength(500)] string? Description,
    [property: Range(1, 3650)] int DurationDays,
    [property: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
    [property: Range(1, 10000)] int? MaxClasses,
    [property: StringLength(50)] string? AccessType,
    [property: Required, RegularExpression("^(Draft|Active)$")] string Status);
