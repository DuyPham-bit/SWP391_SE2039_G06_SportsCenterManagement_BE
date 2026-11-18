using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

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
