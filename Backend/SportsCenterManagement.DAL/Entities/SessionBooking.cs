using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("session_bookings")]
public class SessionBooking : Common.BaseEntity
{
    [Column("session_id")]
    public long SessionId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("subscription_id")]
    public long? SubscriptionId { get; set; }

    [Column("booked_by")]
    public long? BookedBy { get; set; }

    [Column("enrollment_id")]
    public long? EnrollmentId { get; set; }

    [Column("booked_at")]
    public DateTime BookedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
