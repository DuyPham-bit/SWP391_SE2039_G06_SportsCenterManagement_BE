using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("coach_leaves")]
public sealed class CoachLeave : BaseEntity
{
    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("start_at")]
    public DateTime StartAt { get; set; }

    [Column("end_at")]
    public DateTime EndAt { get; set; }

    [MaxLength(30)]
    [Column("leave_type")]
    public string LeaveType { get; set; } = "AnnualLeave";

    [MaxLength(1000)]
    [Column("reason")]
    public string? Reason { get; set; }

    [Column("swap_coach_id")]
    public long? SwapCoachId { get; set; }

    [MaxLength(1000)]
    [Column("manager_note")]
    public string? ManagerNote { get; set; }

    [Column("reviewed_by")]
    public long? ReviewedBy { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; }

    [Column("reviewed_at")]
    public DateTime? ReviewedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;
}
