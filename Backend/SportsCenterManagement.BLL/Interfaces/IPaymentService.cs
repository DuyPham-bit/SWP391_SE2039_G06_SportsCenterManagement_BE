using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>
/// Interface định nghĩa các nghiệp vụ xử lý thanh toán (VNPay, MoMo, VietQR...).
/// </summary>
public interface IPaymentService
{
    // ================= VNPay =================
    /// <summary>
    /// Tạo hóa đơn tạm và sinh ra đường dẫn (URL) chuyển hướng sang cổng VNPay.
    /// </summary>
    Task<string> CreatePaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xử lý kết quả phản hồi từ VNPay (Callback / IPN), xác thực signature và cập nhật trạng thái đơn hàng trong DB.
    /// </summary>
    Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);

    // ================= MoMo =================
    /// <summary>
    /// Tạo hóa đơn tạm và gọi API MoMo sinh ra URL thanh toán (hoặc QR code).
    /// </summary>
    Task<string> CreateMomoPaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Xử lý kết quả phản hồi từ MoMo (Callback / IPN), xác thực chữ ký và kích hoạt gói tập trong DB.
    /// </summary>
    Task<PaymentResultResponse> ProcessMomoCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);


    // ================= Counter (Tại quầy / Tiền mặt / POS) =================
    /// <summary>
    /// Xử lý thanh toán trực tiếp tại quầy (Tiền mặt / POS) do Lễ tân thực hiện.
    /// Kích hoạt gói tập ngay lập tức và trả về dữ liệu biên lai (receiptData).
    /// </summary>
    /// <param name="staffUserId">ID của Lễ tân / Thu ngân đang thực hiện giao dịch</param>
    /// <param name="request">Thông tin hội viên, gói tập và số tiền khách đưa</param>
    /// <param name="cancellationToken">Token hủy request</param>
    Task<CounterPaymentResponse> ProcessCounterPaymentAsync(
        long staffUserId,
        CounterPaymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Hủy giao dịch thanh toán do lễ tân bấm nhầm tại quầy (Void / Rollback gói tập).
    /// </summary>
    /// <param name="staffUserId">ID người thực hiện hủy</param>
    /// <param name="invoiceNumber">Mã giao dịch / Hóa đơn cần hủy</param>
    /// <param name="reason">Lý do giải trình hủy đơn</param>
    /// <param name="cancellationToken">Token hủy request</param>
    Task<PaymentResultResponse> VoidCounterPaymentAsync(
        long staffUserId,
        string invoiceNumber,
        string reason,
        CancellationToken cancellationToken = default);

}
