using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

/// <summary>UC-12 Manage Classes &amp; Rooms (class CRUD + weekly schedules).</summary>
public interface IClassManagementService
{
    Task<ClassDetailResponse> CreateClassAsync(long actorUserId, CreateClassRequest request, CancellationToken cancellationToken = default);
    Task<ClassDetailResponse> PublishClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default);
    Task<ClassDetailResponse> GetClassAsync(long classId, CancellationToken cancellationToken = default);
    Task<ClassDetailResponse> UpdateClassAsync(long actorUserId, long classId, UpdateClassRequest request, CancellationToken cancellationToken = default);

    /// <summary>"Delete" = soft cancel. Never hard-deletes a class with enrollments/bookings.</summary>
    Task<CancelClassResult> CancelClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reopens a cancelled class as Draft. Cancelled schedules, sessions, enrollments and bookings stay cancelled.
    /// </summary>
    Task<ClassDetailResponse> RestoreClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassScheduleResponse>> GetSchedulesAsync(long classId, CancellationToken cancellationToken = default);
    Task<ClassScheduleResponse> CreateScheduleAsync(long actorUserId, long classId, CreateClassScheduleRequest request, CancellationToken cancellationToken = default);
    Task<ClassScheduleResponse> UpdateScheduleAsync(long actorUserId, long classId, long scheduleId, UpdateClassScheduleRequest request, CancellationToken cancellationToken = default);
    Task<ClassScheduleResponse> CancelScheduleAsync(long actorUserId, long classId, long scheduleId, CancellationToken cancellationToken = default);
}

/// <summary>Manages the sport catalog used by classes and coach specialties at a center.</summary>
public interface ISportManagementService
{
    Task<IReadOnlyList<ManagedSportResponse>> GetActiveAsync(long centerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedSportResponse>> GetAllAsync(long centerId, CancellationToken cancellationToken = default);
    Task<ManagedSportResponse> CreateAsync(long actorUserId, long centerId, SaveSportRequest request, CancellationToken cancellationToken = default);
    Task<ManagedSportResponse> UpdateAsync(long actorUserId, long centerId, long sportId, SaveSportRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Provides active coaches and their current teaching slots for manager assignment decisions.</summary>
public interface ICoachAssignmentQueryService
{
    Task<IReadOnlyList<CoachAssignmentCandidateResponse>> GetCandidatesAsync(
        long centerId, CancellationToken cancellationToken = default);
}

/// <summary>UC-42 View Class Schedules (member) and UC-30 View Teaching Schedule (coach).</summary>
public interface IClassScheduleQueryService
{
    Task<PagedResult<SessionScheduleItemResponse>> GetSessionsAsync(SessionScheduleQuery query, CancellationToken cancellationToken = default);

    /// <summary>The coach is always resolved from <paramref name="currentUserId"/>, never from client input.</summary>
    Task<IReadOnlyList<TeachingScheduleItemResponse>> GetTeachingScheduleAsync(
        long currentUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default, bool includeHistory = false);
}

/// <summary>UC-43 Book Class and UC-44 Cancel Class Booking.</summary>
public interface ISessionBookingService
{
    Task<BookSessionResult> BookSessionAsync(long currentUserId, long sessionId, CancellationToken cancellationToken = default, long? actorUserId = null);
    Task<CancelBookingResult> CancelBookingAsync(long currentUserId, long bookingId, CancellationToken cancellationToken = default, long? actorUserId = null);
    Task<IReadOnlyList<SessionBookingResponse>> ListMyBookingsAsync(long currentUserId, CancellationToken cancellationToken = default);
}
