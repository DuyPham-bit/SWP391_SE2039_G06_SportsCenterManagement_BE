using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("class_enrollments")]
public class ClassEnrollment : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("registered_by")]
    public long? RegisteredBy { get; set; }

    [Column("registered_at")]
    public DateTime RegisteredAt { get; set; }

    [Column("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [MaxLength(500)]
    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

}
