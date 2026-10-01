using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("invoice_items")]
public class InvoiceItem : Common.BaseEntity
{
    [Column("invoice_id")]
    public long InvoiceId { get; set; }

    [Column("package_id")]
    public long? PackageId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("enrollment_id")]
    public long? EnrollmentId { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string Description { get; set; } = null!;

    [Column("quantity")]
    public int Quantity { get; set; }

    [Precision(12, 2)]
    [Column("unit_price")]
    public decimal UnitPrice { get; set; }

    [Precision(12, 2)]
    [Column("amount")]
    public decimal Amount { get; set; }

}
