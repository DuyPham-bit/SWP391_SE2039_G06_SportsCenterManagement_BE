namespace SportsCenterManagement.BLL.DTOs.Notifications;



public sealed record SendCenterNotificationResponse(
    long NotificationId,
    int RecipientCount,
    IReadOnlyList<string> TargetRoles,
    DateTime CreatedAt);




public sealed record UserNotificationResponse(
    long Id,
    string Title,
    string Content,
    string NotificationType,
    long SenderId,
    DateTime CreatedAt,
    bool IsRead);

public sealed record CoachSentNotificationResponse(
    long Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    int RecipientCount,
    long? ClassId,
    long[] ClassIds,
    string Target);

