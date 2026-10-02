namespace SportsCenterManagement.BLL.DTOs.MembershipPackages;

/// <summary>
/// Thông tin chi tiết một gói tập thể thao của trung tâm.
/// </summary>
/// <param name="Id">Mã định danh gói tập.</param>
/// <param name="CenterId">Mã trung tâm thể thao phát hành gói.</param>
/// <param name="Name">Tên gói tập.</param>
/// <param name="Description">Mô tả chi tiết quyền lợi gói.</param>
/// <param name="DurationDays">Thời hạn sử dụng tính theo số ngày.</param>
/// <param name="Price">Giá bán gói tập (VNĐ).</param>
/// <param name="MaxClasses">Số buổi hoặc lớp tối đa cho phép.</param>
/// <param name="AccessType">Loại hình truy cập.</param>
/// <param name="Status">Trạng thái gói tập (Draft, Active, Inactive).</param>
public sealed record MembershipPackageResponse(
    long Id,
    long CenterId,
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    int? MaxClasses,
    string? AccessType,
    string Status);
