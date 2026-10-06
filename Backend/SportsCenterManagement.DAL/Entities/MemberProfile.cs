using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("member_profiles")]
public class MemberProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("center_id")]
    public long? CenterId { get; set; }
    [MaxLength(50)]
    [Column("member_code")]
    public string MemberCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [Column("date_of_birth")]
    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(20)]
    [Column("gender")]
    public string? Gender { get; set; }

    [MaxLength(255)]
    [Column("address")]
    public string? Address { get; set; }

    [MaxLength(150)]
    [Column("emergency_contact_name")]
    public string? EmergencyContactName { get; set; }

    [MaxLength(20)]
    [Column("emergency_contact_phone")]
    public string? EmergencyContactPhone { get; set; }

    [MaxLength(500)]
    [Column("fitness_goal")]
    public string? FitnessGoal { get; set; }

    [MaxLength(50)]
    [Column("fitness_level")]
    public string? FitnessLevel { get; set; }

    [MaxLength(1000)]
    [Column("medical_note")]
    public string? MedicalNote { get; set; }

    [Precision(5, 2)]
    [Column("height_cm")]
    public decimal? HeightCm { get; set; }

    [Precision(5, 2)]
    [Column("weight_kg")]
    public decimal? WeightKg { get; set; }

    [Column("booking_suspended_until")]
    public DateTime? BookingSuspendedUntil { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
