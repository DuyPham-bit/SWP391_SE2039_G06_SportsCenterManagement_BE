using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Notifications;

public sealed record SendCenterNotificationRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(5000)] string Message,
    bool SendToAll,
    string[] TargetRoles,
    string[]? Channels = null);

public sealed record SendCenterNotificationResponse(
    long NotificationId,
    int RecipientCount,
    IReadOnlyList<string> TargetRoles,
    DateTime CreatedAt);
