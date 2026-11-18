using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

/// <summary>UC-43 Book Class and UC-44 Cancel Class Booking.</summary>
public interface ISessionBookingService
{
    Task<BookSessionResult> BookSessionAsync(long currentUserId, long sessionId, CancellationToken cancellationToken = default, long? actorUserId = null);
    Task<CancelBookingResult> CancelBookingAsync(long currentUserId, long bookingId, CancellationToken cancellationToken = default, long? actorUserId = null);
    Task<IReadOnlyList<SessionBookingResponse>> ListMyBookingsAsync(long currentUserId, CancellationToken cancellationToken = default);

    Task<long> ResolveMemberUserIdAsync(long actorUserId, bool isAdmin, long memberId, CancellationToken ct = default);
    Task<BookSessionResult> BookForMemberAsync(long actorUserId, bool isAdmin, long memberId, long sessionId, CancellationToken ct = default);
    Task<CancelBookingResult> CancelForMemberAsync(long actorUserId, bool isAdmin, long memberId, long bookingId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberWaitlistItemDto>> GetMyWaitlistAsync(long userId, CancellationToken ct = default);
    Task LeaveWaitlistAsync(long userId, long waitlistId, CancellationToken ct = default);
    Task<int> FillOpenSeatsFromWaitlistAsync(long sessionId, long actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionRosterItemDto>> GetSessionRosterAsync(long coachUserId, long sessionId, CancellationToken ct = default);
    Task<int> RecordAttendanceAsync(long coachUserId, long sessionId, IReadOnlyList<AttendanceRecordDto> records, CancellationToken ct = default);
}
