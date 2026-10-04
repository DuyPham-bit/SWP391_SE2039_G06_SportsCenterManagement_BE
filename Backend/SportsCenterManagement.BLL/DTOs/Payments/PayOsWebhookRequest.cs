using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Payments;

/// <summary>
/// DTO tiếp nhận dữ liệu Webhook từ PayOS khi có giao dịch chuyển khoản thành công.
/// </summary>
public class PayOsWebhookRequest
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PayOsWebhookData? Data { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public class PayOsWebhookData
{
    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("accountNumber")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("transactionDateTime")]
    public string TransactionDateTime { get; set; } = string.Empty;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "VND";

    [JsonPropertyName("paymentLinkId")]
    public string PaymentLinkId { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;
}
