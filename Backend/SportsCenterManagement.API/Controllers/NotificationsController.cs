using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class NotificationsController(SportsCenterDbContext db) : FlowControllerBase
{
    [HttpGet("notifications/me")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await (
        from link in db.UserNotifications join n in db.Notifications on link.NotificationId equals n.Id
        where link.UserId == CurrentUserId orderby n.CreatedAt descending, n.Id descending
        select new { n.Id, n.Title, Content = n.Message, n.NotificationType, n.CreatedAt, link.IsRead }
    ).Take(200).ToListAsync(ct));

    [HttpPut("notifications/{notificationId:long}/read")]
    public async Task<IActionResult> Read(long notificationId, CancellationToken ct)
    {
        var count = await db.UserNotifications.Where(n => n.NotificationId == notificationId && n.UserId == CurrentUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        return count == 0 ? NotFound() : NoContent();
    }

    [HttpGet("coaches/me/notifications")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Sent(CancellationToken ct) => Ok(await db.Notifications
        .Where(n => n.SenderId == CurrentUserId && n.NotificationType == "CoachMessage")
        .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(200)
        .Select(n => new { n.Id, n.Title, Content = n.Message, n.CreatedAt,
            Target = "Học viên được phân công", RecipientCount = db.UserNotifications.Count(l => l.NotificationId == n.Id) })
        .ToListAsync(ct));
}
