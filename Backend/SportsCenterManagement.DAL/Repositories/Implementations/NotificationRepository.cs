using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.DAL.Repositories.Implementations;

public sealed class NotificationRepository(SportsCenterDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<UserNotificationItem>> GetUserNotificationsAsync(long userId, int limit = 200, CancellationToken ct = default)
    {
        return await (
            from link in db.UserNotifications
            join n in db.Notifications on link.NotificationId equals n.Id
            where link.UserId == userId
            orderby n.CreatedAt descending, n.Id descending
            select new UserNotificationItem(
                n.Id,
                n.Title,
                n.Message,
                n.NotificationType,
                n.SenderId,
                n.CreatedAt,
                link.IsRead)
        ).Take(limit).ToListAsync(ct);
    }

    public async Task<bool> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        var count = await db.UserNotifications
            .Where(n => n.NotificationId == notificationId && n.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        return count > 0;
    }

    public async Task MarkAllAsReadAsync(long userId, CancellationToken ct = default)
    {
        var readAt = DateTime.UtcNow;
        await db.UserNotifications
            .Where(link => link.UserId == userId && !link.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(link => link.IsRead, true).SetProperty(link => link.ReadAt, readAt), ct);
    }

    public async Task<IReadOnlyList<CoachSentNotificationItem>> GetCoachSentNotificationsAsync(long coachUserId, int limit = 200, CancellationToken ct = default)
    {
        var rows = await db.Notifications
            .Where(n => n.SenderId == coachUserId && n.NotificationType == "CoachMessage")
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(limit)
            .Select(n => new CoachSentNotificationItem(
                n.Id,
                n.Title,
                n.Message,
                n.CreatedAt,
                n.TargetClassIds,
                db.UserNotifications.Count(l => l.NotificationId == n.Id),
                db.UserNotifications.Count(l => l.NotificationId == n.Id) == 1
                    ? db.MemberProfiles.Where(m => db.UserNotifications.Any(l => l.NotificationId == n.Id && l.UserId == m.UserId))
                        .Select(m => m.FullName).FirstOrDefault()
                    : null
            ))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyDictionary<long, string>> GetClassNamesAsync(IEnumerable<long> classIds, CancellationToken ct = default)
    {
        var targetIds = classIds.Distinct().ToArray();
        return await db.Classes
            .Where(c => targetIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    public async Task<IReadOnlyList<long>> GetRecipientUserIdsForCenterAsync(long actorUserId, long centerId, IReadOnlyList<string> targetRoles, CancellationToken ct = default)
    {
        return await (from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            where user.Id != actorUserId && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil <= DateTime.UtcNow)
                && targetRoles.Contains(role.Name)
                && ((role.Name == RoleNames.Member && db.MemberProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId))
                    || (role.Name == RoleNames.Coach && db.CoachProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId && profile.Status == "Active"))
                    || ((role.Name == RoleNames.Receptionist || role.Name == RoleNames.Manager)
                        && db.StaffProfiles.Any(profile => profile.UserId == user.Id && profile.CenterId == centerId && profile.Status == "Active")))
            select user.Id).Distinct().ToListAsync(ct);
    }

    public async Task<bool> IsActiveManagerAtCenterAsync(long userId, long centerId, CancellationToken ct = default)
    {
        return await (from user in db.Users.AsNoTracking()
            join role in db.Roles.AsNoTracking() on user.RoleId equals role.Id
            join staff in db.StaffProfiles.AsNoTracking() on user.Id equals staff.UserId
            where user.Id == userId && role.Name == RoleNames.Manager && staff.CenterId == centerId && staff.Status == "Active"
            select staff.Id).AnyAsync(ct);
    }

    public async Task<Notification?> CreateNotificationWithRecipientsAsync(long senderId, IEnumerable<long> recipientUserIds, string title, string message, string type, CancellationToken ct = default, string? targetClassIds = null)
    {
        var users = recipientUserIds.Distinct().Where(id => id > 0).ToList();
        if (users.Count == 0) return null;

        var notification = new Notification
        {
            SenderId = senderId,
            Title = title,
            Message = message,
            NotificationType = type,
            TargetClassIds = targetClassIds,
            CreatedAt = DateTime.UtcNow
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(ct);

        foreach (var userId in users)
        {
            db.UserNotifications.Add(new UserNotification
            {
                NotificationId = notification.Id,
                UserId = userId,
                IsRead = false
            });
        }

        await db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task CreateForMemberAsync(long? senderId, long memberId, string title, string message, string type, CancellationToken ct = default)
    {
        var userId = await db.MemberProfiles.Where(m => m.Id == memberId)
            .Select(m => m.UserId).SingleAsync(ct);
        await CreateNotificationWithRecipientsAsync(senderId ?? userId, [userId], title, message, type, ct);
    }
}

