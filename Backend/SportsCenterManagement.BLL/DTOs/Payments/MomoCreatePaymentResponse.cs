using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Payments;

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
