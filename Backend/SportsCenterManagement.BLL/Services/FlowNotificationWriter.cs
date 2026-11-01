using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

internal static class FlowNotificationWriter
{
    public static Task MembershipPaidAsync(SportsCenterDbContext db, long? senderId, MemberSubscription subscription,
        string packageName, DateOnly today, CancellationToken cancellationToken)
    {
        var scheduled = subscription.StartDate > today;
        var title = scheduled ? "Thanh toán thành công - Gói tập chờ hiệu lực" : "Thanh toán thành công - Gói tập đã kích hoạt";
        var state = scheduled ? "sẽ có hiệu lực" : "có hiệu lực";
        var message = $"{packageName} đã được thanh toán và {state} từ {subscription.StartDate:dd/MM/yyyy} đến {subscription.EndDate:dd/MM/yyyy}.";
        if (scheduled) message += " Gói đang dùng tiếp tục có hiệu lực đến ngày hết hạn của gói đó.";
        return CreateForMemberAsync(db, senderId, subscription.MemberId, title, message, "MembershipActivated", cancellationToken);
    }

    public static async Task CreateForMemberAsync(
        SportsCenterDbContext db, long? senderId, long memberId,
        string title, string message, string type, CancellationToken cancellationToken)
    {
        // Notifications are addressed to login accounts, not member profile IDs.
        var userId = await db.MemberProfiles.Where(m => m.Id == memberId)
            .Select(m => m.UserId).SingleAsync(cancellationToken);
        await CreateAsync(db, senderId ?? userId, [userId], title, message, type, cancellationToken);
    }

    public static async Task CreateAsync(
        SportsCenterDbContext db,
        long senderId,
        IEnumerable<long> recipientUserIds,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken,
        string? targetClassIds = null)
    {
        await CreateWithResultAsync(db, senderId, recipientUserIds, title, message, type, cancellationToken, targetClassIds);
    }

    public static async Task<Notification?> CreateWithResultAsync(
        SportsCenterDbContext db,
        long senderId,
        IEnumerable<long> recipientUserIds,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken,
        string? targetClassIds = null)
    {
        var recipients = recipientUserIds.Distinct().Where(id => id > 0).ToList();
        if (recipients.Count == 0) return null;

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            SenderId = senderId,
            Title = title,
            Message = message,
            NotificationType = type,
            TargetClassIds = targetClassIds,
            CreatedAt = now
        };
        await db.Notifications.AddAsync(notification, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var userId in recipients)
        {
            await db.UserNotifications.AddAsync(new UserNotification
            {
                NotificationId = notification.Id,
                UserId = userId,
                IsRead = false,
                CreatedAt = now
            }, cancellationToken);
        }
        await db.SaveChangesAsync(cancellationToken);
        return notification;
    }
}
