using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

/// <summary>UC-42 View Class Schedules (member) and UC-30 View Teaching Schedule (coach).</summary>
public interface IClassScheduleQueryService
{
    Task<PagedResult<SessionScheduleItemResponse>> GetSessionsAsync(SessionScheduleQuery query, CancellationToken cancellationToken = default);
    Task<MemberEntitlementResponse> GetMyEntitlementsAsync(long currentUserId, CancellationToken cancellationToken = default);

    /// <summary>The coach is always resolved from <paramref name="currentUserId"/>, never from client input.</summary>
    Task<IReadOnlyList<TeachingScheduleItemResponse>> GetTeachingScheduleAsync(
        long currentUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default, bool includeHistory = false);
}
