using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("exercises")]
public class Exercise : Common.BaseEntity
{
    [Column("sport_id")]
    public long? SportId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(100)]
    [Column("muscle_group")]
    public string? MuscleGroup { get; set; }

    [MaxLength(50)]
    [Column("difficulty_level")]
    public string? DifficultyLevel { get; set; }

    [Column("instructions")]
    public string? Instructions { get; set; }

    [MaxLength(500)]
    [Column("video_url")]
    public string? VideoUrl { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
