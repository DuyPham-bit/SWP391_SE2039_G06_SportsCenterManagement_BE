using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("training_plan_exercises")]
public class TrainingPlanExercise : Common.BaseEntity
{
    [Column("training_plan_id")]
    public long TrainingPlanId { get; set; }

    [Column("exercise_id")]
    public long ExerciseId { get; set; }

    [Column("day_number")]
    public int? DayNumber { get; set; }

    [Column("sets")]
    public int? Sets { get; set; }

    [Column("repetitions")]
    public int? Repetitions { get; set; }

    [Column("duration_seconds")]
    public int? DurationSeconds { get; set; }

    [Column("rest_seconds")]
    public int? RestSeconds { get; set; }

    [Precision(8, 2)]
    [Column("target_weight")]
    public decimal? TargetWeight { get; set; }

    [MaxLength(1000)]
    [Column("notes")]
    public string? Notes { get; set; }

    [Column("sort_order")]
    public int? SortOrder { get; set; }

}
