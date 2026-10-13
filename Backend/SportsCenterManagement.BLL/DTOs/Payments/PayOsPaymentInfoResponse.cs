using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed class PayOsPaymentInfoResponse
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PayOsPaymentInfo? Data { get; set; }

    [JsonPropertyName("signature")]
    public string Signature { get; set; } = string.Empty;
}

public sealed class PayOsPaymentInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("orderCode")]
    public long OrderCode { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }

    [JsonPropertyName("amountRemaining")]
    public decimal AmountRemaining { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("transactions")]
    public List<PayOsPaymentTransaction> Transactions { get; set; } = new();
}

public sealed class PayOsPaymentTransaction
{
    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
