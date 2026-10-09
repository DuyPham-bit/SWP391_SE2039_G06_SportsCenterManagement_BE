using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SportsCenterManagement.BLL.DTOs.Payments
{
    /// <summary>DTO tiếp nhận yêu cầu thanh toán tại quầy từ Frontend Lễ tân.</summary>
    public class CounterPaymentRequest
    {
        [Range(1, long.MaxValue, ErrorMessage = "Hội viên không hợp lệ.")]
        public long MemberId { get; set; }

        [Range(1, long.MaxValue, ErrorMessage = "Gói tập không hợp lệ.")]
        public long PackageId { get; set; }

        [Required(ErrorMessage = "Phương thức thanh toán là bắt buộc.")]
        [RegularExpression("^(?i:CASH|POS)$")]
        public string PaymentMethod { get; set; } = "CASH";

        [Range(0, 1_000_000_000, ErrorMessage = "Số tiền không hợp lệ.")]
        public decimal AmountReceived { get; set; }

        [MaxLength(100)]
        public string? PosApprovalCode { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public bool CancelPendingIfAny { get; set; }
    }

    public class CreatePaymentRequest
    {
        [Range(0, long.MaxValue)]
        public long PackageId { get; set; }

        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        [MaxLength(50)]
        public string? BankCode { get; set; }

        public bool CancelPendingIfAny { get; set; }
    }

    public class CancelPendingRequest
    {
        [MaxLength(50)]
        public string? InvoiceNumber { get; set; }

        public long? PackageId { get; set; }

        public long? MemberId { get; set; }
    }

    public sealed class CreateRefundRequest
    {
        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal Amount { get; set; }

        [Required, MinLength(5), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? ExternalRefundReference { get; set; }
    }

    /// <summary>DTO tiếp nhận dữ liệu Webhook từ PayOS khi có giao dịch chuyển khoản thành công.</summary>
    public class PayOsWebhookRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("desc")]
        public string Desc { get; set; } = string.Empty;

        [JsonPropertyName("success")]
        public bool Success { get; set; }

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
        public int Amount { get; set; }

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

        [JsonPropertyName("counterAccountBankId")]
        public string CounterAccountBankId { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountBankName")]
        public string CounterAccountBankName { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountName")]
        public string CounterAccountName { get; set; } = string.Empty;

        [JsonPropertyName("counterAccountNumber")]
        public string CounterAccountNumber { get; set; } = string.Empty;

        [JsonPropertyName("virtualAccountName")]
        public string VirtualAccountName { get; set; } = string.Empty;

        [JsonPropertyName("virtualAccountNumber")]
        public string VirtualAccountNumber { get; set; } = string.Empty;
    }

    public sealed class ReconcilePendingPaymentRequest
    {
        [Required, RegularExpression("^(Succeeded|Failed)$")]
        public string Status { get; set; } = string.Empty;

        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal? ConfirmedAmount { get; set; }

        [MaxLength(150)]
        public string? ProviderTransactionId { get; set; }

        [Required, MinLength(10), MaxLength(500)]
        public string EvidenceNote { get; set; } = string.Empty;
    }

    public sealed class ReconcileRefundRequest
    {
        [Required, RegularExpression("^(Succeeded|Failed)$")]
        public string Status { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? ProviderRefundId { get; set; }

        [Required, MinLength(10), MaxLength(500)]
        public string EvidenceNote { get; set; } = string.Empty;
    }

    public sealed class RecordInvoicePaymentRequest
    {
        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal Amount { get; set; }

        [Required, RegularExpression("^(?i:CASH|POS)$")]
        public string PaymentMethod { get; set; } = "CASH";

        [MaxLength(100)]
        public string? PosApprovalCode { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }
    }

    /// <summary>DTO tiếp nhận yêu cầu hủy giao dịch nhầm tại quầy.</summary>
    public class VoidPaymentRequest
    {
        [Required(ErrorMessage = "Vui lòng nhập lý do hủy giao dịch.")]
        [MinLength(5, ErrorMessage = "Lý do hủy giao dịch phải từ 5 ký tự trở lên.")]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public static class Requests
    {
        /// <summary>Thông tin Frontend gửi lên khi Member chọn thanh toán một gói tập.</summary>
        public sealed class CreatePaymentRequest
        {
            [Range(1, long.MaxValue)]
            public long PackageId { get; set; }

            [StringLength(20)]
            public string? BankCode { get; set; }
        }

        public sealed record RecordCashPaymentRequest(
            [param: Range(typeof(decimal), "0.01", "9999999999.99")] decimal Amount,
            [param: Required, StringLength(100, MinimumLength = 16)] string IdempotencyKey);
    }
}
