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
    [Column("status")]
    public string Status { get; set; } = null!;
}
