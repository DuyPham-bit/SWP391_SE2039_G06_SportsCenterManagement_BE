using SportsCenterManagement.BLL.DTOs.Notifications;

namespace SportsCenterManagement.BLL.Interfaces;

public interface ICenterNotificationService
{
    Task<SendCenterNotificationResponse> SendAsync(
        long actorUserId,
        long centerId,
        SendCenterNotificationRequest request,
        CancellationToken cancellationToken = default);
}
