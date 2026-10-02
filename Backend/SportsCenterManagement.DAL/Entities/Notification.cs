using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("notifications")]
public class Notification : Common.BaseEntity
{
    [Column("sender_id")]
    public long SenderId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("message")]
    public string Message { get; set; } = null!;

    [MaxLength(50)]
    [Column("notification_type")]
    public string NotificationType { get; set; } = null!;

    [MaxLength(150)]
    [Column("deduplication_key")]
    public string? DeduplicationKey { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

}
