using SportsCenterManagement.BLL.DTOs.Notifications;

namespace SportsCenterManagement.BLL.Interfaces;

public interface ICenterNotificationService
{
    Task<SendCenterNotificationResponse> SendAsync(
        long actorUserId,
        long centerId,
        SendCenterNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotificationResponse>> GetMineAsync(long userId, CancellationToken ct = default);

    Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(long userId, CancellationToken ct = default);

    Task<IReadOnlyList<CoachSentNotificationResponse>> GetCoachSentNotificationsAsync(long coachUserId, CancellationToken ct = default);
}
