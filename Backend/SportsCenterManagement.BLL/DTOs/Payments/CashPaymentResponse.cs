namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// Kết quả phản hồi sau khi ghi nhận thanh toán tiền mặt tại quầy.
/// </summary>
/// <param name="PaymentId">Mã định danh bản ghi thanh toán (ID bảng payments).</param>
/// <param name="InvoiceId">Mã hóa đơn được thanh toán.</param>
/// <param name="Amount">Số tiền đã thanh toán (VNĐ).</param>
/// <param name="Status">Trạng thái thanh toán (Paid, Pending, Failed,...).</param>
/// <param name="PaidAt">Thời điểm ghi nhận thanh toán.</param>
public sealed record CashPaymentResponse(
    long PaymentId,
    long InvoiceId,
    decimal Amount,
    string Status,
    DateTime PaidAt);
