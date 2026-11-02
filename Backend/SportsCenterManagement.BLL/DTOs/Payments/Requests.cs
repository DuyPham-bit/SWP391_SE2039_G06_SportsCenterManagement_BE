using System.ComponentModel.DataAnnotations;
using System.Text.Json;
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

    public sealed class CounterOnlinePaymentRequest : CreatePaymentRequest
    {
        [Range(1, long.MaxValue, ErrorMessage = "Hội viên không hợp lệ.")]
        public long MemberId { get; set; }
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

    public sealed class SubmitManualRefundRequest
    {
        [Range(typeof(decimal), "0.01", "1000000000")]
        public decimal Amount { get; set; }

        [Required, MinLength(5), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [Required, MinLength(2), MaxLength(100)]
        public string BankName { get; set; } = string.Empty;

        [Required, RegularExpression("^[0-9]{6,30}$")]
        public string BankAccountNumber { get; set; } = string.Empty;

        [Required, MinLength(2), MaxLength(150)]
        public string BankAccountName { get; set; } = string.Empty;
    }

    public sealed class ReviewManualRefundRequest
    {
        [Required, RegularExpression("^(Approved|Rejected)$")]
        public string Status { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? TransferProofUrl { get; set; }

        [MaxLength(150)]
        public string? TransferReference { get; set; }

        [MaxLength(1000)]
        public string? ReviewNote { get; set; }
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
