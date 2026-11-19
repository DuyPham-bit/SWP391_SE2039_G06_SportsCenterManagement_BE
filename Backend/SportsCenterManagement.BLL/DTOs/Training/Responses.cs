namespace SportsCenterManagement.BLL.DTOs.Training;



/// <summary>Weight chosen for AI context and where it came from ("training_result" or "profile").</summary>
public sealed record WeightPick(decimal? Weight, string? Source, DateOnly? MeasuredOn);

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
    HealthRecordSummary[]? HealthRecords = null,
    string? WeightSource = null,
    DateOnly? WeightMeasuredOn = null,
    decimal? ProfileWeightKg = null,
    // Newest training result of this member across all classes; takes priority over manual entry.
    RecommendationHistory? LatestResult = null
);

