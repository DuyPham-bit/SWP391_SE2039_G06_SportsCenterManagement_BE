using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Repositories.Interfaces;

public record UserNotificationItem(
    long Id,
    string Title,
    string Content,
    string NotificationType,
    long SenderId,
    DateTime CreatedAt,
    bool IsRead);

public record CoachSentNotificationItem(
    long Id,
    string Title,
    string Content,
    DateTime CreatedAt,
    string? TargetClassIds,
    int RecipientCount,
    string? SingleMemberName);

public interface INotificationRepository
{
    Task<IReadOnlyList<UserNotificationItem>> GetUserNotificationsAsync(long userId, int limit = 200, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<CoachSentNotificationItem>> GetCoachSentNotificationsAsync(long coachUserId, int limit = 200, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, string>> GetClassNamesAsync(IEnumerable<long> classIds, CancellationToken ct = default);
    Task<IReadOnlyList<long>> GetRecipientUserIdsForCenterAsync(long actorUserId, long centerId, IReadOnlyList<string> targetRoles, CancellationToken ct = default);
    Task<bool> IsActiveManagerAtCenterAsync(long userId, long centerId, CancellationToken ct = default);
    Task<Notification?> CreateNotificationWithRecipientsAsync(long senderId, IEnumerable<long> recipientUserIds, string title, string message, string type, CancellationToken ct = default, string? targetClassIds = null);
    Task CreateForMemberAsync(long? senderId, long memberId, string title, string message, string type, CancellationToken ct = default);
}

