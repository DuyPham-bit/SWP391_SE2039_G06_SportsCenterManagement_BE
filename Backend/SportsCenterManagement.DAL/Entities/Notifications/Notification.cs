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

    [MaxLength(1000)]
    [Column("target_class_ids")]
    public string? TargetClassIds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("scheduled_at")]
    public DateTime? ScheduledAt { get; set; }

    [MaxLength(100)]
    [Column("reminder_key")]
    public string? ReminderKey { get; set; }

}
