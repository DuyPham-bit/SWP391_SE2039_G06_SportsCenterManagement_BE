using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface chuyên trách kết nối kỹ thuật với cổng thanh toán MoMo:
/// - Đóng gói dữ liệu thanh toán theo chuẩn MoMo API v2 (All-in-One).
/// - Băm chữ ký bảo mật HMAC-SHA256 chống làm giả dữ liệu.
/// - Gửi HTTP Request sang máy chủ MoMo Sandbox để lấy URL thanh toán/QR Code.
/// - Xác thực chữ ký số từ phản hồi Callback / IPN của MoMo.
/// </summary>
public interface IMoMoService
{
    /// <summary>
    /// Gửi yêu cầu sang cổng MoMo để khởi tạo link thanh toán (payUrl).
    /// </summary>
    /// <param name="orderId">Mã hóa đơn duy nhất của hệ thống (InvoiceNumber)</param>
    /// <param name="amount">Số tiền thanh toán (VNĐ)</param>
    /// <param name="orderInfo">Nội dung mô tả giao dịch</param>
    /// <param name="cancellationToken">Token hủy request</param>
    /// <returns>Đối tượng phản hồi từ MoMo gồm link thanh toán payUrl, qrCodeUrl</returns>
    Task<MomoCreatePaymentResponse> CreatePaymentUrlAsync(
        string orderId,
        decimal amount,
        string orderInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phân tích dữ liệu phản hồi từ MoMo khi người dùng thanh toán xong và kiểm tra chữ ký số.
    /// </summary>
    /// <param name="queryParams">Tập hợp các tham số MoMo gửi về qua URL hoặc Form</param>
    /// <returns>Kết quả giải mã đã qua kiểm tra chữ ký số</returns>
    MomoCallbackResult ProcessCallback(IDictionary<string, string> queryParams);

    Task<ProviderRefundResult> RefundAsync(
        string requestId,
        string refundOrderId,
        string providerTransactionId,
        decimal amount,
        string reason,
        CancellationToken cancellationToken = default);
}
