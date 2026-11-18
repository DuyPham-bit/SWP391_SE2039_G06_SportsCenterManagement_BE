using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("invoices")]
public class Invoice : Common.BaseEntity
{
    [MaxLength(50)]
    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = null!;

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("created_by")]
    public long? CreatedBy { get; set; }

    [Precision(12, 2)]
    [Column("subtotal")]
    public decimal Subtotal { get; set; }

    [Precision(12, 2)]
    [Column("discount")]
    public decimal Discount { get; set; }

    [Precision(12, 2)]
    [Column("tax")]
    public decimal Tax { get; set; }

    [Precision(12, 2)]
    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("issued_at")]
    public DateTime IssuedAt { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [MaxLength(100)]
    [Column("idempotency_key")]
    public string? IdempotencyKey { get; set; }

}
