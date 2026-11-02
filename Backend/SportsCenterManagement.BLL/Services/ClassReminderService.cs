using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassReminderService(SportsCenterDbContext db, TimeProvider timeProvider)
{
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = timeProvider.GetUtcNow();
        var localNow = utcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
        var localLimit = localNow.AddHours(1);
        var today = DateOnly.FromDateTime(localNow);
        var lastDate = DateOnly.FromDateTime(localLimit);
        var nowTime = TimeOnly.FromDateTime(localNow);
        var lastTime = TimeOnly.FromDateTime(localLimit);

        // Keep the eligibility check, notification and recipient in one transaction.
        // The unique reminder key also protects against concurrent API instances.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var bookings = await (
            from booking in db.SessionBookings
            join session in db.ClassSessions on booking.SessionId equals session.Id
            join cls in db.Classes on session.ClassId equals cls.Id
            join member in db.MemberProfiles on booking.MemberId equals member.Id
            join user in db.Users on member.UserId equals user.Id
            join center in db.Centers on cls.CenterId equals center.Id
            where booking.Status == FlowStatuses.BookingBooked
                && session.SessionStatus == FlowStatuses.SessionScheduled
                && cls.Status == FlowStatuses.ClassPublished
                && user.Status == "Active" && center.Status == "Active"
                && session.SessionDate >= today && session.SessionDate <= lastDate
                && (session.SessionDate > today || session.StartTime > nowTime)
                && (session.SessionDate < lastDate || session.StartTime <= lastTime)
            orderby session.SessionDate, session.StartTime, booking.Id
            select new
            {
                booking.Id, member.UserId, cls.Name,
                session.SessionDate, session.StartTime, session.EndTime,
                RoomName = db.Rooms.Where(room => room.Id == (session.RoomId ?? cls.RoomId))
                    .Select(room => room.Name).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var due = bookings.Select(booking => new
        {
            Booking = booking,
            Start = booking.SessionDate.ToDateTime(booking.StartTime),
            Key = FormattableString.Invariant($"booking:{booking.Id}:start:{booking.SessionDate:yyyyMMdd}{booking.StartTime:HHmmssfffffff}")
        }).ToList();
        var keys = due.Select(item => item.Key).ToArray();
        var sentKeys = (await db.Notifications.Where(notification => keys.Contains(notification.ReminderKey!))
            .Select(notification => notification.ReminderKey!).ToListAsync(cancellationToken)).ToHashSet();

        var sent = 0;
        foreach (var item in due.Where(item => !sentKeys.Contains(item.Key)))
        {
            var booking = item.Booking;
            var notification = new Notification
            {
                SenderId = booking.UserId,
                Title = "Nhắc lịch: Ca học sắp bắt đầu",
                Message = $"Bạn có ca học {booking.Name} ngày {booking.SessionDate:dd/MM/yyyy}, "
                    + $"lúc {booking.StartTime:HH:mm} - {booking.EndTime:HH:mm}"
                    + (string.IsNullOrWhiteSpace(booking.RoomName) ? "." : $", tại {booking.RoomName}.")
                    + " Ca học sẽ bắt đầu trong vòng 1 tiếng. Vui lòng đến đúng giờ.",
                NotificationType = "ClassReminder",
                ReminderKey = item.Key,
                ScheduledAt = new DateTimeOffset(item.Start, TimeSpan.FromHours(7)).AddHours(-1).UtcDateTime,
                CreatedAt = utcNow.UtcDateTime
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(cancellationToken);
            db.UserNotifications.Add(new UserNotification
            {
                NotificationId = notification.Id,
                UserId = booking.UserId,
                IsRead = false,
                CreatedAt = utcNow.UtcDateTime
            });
            sent++;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sent;
    }
}
