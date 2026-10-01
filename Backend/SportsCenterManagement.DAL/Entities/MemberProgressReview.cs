using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("member_progress_reviews")]
public class MemberProgressReview : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("review_date")]
    public DateOnly ReviewDate { get; set; }

    [Precision(5, 2)]
    [Column("progress_score")]
    public decimal? ProgressScore { get; set; }

    [Precision(8, 2)]
    [Column("weight")]
    public decimal? Weight { get; set; }

    [Precision(5, 2)]
    [Column("body_fat_percentage")]
    public decimal? BodyFatPercentage { get; set; }

    [MaxLength(2000)]
    [Column("review_note")]
    public string? ReviewNote { get; set; }

    [MaxLength(1000)]
    [Column("next_goal")]
    public string? NextGoal { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
