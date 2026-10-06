using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

internal static class FlowNotificationWriter
{
    public static async Task CreateAsync(
        SportsCenterDbContext db,
        long senderId,
        IEnumerable<long> recipientUserIds,
        string title,
        string message,
        string type,
        CancellationToken cancellationToken)
    {
        var recipients = recipientUserIds.Distinct().Where(id => id > 0).ToList();
        if (recipients.Count == 0) return;

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            SenderId = senderId,
            Title = title,
            Message = message,
            NotificationType = type,
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
    }
}
