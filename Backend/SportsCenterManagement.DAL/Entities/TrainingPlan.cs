using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("training_plans")]
public class TrainingPlan : Common.BaseEntity
{
    [Column("member_id")]
    public long? MemberId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(1000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(500)]
    [Column("goal")]
    public string? Goal { get; set; }

    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [MaxLength(30)]
    [Column("plan_type")]
    public string PlanType { get; set; } = null!;

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
