using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("ai_exercise_recommendations")]
public class AiExerciseRecommendation : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("conversation_id")]
    public long? ConversationId { get; set; }

    [Column("exercise_id")]
    public long? ExerciseId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("requested_by")]
    public long? RequestedBy { get; set; }

    [Column("recommendation_reason")]
    public string? RecommendationReason { get; set; }

    [MaxLength(500)]
    [Column("target_goal")]
    public string? TargetGoal { get; set; }

    [MaxLength(50)]
    [Column("difficulty_level")]
    public string? DifficultyLevel { get; set; }

    [MaxLength(100)]
    [Column("ai_model")]
    public string? AiModel { get; set; }

    [Precision(5, 4)]
    [Column("confidence_score")]
    public decimal? ConfidenceScore { get; set; }

    [Column("accepted")]
    public bool? Accepted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
