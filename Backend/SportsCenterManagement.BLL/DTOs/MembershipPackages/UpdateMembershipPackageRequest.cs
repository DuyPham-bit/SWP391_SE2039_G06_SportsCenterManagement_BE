using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

/// <summary>
/// Yêu cầu cập nhật thông tin một gói tập thể thao.
/// </summary>
/// <param name="Name">Tên gói tập mới.</param>
/// <param name="Description">Mô tả cập nhật của gói tập.</param>
/// <param name="DurationDays">Thời hạn sử dụng tính theo số ngày (1 - 3650 ngày).</param>
/// <param name="Price">Giá bán mới của gói tập (VNĐ).</param>
/// <param name="MaxClasses">Số buổi hoặc lớp tối đa cho phép.</param>
/// <param name="AccessType">Loại hình truy cập.</param>
/// <param name="Status">Trạng thái gói tập (Draft, Active, hoặc Inactive).</param>
public sealed record UpdateMembershipPackageRequest(
    [property: Required, StringLength(150, MinimumLength = 1)] string Name,
    [property: StringLength(500)] string? Description,
    [property: Range(1, 3650)] int DurationDays,
    [property: Range(typeof(decimal), "0.01", "9999999999")] decimal Price,
    [property: Range(1, 10000)] int? MaxClasses,
    [property: StringLength(50)] string? AccessType,
    [property: Required, RegularExpression("^(Draft|Active|Inactive)$")] string Status);
