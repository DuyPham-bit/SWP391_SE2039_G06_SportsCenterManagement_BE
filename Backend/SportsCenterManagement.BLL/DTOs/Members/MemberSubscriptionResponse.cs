namespace SportsCenterManagement.BLL.DTOs.Members;

/// <summary>
/// Thông tin chi tiết gói tập đã đăng ký của Hội viên.
/// </summary>
/// <param name="SubscriptionId">Mã định danh lượt đăng ký gói (ID bảng member_subscriptions).</param>
/// <param name="PackageId">Mã định danh gói tập gốc.</param>
/// <param name="PackageName">Tên gói tập thể thao.</param>
/// <param name="Price">Giá tiền gói tập tại thời điểm đăng ký.</param>
/// <param name="DurationDays">Thời hạn sử dụng tính theo số ngày.</param>
/// <param name="StartDate">Ngày bắt đầu có hiệu lực.</param>
/// <param name="EndDate">Ngày hết hạn gói tập.</param>
/// <param name="Status">Trạng thái gói (Pending, Active, Expired, Cancelled).</param>
/// <param name="CreatedAt">Thời điểm tạo đơn đăng ký.</param>
public sealed record MemberSubscriptionResponse(
    long SubscriptionId,
    long PackageId,
    string PackageName,
    decimal Price,
    int DurationDays,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    DateTime CreatedAt);
