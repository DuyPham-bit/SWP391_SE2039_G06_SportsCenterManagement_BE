using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("payment_refunds")]
public class PaymentRefund : Common.BaseEntity
{
    [Column("payment_id")]
    public long PaymentId { get; set; }

    [MaxLength(100)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [MaxLength(500)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("requested_by")]
    public long? RequestedBy { get; set; }

    [Column("approved_by")]
    public long? ApprovedBy { get; set; }

    [MaxLength(150)]
    [Column("transaction_code")]
    public string? TransactionCode { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
}
