using SportsCenterManagement.BLL.DTOs.Checkins;

namespace SportsCenterManagement.BLL.Interfaces;

public interface ICheckinService
{
    Task<CheckinResponse> CheckInMemberAsync(
        long actorUserId,
        long centerId,
        CheckinRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckinResponse>> GetTodayCheckinsAsync(
        long actorUserId,
        long centerId,
        CancellationToken cancellationToken = default);
}
