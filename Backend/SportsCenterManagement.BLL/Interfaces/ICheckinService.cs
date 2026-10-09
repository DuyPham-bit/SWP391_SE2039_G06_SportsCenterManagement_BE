using SportsCenterManagement.BLL.DTOs.Checkins;

namespace SportsCenterManagement.BLL.Interfaces;

public interface ICheckinService
{
    Task<CheckinEligibilityResponse> GetEligibilityAsync(
        long staffUserId,
        long centerId,
        long? memberId,
        string? memberCode,
        CancellationToken cancellationToken = default);

    Task<CounterCheckinResponse> CheckInAsync(
        long staffUserId,
        long centerId,
        CounterCheckinRequest request,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckinListItemResponse>> GetDailyCheckinsAsync(
        long staffUserId,
        long centerId,
        DateOnly businessDate,
        CancellationToken cancellationToken = default);
}
