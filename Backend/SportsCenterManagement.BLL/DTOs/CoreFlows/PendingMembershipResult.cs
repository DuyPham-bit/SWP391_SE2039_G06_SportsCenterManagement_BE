namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

/// <summary>
/// Kết quả tạo đơn đăng ký gói tập ở trạng thái chờ thanh toán (Pending).
/// </summary>
/// <param name="SubscriptionId">Mã đăng ký gói tập của hội viên.</param>
/// <param name="InvoiceId">Mã hóa đơn gắn với gói tập.</param>
/// <param name="InvoiceNumber">Số hóa đơn nội bộ sinh ra bởi hệ thống.</param>
/// <param name="Amount">Số tiền cần thanh toán cho gói tập (VNĐ).</param>
public sealed record PendingMembershipResult(
    long SubscriptionId,
    long InvoiceId,
    string InvoiceNumber,
    decimal Amount);
