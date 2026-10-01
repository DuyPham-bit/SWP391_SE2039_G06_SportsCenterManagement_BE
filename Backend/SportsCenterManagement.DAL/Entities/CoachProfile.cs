using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("coach_profiles")]
public class CoachProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(50)]
    [Column("coach_code")]
    public string CoachCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [MaxLength(255)]
    [Column("specialization")]
    public string? Specialization { get; set; }

    [MaxLength(500)]
    [Column("certification")]
    public string? Certification { get; set; }

    [Column("experience_years")]
    public int? ExperienceYears { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("hire_date")]
    public DateOnly? HireDate { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
