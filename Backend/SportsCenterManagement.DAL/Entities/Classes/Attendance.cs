using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("attendance")]
public class Attendance : Common.BaseEntity
{
    [Column("session_id")]
    public long SessionId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("checked_in_by")]
    public long? CheckedInBy { get; set; }

    [MaxLength(30)]
    [Column("attendance_status")]
    public string AttendanceStatus { get; set; } = null!;

    [Column("check_in_time")]
    public DateTime? CheckInTime { get; set; }

    [Column("check_out_time")]
    public DateTime? CheckOutTime { get; set; }

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
