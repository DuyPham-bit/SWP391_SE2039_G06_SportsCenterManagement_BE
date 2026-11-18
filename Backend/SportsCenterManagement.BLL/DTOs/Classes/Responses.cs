namespace SportsCenterManagement.BLL.DTOs.Classes
{
public static class Responses
{
    public sealed record ClassCatalogResponse(
        long Id,
        long CenterId,
        long SportId,
        long? RoomId,
        string Name,
        string? Description,
        string? Level,
        int Capacity,
        int DurationMinutes);
}
}

namespace SportsCenterManagement.BLL.DTOs.Classes
{
// ---------- UC-12: Manage classes & weekly schedules ----------

public sealed record ClassDetailResponse(
    long Id,
    long CenterId,
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes,
    string Status,
    bool AllowWaitlist = true);

/// <summary>Result of "deleting" a class: classes are never hard-deleted, only set to Cancelled.</summary>
public sealed record CancelClassResult(
    long ClassId,
    string Status,
    int AffectedEnrollments,
    int AffectedBookings,
    int CancelledSessions);

public sealed record ClassScheduleResponse(
    long Id,
    long ClassId,
    long? RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Status,
    int GeneratedSessions);

// ---------- UC-42 / UC-30: schedule queries ----------

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record SessionScheduleItemResponse(
    long SessionId,
    long ClassId,
    string ClassName,
    long SportId,
    string SportName,
    long? RoomId,
    long? CoachId,
    string? CoachName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SessionStatus,
    string ClassStatus,
    int Capacity,
    int BookedCount,
    int AvailableSeats,
    bool IsBookable,
    bool AllowWaitlist = true,
    string? RoomName = null,
    // Member-only: ALREADY_BOOKED | EXPIRED | WAITLISTED | NO_VALID_PACKAGE | SPORT_NOT_INCLUDED | FULL | NOT_OPEN | CAN_BOOK
    string? ButtonState = null,
    long? BookingId = null,
    // Member-only: the caller's own queue entry and FIFO position for this session.
    long? WaitlistId = null,
    int? WaitlistPosition = null,
    int WaitlistCount = 0);

/// <summary>Member's current package entitlements for the schedule page (sport filter locks, cancel rule).</summary>
public sealed record MemberEntitlementResponse(
    bool HasActivePackage,
    long? SubscriptionId,
    long? PackageId,
    string? PackageName,
    int TierLevel,
    bool IsAllAccess,
    int MaxSports,
    int CancelBeforeHours,
    int MaxFreezeDays,
    DateOnly? EndDate,
    IReadOnlyList<long>? AllowedSportIds,
    int ActiveSportCount);

public sealed record TeachingScheduleItemResponse(
    long SessionId,
    long ClassId,
    string ClassName,
    string SportName,
    long? RoomId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string SessionStatus,
    string ClassStatus,
    int Capacity,
    int BookedCount,
    string? RoomName = null);

// ---------- UC-43 / UC-44: session bookings ----------

public sealed record SessionBookingResponse(
    long Id,
    long SessionId,
    long ClassId,
    string ClassName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    DateTime BookedAt,
    string? SessionStatus = null,
    string? RoomName = null,
    string? CoachName = null,
    string? CoachSpecialization = null,
    string? AttendanceStatus = null,
    string? AttendanceNote = null,
    DateTime? CheckInTime = null);

public sealed record BookSessionResult(
    bool Booked,
    bool Waitlisted,
    SessionBookingResponse? Booking,
    long? WaitlistId,
    string Message);

public sealed record CancelBookingResult(
    SessionBookingResponse Booking,
    bool IsLateCancellation,
    long? PromotedMemberId,
    string Message);
}

namespace SportsCenterManagement.BLL.DTOs.Classes
{
public sealed record ClassCatalogResponse(
    long Id,
    long CenterId,
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes);
}

namespace SportsCenterManagement.BLL.DTOs.Classes
{
public sealed record ClassCoachResponse(
    long ClassId,
    long CoachId,
    string CoachName,
    string CoachCode,
    bool IsPrimary,
    DateOnly? AssignedDate,
    string? Specialization);
}

namespace SportsCenterManagement.BLL.DTOs.Classes
{
public sealed record ManagedSportResponse(
    long Id,
    string Name,
    string? Description,
    string Status,
    bool IsShared);

public sealed record CoachTeachingSlotResponse(
    long ClassId,
    string ClassName,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? RoomName);

public sealed record CoachAssignmentCandidateResponse(
    long Id,
    long UserId,
    string CoachCode,
    string FullName,
    string? Specialization,
    string? Certification,
    IReadOnlyList<long> SportIds,
    IReadOnlyList<CoachTeachingSlotResponse> TeachingSlots);
}

namespace SportsCenterManagement.BLL.DTOs.Classes
{


    public sealed record MemberWaitlistItemDto(
        long Id,
        long? SessionId,
        string ClassName,
        DateOnly SessionDate,
        TimeOnly StartTime,
        TimeOnly EndTime,
        string Status,
        DateTime JoinedAt,
        int Position = 0);

    public sealed record SessionRosterItemDto(
        long? BookingId,
        long MemberId,
        string MemberName,
        string? MemberCode,
        string Email,
        string? Phone,
        string? FitnessGoal,
        string? FitnessLevel,
        string? AttendanceStatus,
        string? Note);

}

namespace SportsCenterManagement.BLL.DTOs.Classes
{


    public sealed record MemberEnrollmentDto(
        long Id,
        long ClassId,
        string Name,
        long? SubscriptionId,
        DateTime RegisteredAt,
        DateTime? CancelledAt,
        string Status,
        string? CancellationReason);

    public sealed record ClassEnrollmentResultDto(
        long Id,
        long ClassId,
        long MemberId,
        long? SubscriptionId,
        DateTime RegisteredAt,
        string Status);

    public sealed record CancelledClassEnrollmentResultDto(
        long Id,
        long ClassId,
        string Status,
        DateTime? CancelledAt,
        string? CancellationReason);

}

namespace SportsCenterManagement.BLL.DTOs.Classes
{


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

}
