using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("class_sessions")]
public class ClassSession : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("schedule_id")]
    public long? ScheduleId { get; set; }

    [Column("room_id")]
    public long? RoomId { get; set; }

    [Column("coach_id")]
    public long? CoachId { get; set; }

    [Column("session_date")]
    public DateOnly SessionDate { get; set; }

    [Column("start_time")]
    public TimeOnly StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly EndTime { get; set; }

    [MaxLength(30)]
    [Column("session_status")]
    public string SessionStatus { get; set; } = null!;

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
