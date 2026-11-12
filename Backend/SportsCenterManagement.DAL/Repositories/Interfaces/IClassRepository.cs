using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Repositories.Interfaces;

public sealed record MemberEnrollmentItem(
    long Id,
    long ClassId,
    string Name,
    long? SubscriptionId,
    DateTime RegisteredAt,
    DateTime? CancelledAt,
    string Status,
    string? CancellationReason);

public sealed record MemberWaitlistItem(
    long Id,
    long? SessionId,
    string ClassName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    DateTime JoinedAt,
    string SessionStatus,
    int Position);

public sealed record SessionRosterItem(
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

public interface IClassRepository
{
    Task<long?> GetMemberIdByUserIdAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberEnrollmentItem>> GetMemberEnrollmentsAsync(long memberId, CancellationToken ct = default);
    Task<bool> ExistsEnrollmentAsync(long enrollmentId, long classId, long memberId, CancellationToken ct = default);

    Task<long?> GetClassIdBySessionIdAsync(long sessionId, CancellationToken ct = default);
    Task<long?> GetClassIdByBookingIdAsync(long bookingId, long memberId, CancellationToken ct = default);
    Task<(long UserId, long? CenterId)?> GetMemberUserAndCenterAsync(long memberId, CancellationToken ct = default);
    Task<IReadOnlyList<MemberWaitlistItem>> GetMemberWaitlistAsync(long userId, CancellationToken ct = default);
    Task<int> CancelWaitlistEntryAsync(long waitlistId, long memberId, CancellationToken ct = default);
    Task<bool> IsCoachAssignedToSessionAsync(long coachUserId, long sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionRosterItem>> GetSessionRosterAsync(long sessionId, CancellationToken ct = default);
    Task<ClassSession?> GetSessionByIdAsync(long sessionId, CancellationToken ct = default);
    Task<long?> GetCenterIdByClassIdAsync(long classId, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetEligibleAttendanceMemberIdsAsync(long sessionId, long classId, IEnumerable<long> memberIds, CancellationToken ct = default);
    Task UpsertAttendancesAsync(long sessionId, long actorUserId, IReadOnlyList<(long MemberId, string Status, string? Note)> records, CancellationToken ct = default);
}
