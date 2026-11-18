using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Training;

public sealed record PlanExercise(
    [param: Required, StringLength(150)] string Name,
    [param: StringLength(30)] string? Sets,
    [param: StringLength(50)] string? Reps,
    [param: StringLength(500)] string? Note,
    [param: Range(1, 52)] int? WeekNumber = null,
    [param: StringLength(20)] string? DayOfWeek = null);

public sealed record SaveTrainingPlanRequest(
    [param: Range(1, long.MaxValue)] long ClassId,
    [param: Range(1, long.MaxValue)] long? MemberId,
    [param: Required, StringLength(150)] string Title,
    [param: StringLength(500)] string? Goal,
    DateOnly StartDate,
    DateOnly? EndDate,
    [param: StringLength(1000)] string? FitnessCondition,
    [param: Required, MinLength(1), MaxLength(50)] PlanExercise[] Exercises,
    [param: Range(1, long.MaxValue)] long? PlanId = null);

public sealed record RecordProgressRequest(
    [param: Range(1, long.MaxValue)] long ClassId,
    [param: Range(1, long.MaxValue)] long MemberId,
    [param: Range(1, 500)] decimal? Weight,
    [param: Range(0, 100)] decimal? BodyFat,
    [param: Range(0, 500)] decimal? MuscleMass,
    [param: Required, StringLength(1000)] string WorkoutResult,
    [param: StringLength(1000)] string? CoachFeedback,
    DateOnly? Date,
    [param: Range(1, long.MaxValue)] long? SessionId = null,
    bool NotifyMember = true);

/// <summary>
/// Coach-entered biometrics for a member of an assigned class (manual input).
/// Gender, date of birth and fitness level are owned by the member's own profile and are not editable here.
/// </summary>
public sealed record UpdateMemberBiometricsRequest(
    [param: Range(80, 250)] decimal? HeightCm,
    [param: Range(25, 300)] decimal? WeightKg,
    [param: StringLength(1000)] string? MedicalNote);

public sealed record SendNotificationRequest(
    [param: Range(1, long.MaxValue)] long? ClassId,
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(4000)] string Content,
    [param: Range(1, long.MaxValue)] long? MemberId = null,
    long[]? ClassIds = null);

