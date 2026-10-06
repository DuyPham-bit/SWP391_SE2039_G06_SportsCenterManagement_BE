using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("classes")]
public class ClassEntity : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [Column("sport_id")]
    public long SportId { get; set; }

    [Column("room_id")]
    public long? RoomId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Column("level")]
    public string? Level { get; set; }

    [Column("capacity")]
    public int Capacity { get; set; }

    [Column("allow_waitlist")]
    public bool AllowWaitlist { get; set; } = true;

    [Column("duration_minutes")]
    public int DurationMinutes { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
