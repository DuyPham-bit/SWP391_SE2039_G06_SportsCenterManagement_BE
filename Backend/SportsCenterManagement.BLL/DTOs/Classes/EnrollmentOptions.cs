namespace SportsCenterManagement.BLL.DTOs.Classes;

/// <summary>Sport → coach → class tree a member can pick from when signing up for a sport.</summary>
public sealed record MemberEnrollmentOptionsDto(
    long CenterId,
    IReadOnlyList<EnrollmentSubscriptionDto> Subscriptions,
    IReadOnlyList<EnrollmentSportOptionDto> Sports);

/// <summary>An active package and how much of its sport/class allowance is already used.</summary>
public sealed record EnrollmentSubscriptionDto(
    long SubscriptionId,
    string PackageName,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int AllowedSports,
    int? MaxClasses,
    IReadOnlyList<long> UsedSportIds,
    int UsedClasses);

public sealed record EnrollmentSportOptionDto(
    long SportId,
    string Name,
    string? Description,
    bool IsEnrolled,
    IReadOnlyList<EnrollmentCoachOptionDto> Coaches);

public sealed record EnrollmentCoachOptionDto(
    long CoachId,
    string FullName,
    string? Specialization,
    string? Certification,
    int? ExperienceYears,
    string? Bio,
    double? AverageRating,
    int ReviewCount,
    IReadOnlyList<EnrollmentClassOptionDto> Classes);

/// <summary>
/// One class taught by the coach. Exactly one of these holds: <see cref="EnrollmentId"/> (already joined),
/// <see cref="EligibleSubscriptionId"/> (can join with that package) or <see cref="BlockedCode"/> (why not).
/// </summary>
public sealed record EnrollmentClassOptionDto(
    long ClassId,
    string Name,
    string? Level,
    string? Description,
    string? RoomName,
    int Capacity,
    int EnrolledCount,
    IReadOnlyList<EnrollmentScheduleDto> Schedules,
    long? EnrollmentId,
    long? EligibleSubscriptionId,
    string? BlockedCode,
    string? BlockedReason);

public sealed record EnrollmentScheduleDto(int DayOfWeek, TimeOnly StartTime, TimeOnly EndTime, string? RoomName);
