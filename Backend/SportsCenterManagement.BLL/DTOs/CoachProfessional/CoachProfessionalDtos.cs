using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.CoachProfessional;

public sealed record SaveWorkEventRequest(
    long? MemberId,
    long? RoomId,
    [Required, StringLength(200)] string Title,
    [Required, StringLength(30)] string EventType,
    DateTime StartAt,
    DateTime EndAt,
    [StringLength(1000)] string? Notes);

public sealed record ChangeWorkEventStatusRequest(
    [Required, StringLength(30)] string Status,
    [StringLength(500)] string? Reason);

public sealed record SaveLeaveRequest(
    DateTime StartAt,
    DateTime EndAt,
    [Required, StringLength(30)] string LeaveType,
    [Required, StringLength(1000)] string Reason,
    long? SwapCoachId);

public sealed record ReviewLeaveRequest(
    [Required, StringLength(30)] string Status,
    [StringLength(1000)] string? ManagerNote,
    long? ReplacementCoachId = null);

public sealed record SaveHealthRecordRequest(
    long MemberId,
    [Required, StringLength(40)] string RecordType,
    [Required, StringLength(200)] string Title,
    [Required] string Details,
    DateOnly OccurredOn);

public sealed record SaveExerciseRequest(
    long? SportId,
    [Required, StringLength(150)] string Name,
    [StringLength(1000)] string? Description,
    [StringLength(100)] string? MuscleGroup,
    [StringLength(50)] string? DifficultyLevel,
    string? Instructions,
    [StringLength(500)] string? VideoUrl,
    [StringLength(500)] string? ImageUrl);

public sealed record SaveHomeworkRequest(
    long? ClassId,
    long? MemberId,
    [Required, StringLength(200)] string Title,
    [Required] string Description,
    DateTime? DueDate);

public sealed record SubmitHomeworkRequest([Required, StringLength(4000)] string Content);

public sealed record ReviewHomeworkRequest(
    [Range(0, 100)] decimal? Score,
    [StringLength(1000)] string? CoachFeedback);

public sealed record SaveNutritionPlanRequest(
    long MemberId,
    [Required, StringLength(200)] string Title,
    DateOnly StartDate,
    DateOnly? EndDate,
    [Range(800, 10000)] int DailyCalories,
    [Range(0, 1000)] int ProteinGrams,
    [Range(0, 2000)] int CarbGrams,
    [Range(0, 1000)] int FatGrams,
    string? MealPlanJson,
    [StringLength(1000)] string? Notes);

public sealed record SaveFoodDiaryRequest(
    DateOnly EntryDate,
    [Required, StringLength(40)] string MealType,
    [Required, StringLength(300)] string FoodName,
    [Range(0, 10000)] int? Calories,
    [Range(0, 1000)] decimal? ProteinGrams,
    [Range(0, 2000)] decimal? CarbGrams,
    [Range(0, 1000)] decimal? FatGrams,
    [StringLength(1000)] string? Notes);

public sealed record SendCoachMessageRequest(
    long CoachId,
    long MemberId,
    [Required, StringLength(4000)] string Content);

public sealed record SendBroadcastRequest(
    long? ClassId,
    long[]? MemberIds,
    [Required, StringLength(200)] string Title,
    [Required, StringLength(4000)] string Content,
    string[]? Channels);

public sealed record BookResourceRequest(
    long ResourceId,
    long? WorkEventId,
    DateTime StartAt,
    DateTime EndAt,
    [Required, StringLength(500)] string Purpose);

public sealed record CreateResourceRequest(
    [Required, StringLength(150)] string Name,
    [Required, StringLength(50)] string ResourceType,
    [StringLength(300)] string? Location,
    [Range(1, 10000)] int? Capacity);

public sealed record ReportEquipmentIssueRequest(
    long? ResourceId,
    [Required, StringLength(200)] string Title,
    [Required] string Description,
    [Required, StringLength(20)] string Severity,
    [StringLength(1000)] string? PhotoUrl);

public sealed record ResolveEquipmentIssueRequest(
    [Required, StringLength(30)] string Status,
    [StringLength(1000)] string? ResolutionNote);

public sealed record SaveCompensationPolicyRequest(
    long CoachId,
    [Range(0, 1000000000)] decimal MonthlyBaseSalary,
    [Range(0, 100000000)] decimal CommissionPerSession,
    [Range(0, 100)] decimal SalesCommissionPercent,
    [Range(0, 10000)] int TargetSessions,
    [Range(0, 100000000000)] decimal TargetSales,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record SaveSalesCreditRequest(
    long CoachId,
    long? MemberId,
    long? SubscriptionId,
    [Range(0.01, 100000000000)] decimal Amount,
    DateTime? CreditedAt,
    [StringLength(500)] string? Note);

public sealed record SaveCoachReviewRequest(
    long CoachId,
    long? WorkEventId,
    [Range(1, 5)] int Rating,
    [StringLength(2000)] string? Comment);
