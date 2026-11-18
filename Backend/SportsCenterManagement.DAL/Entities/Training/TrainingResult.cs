using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("training_results")]
public class TrainingResult : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Precision(5, 2)]
    [Column("body_fat_percentage")]
    public decimal? BodyFatPercentage { get; set; }

    [Precision(8, 2)]
    [Column("muscle_mass")]
    public decimal? MuscleMass { get; set; }

    [MaxLength(1000)]
    [Column("workout_result")]
    public string? WorkoutResult { get; set; }

    [MaxLength(1000)]
    [Column("coach_feedback")]
    public string? CoachFeedback { get; set; }

    [Column("session_id")]
    public long? SessionId { get; set; }

    [Column("training_plan_exercise_id")]
    public long? TrainingPlanExerciseId { get; set; }

    [Column("exercise_id")]
    public long? ExerciseId { get; set; }

    [Column("training_plan_id")]
    public long? TrainingPlanId { get; set; }

    [Column("result_date")]
    public DateOnly ResultDate { get; set; }

    [Column("sets_completed")]
    public int? SetsCompleted { get; set; }

    [Column("repetitions_completed")]
    public int? RepetitionsCompleted { get; set; }

    [Precision(8, 2)]
    [Column("weight")]
    public decimal? Weight { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Precision(8, 2)]
    [Column("calories_burned")]
    public decimal? CaloriesBurned { get; set; }

    [Precision(5, 2)]
    [Column("performance_score")]
    public decimal? PerformanceScore { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
