using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface kết nối kỹ thuật với Cổng VietQR / PayOS:
/// - Tạo link thanh toán động kèm mã QR Napas 247.
/// - Xác thực signature số Webhook từ PayOS.
/// </summary>
public interface IPayOsService
{
    /// <summary>
    /// Gửi yêu cầu sang PayOS để sinh mã QR và link thanh toán.
    /// </summary>
    /// <param name="orderCode">Mã đơn hàng số nguyên duy nhất (VD: Timestamp)</param>
    /// <param name="amount">Số tiền thanh toán (VNĐ)</param>
    /// <param name="description">Nội dung chuyển khoản (tối đa 25 ký tự, không dấu)</param>
    /// <param name="itemName">Tên gói tập</param>
    /// <param name="cancellationToken">Token hủy request</param>
    Task<PayOsCreatePaymentResponse> CreatePaymentLinkAsync(
        long orderCode,
        decimal amount,
        string description,
        string itemName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực signature số Webhook nhận được từ PayOS xem có hợp lệ không.
    /// </summary>
    bool VerifyWebhookSignature(PayOsWebhookRequest webhookRequest);
}
