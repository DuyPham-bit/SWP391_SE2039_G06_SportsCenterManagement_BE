using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed class RecordInvoicePaymentRequest
{
    [Range(typeof(decimal), "0.01", "1000000000")]
    public decimal Amount { get; set; }

    [Required]
    [RegularExpression("^(?i:CASH|POS)$")]
    public string PaymentMethod { get; set; } = "CASH";

    [MaxLength(100)]
    public string? PosApprovalCode { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
