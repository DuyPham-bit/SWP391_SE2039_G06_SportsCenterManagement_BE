using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("membership_packages")]
public class MembershipPackage : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("duration_days")]
    public int DurationDays { get; set; }

    [Precision(12, 2)]
    [Column("price")]
    public decimal Price { get; set; }

    [Column("max_classes")]
    public int? MaxClasses { get; set; }

    [MaxLength(50)]
    [Column("access_type")]
    public string? AccessType { get; set; }

    [Column("allowed_sports")]
    public int AllowedSports { get; set; } = 1;

    [MaxLength(50)]
    [Column("badge")]
    public string? Badge { get; set; }

    [Column("features", TypeName = "nvarchar(max)")]
    public string? Features { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
