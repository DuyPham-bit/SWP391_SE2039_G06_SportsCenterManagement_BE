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
    [param: Required, MinLength(1), MaxLength(50)] PlanExercise[] Exercises);

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

public sealed record SendNotificationRequest(
    [param: Range(1, long.MaxValue)] long? ClassId,
    [param: Required, StringLength(200)] string Title,
    [param: Required, StringLength(4000)] string Content,
    [param: Range(1, long.MaxValue)] long? MemberId = null,
    long[]? ClassIds = null);

public sealed record TrainingPlanResponse(long Id, long? ClassId, string? ClassName, string? SportName,
    long? MemberId, string? MemberName, long CoachId, string CoachName, string Title, string? Goal,
    DateOnly StartDate, DateOnly? EndDate, string? FitnessCondition, bool IsCustom,
    string Status, DateTime CreatedAt, PlanExercise[] Exercises);

public sealed record ProgressResponse(long Id, long? ClassId, string? ClassName, string? SportName,
    long MemberId, string MemberName, string CoachName, DateOnly Date, decimal? Weight, decimal? BodyFat,
    decimal? MuscleMass, string? WorkoutResult, string? CoachFeedback,
    long? SessionId = null, TimeOnly? SessionStartTime = null, TimeOnly? SessionEndTime = null);

public sealed record RecommendationHistory(DateOnly Date, decimal? Weight, decimal? BodyFat,
    decimal? MuscleMass, string? WorkoutResult, string? CoachFeedback);

public sealed record HealthRecordSummary(string RecordType, string Title, string Details, DateOnly OccurredOn);

public sealed record RecommendationContext(
    string SportName,
    string? FitnessGoal,
    string? FitnessLevel,
    string? MedicalNote,
    RecommendationHistory[] RecentHistory,
    decimal? HeightCm = null,
    decimal? WeightKg = null,
    string? Gender = null,
    DateOnly? DateOfBirth = null,
    int? Age = null,
    decimal? Bmi = null,
    string? BmiCategory = null,
    HealthRecordSummary[]? HealthRecords = null
);
