using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("payment_refunds")]
public sealed class PaymentRefund : BaseEntity
{
    [Column("payment_id")]
    public long PaymentId { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = "Pending";

    [MaxLength(100)]
    [Column("idempotency_key")]
    public string IdempotencyKey { get; set; } = null!;

    [MaxLength(150)]
    [Column("provider_refund_id")]
    public string? ProviderRefundId { get; set; }

    [MaxLength(150)]
    [Column("external_reference")]
    public string? ExternalReference { get; set; }

    [MaxLength(500)]
    [Column("reason")]
    public string Reason { get; set; } = null!;

    [Column("requested_by")]
    public long RequestedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }
}
