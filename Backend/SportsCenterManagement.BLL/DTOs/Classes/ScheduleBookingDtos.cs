namespace SportsCenterManagement.BLL.DTOs.Classes;

// ---------- UC-12: Manage classes & weekly schedules ----------

public sealed record UpdateClassRequest(
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes);

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
    string Status);

/// <summary>Result of "deleting" a class: classes are never hard-deleted, only set to Cancelled.</summary>
public sealed record CancelClassResult(
    long ClassId,
    string Status,
    int AffectedEnrollments,
    int AffectedBookings,
    int CancelledSessions);

public sealed record UpdateClassScheduleRequest(
    long? RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly StartDate,
    DateOnly EndDate);

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

public sealed record SessionScheduleQuery(
    long? SportId,
    long? CoachId,
    DateOnly? From,
    DateOnly? To,
    int Page = 1,
    int PageSize = 20);

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
    bool IsBookable);

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
    int BookedCount);

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
    DateTime BookedAt);

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
