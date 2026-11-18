using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("class_waitlist")]
public class ClassWaitlist : Common.BaseEntity
{
    [Column("session_id")]
    public long? SessionId { get; set; }

    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("registered_by")]
    public long? RegisteredBy { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
