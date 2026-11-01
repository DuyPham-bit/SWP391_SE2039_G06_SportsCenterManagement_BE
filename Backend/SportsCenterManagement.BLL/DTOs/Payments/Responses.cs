using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Payments
{
public static class Responses
{
    public sealed record CashPaymentResponse(
        long PaymentId,
        long InvoiceId,
        decimal Amount,
        string Status,
        DateTime PaidAt);

    /// <summary>Kết quả phản hồi sau khi Backend xử lý kết quả thanh toán từ VNPay.</summary>
    public sealed class PaymentResultResponse
    {
        /// <summary>True nếu thanh toán thành công; false nếu thất bại hoặc bị hủy.</summary>
        public bool Success { get; set; }

        /// <summary>Thông điệp mô tả trạng thái giao dịch.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Mã hóa đơn nội bộ của hệ thống.</summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>Mã giao dịch do cổng VNPay sinh ra để đối soát.</summary>
        public string? TransactionId { get; set; }

        /// <summary>Số tiền thực tế của giao dịch (VNĐ).</summary>
        public decimal Amount { get; set; }
    }
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// DTO chứa kết quả sau khi giải mã và xác thực chữ ký số từ cổng VNPay.
/// </summary>
public class VnPayCallbackResult
{
    /// <summary>
    /// Chữ ký số từ VNPay có hợp lệ hay không (chống giả mạo dữ liệu).
    /// </summary>
    public bool IsValidSignature { get; set; }

    /// <summary>
    /// Giao dịch thành công hay không (Mã phản hồi vnp_ResponseCode == "00").
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Mã lỗi hoặc mã trạng thái từ VNPay (ví dụ: "00", "24", "97").
    /// </summary>
    public string ResponseCode { get; set; } = string.Empty;

    public string TransactionStatus { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public bool IsPending { get; set; }

    /// <summary>
    /// Mã hóa đơn nội bộ của hệ thống gửi sang VNPay (vnp_TxnRef).
    /// </summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch do cổng VNPay sinh ra (vnp_TransactionNo).
    /// </summary>
    public string TransactionNo { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế của giao dịch (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Mã ngân hàng mà người dùng đã chọn để thanh toán (vnp_BankCode).
    /// </summary>
    public string BankCode { get; set; } = string.Empty;
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
public sealed record ProviderRefundResult(
    bool IsAuthenticated,
    string Status,
    string? ProviderRefundId,
    string Message);
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// DTO chứa kết quả phản hồi sau khi Backend xử lý kết quả thanh toán từ VNPay.
/// </summary>
public class PaymentResultResponse
{
    /// <summary>
    /// Trạng thái thanh toán: true nếu thành công, false nếu thất bại/hủy.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>True if the signed provider notification was safely applied or already applied.</summary>
    public bool Processed { get; set; }

    public string? ProviderAckCode { get; set; }

    /// <summary>
    /// Thông điệp mô tả chi tiết trạng thái giao dịch.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã hóa đơn nội bộ của hệ thống (ví dụ: "SC-20261001-XXXX").
    /// </summary>
    public string? InvoiceNumber { get; set; }

    /// <summary>
    /// Mã giao dịch do cổng VNPay sinh ra (vnp_TransactionNo) để đối soát.
    /// </summary>
    public string? TransactionId { get; set; }

    /// <summary>
    /// Số tiền thực tế của giao dịch (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
public sealed record PaymentRefundResponse(
    long RefundId,
    long PaymentId,
    string InvoiceNumber,
    decimal Amount,
    string Status,
    string? ProviderRefundId,
    string? ExternalReference,
    string Message,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);

public sealed record ManualRefundRequestResponse(
    long Id,
    long PaymentId,
    string InvoiceNumber,
    long MemberId,
    string MemberName,
    string? MemberCode,
    decimal PaymentAmount,
    decimal Amount,
    string PaymentMethod,
    string Status,
    string Reason,
    string BankName,
    string BankAccountNumber,
    string BankAccountName,
    long RequestedBy,
    DateTime RequestedAtUtc,
    long? ReviewedBy,
    string? ReviewerName,
    DateTime? ReviewedAtUtc,
    string? ReviewNote,
    string? TransferProofUrl,
    string? TransferReference);

public sealed record RefundEligiblePaymentResponse(
    long PaymentId,
    string InvoiceNumber,
    decimal PaidAmount,
    decimal RefundableAmount,
    string PaymentMethod,
    DateTime? PaidAtUtc);
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// Cấu hình thông tin tích hợp Cổng thanh toán MoMo từ appsettings.json.
/// </summary>
public class MomoOption
{
    public const string SectionName = "Momo";

    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string PaymentUrl { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string NotifyUrl { get; set; } = string.Empty;
    public string RequestType { get; set; } = "captureWallet";
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// DTO hứng dữ liệu phản hồi từ máy chủ MoMo API v2 khi tạo giao dịch thanh toán.
/// </summary>
public class MomoCreatePaymentResponse
{
    [JsonPropertyName("partnerCode")]
    public string PartnerCode { get; set; } = string.Empty;

    [JsonPropertyName("requestId")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("responseTime")]
    public long ResponseTime { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("resultCode")]
    public int ResultCode { get; set; }

    /// <summary>
    /// Đường dẫn thanh toán trên trình duyệt (MoMo Gateway Web/QR).
    /// </summary>
    [JsonPropertyName("payUrl")]
    public string PayUrl { get; set; } = string.Empty;

    /// <summary>
    /// Link mở trực tiếp App MoMo trên điện thoại.
    /// </summary>
    [JsonPropertyName("deeplink")]
    public string Deeplink { get; set; } = string.Empty;

    /// <summary>
    /// Link mã QR code MoMo.
    /// </summary>
    [JsonPropertyName("qrCodeUrl")]
    public string QrCodeUrl { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// DTO chứa kết quả sau khi giải mã và xác thực chữ ký bảo mật từ MoMo Callback/IPN.
/// </summary>
public class MomoCallbackResult
{
    /// <summary>
    /// Chữ ký số từ MoMo có hợp lệ hay không (chống giả mạo dữ liệu).
    /// </summary>
    public bool IsValidSignature { get; set; }

    /// <summary>
    /// Giao dịch thành công hay không (ResultCode == 0).
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>Provider has not reached a final state; do not allow a duplicate checkout yet.</summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Mã trạng thái từ MoMo (0: Thành công, 9000: Đã xác nhận, 1006: User hủy...).
    /// </summary>
    public int ResultCode { get; set; }

    /// <summary>
    /// Thông điệp phản hồi từ MoMo.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã hóa đơn nội bộ hệ thống gửi sang (InvoiceNumber).
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Mã yêu cầu duy nhất cho giao dịch.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch thực tế trên hệ thống MoMo (transId).
    /// </summary>
    public string TransId { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế thanh toán (VNĐ).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Phương thức thanh toán (ví dụ: qr, momo_wallet, credit).
    /// </summary>
    public string PayType { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung mô tả đơn hàng.
    /// </summary>
    public string OrderInfo { get; set; } = string.Empty;
}
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
public sealed record InvoiceDetailsResponse(
    long Id,
    string InvoiceNumber,
    long MemberId,
    long CenterId,
    DateTime IssuedAtUtc,
    DateTime? PaidAtUtc,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal OutstandingBalance,
    IReadOnlyList<InvoiceLineResponse> Items,
    IReadOnlyList<InvoicePaymentResponse> Payments,
    string? MemberName = null,
    string? MemberCode = null,
    string? CenterName = null,
    string? CenterAddress = null);

public sealed record InvoiceLineResponse(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record InvoicePaymentResponse(
    long Id,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string? TransactionCode,
    string? ProviderTransactionId,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    IReadOnlyList<InvoiceRefundResponse> Refunds,
    string? GatewayReference = null);

public sealed record InvoiceRefundResponse(
    long Id,
    decimal Amount,
    string Status,
    string Reason,
    string? ProviderRefundId,
    string? ExternalReference,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
}

namespace SportsCenterManagement.BLL.DTOs.Payments
{
/// <summary>
/// DTO trả về kết quả thanh toán tại quầy, phục vụ Step 4 hiển thị và in biên lai.
/// </summary>
public class CounterPaymentResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Mã giao dịch định danh (Ví dụ: SC-20261002-XXXX). Khớp với receiptData.transactionRef
    /// </summary>
    public string TransactionRef { get; set; } = string.Empty;
    public string InvoiceNumber { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền thực tế của gói tập. Khớp với receiptData.amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Số tiền khách đã đưa.
    /// </summary>
    public decimal AmountReceived { get; set; }

    /// <summary>
    /// Số tiền thối lại cho khách.
    /// </summary>
    public decimal ChangeDue { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal OutstandingBalance { get; set; }

    /// <summary>
    /// Phương thức thanh toán. Khớp với receiptData.paymentMethod
    /// </summary>
    public string PaymentMethod { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm thanh toán thành công (UTC).
    /// </summary>
    public DateTime PaidAt { get; set; }

    /// <summary>
    /// Thông tin hội viên để hiển thị trên biên lai (receiptData.member)
    /// </summary>
    public MemberReceiptDto Member { get; set; } = new();

    /// <summary>
    /// Thông tin gói tập để hiển thị trên biên lai (receiptData.package)
    /// </summary>
    public PackageReceiptDto Package { get; set; } = new();
}

public class MemberReceiptDto
{
    public long Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public string PackageExpiry { get; set; } = string.Empty; // Chuỗi định dạng dd/MM/yyyy
}

public class PackageReceiptDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationDays { get; set; }
}

public record PendingInvoiceDto(
    string InvoiceNumber,
    long PackageId,
    string PackageName,
    decimal Amount,
    int DurationDays,
    DateTime IssuedAt,
    string? PaymentMethod
);
}
