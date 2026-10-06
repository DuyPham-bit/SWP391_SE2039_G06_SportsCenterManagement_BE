using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("payments")]
public class Payment : Common.BaseEntity
{
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("processed_by")]
    public long? ProcessedBy { get; set; }

    [MaxLength(50)]
    [Column("payment_method")]
    public string PaymentMethod { get; set; } = null!;

    [MaxLength(150)]
    [Column("transaction_code")]
    public string? TransactionCode { get; set; }

    [MaxLength(100)]
    [Column("gateway_reference")]
    public string? GatewayReference { get; set; }

    [MaxLength(150)]
    [Column("provider_transaction_id")]
    public string? ProviderTransactionId { get; set; }

    [MaxLength(100)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }

    [MaxLength(2048)]
    [Column("gateway_payment_url")]
    public string? GatewayPaymentUrl { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

    [Precision(12, 2)]
    [Column("amount_received")]
    public decimal? AmountReceived { get; set; }

    [MaxLength(30)]
    [Column("payment_status")]
    public string PaymentStatus { get; set; } = null!;

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("refund_approved_by")]
    public long? RefundApprovedBy { get; set; }

}
