using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.BLL.DTOs.Notifications;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
[FlowExceptionFilter]
public sealed class NotificationsController(
    SportsCenterDbContext db,
    ICenterNotificationService centerNotificationService) : FlowControllerBase
{
    [HttpPost("centers/{centerId:long}/notifications")]
    [Authorize(Roles = "MANAGER")]
    public async Task<ActionResult<SendCenterNotificationResponse>> SendToCenter(
        long centerId,
        SendCenterNotificationRequest request,
        CancellationToken ct) =>
        Ok(await centerNotificationService.SendAsync(CurrentUserId, centerId, request, ct));

    [HttpGet("notifications/me")]
    public async Task<IActionResult> Mine(CancellationToken ct) => Ok(await (
        from link in db.UserNotifications join n in db.Notifications on link.NotificationId equals n.Id
        where link.UserId == CurrentUserId orderby n.CreatedAt descending, n.Id descending
        select new { n.Id, n.Title, Content = n.Message, n.NotificationType, n.SenderId, n.CreatedAt, link.IsRead }
    ).Take(200).ToListAsync(ct));

    [HttpPut("notifications/{notificationId:long}/read")]
    public async Task<IActionResult> Read(long notificationId, CancellationToken ct)
    {
        var count = await db.UserNotifications.Where(n => n.NotificationId == notificationId && n.UserId == CurrentUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, DateTime.UtcNow), ct);
        return count == 0 ? NotFound() : NoContent();
    }

    [HttpPut("notifications/me/read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var readAt = DateTime.UtcNow;
        await db.UserNotifications.Where(link => link.UserId == CurrentUserId && !link.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(link => link.IsRead, true)
                .SetProperty(link => link.ReadAt, readAt), ct);
        return NoContent();
    }

    [HttpGet("coaches/me/notifications")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Sent(CancellationToken ct)
    {
        var rows = await db.Notifications
            .Where(n => n.SenderId == CurrentUserId && n.NotificationType == "CoachMessage")
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(200)
            .Select(n => new {
                n.Id,
                n.Title,
                Content = n.Message,
                n.CreatedAt,
                n.TargetClassIds,
                RecipientCount = db.UserNotifications.Count(l => l.NotificationId == n.Id),
                SingleMemberName = db.UserNotifications.Count(l => l.NotificationId == n.Id) == 1
                    ? db.MemberProfiles.Where(m => db.UserNotifications.Any(l => l.NotificationId == n.Id && l.UserId == m.UserId))
                        .Select(m => m.FullName).FirstOrDefault()
                    : null
            })
            .ToListAsync(ct);
        var targetIds = rows.SelectMany(row => ParseClassIds(row.TargetClassIds)).Distinct().ToArray();
        var classNames = await db.Classes.Where(c => targetIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name }).ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return Ok(rows.Select(row =>
        {
            var classIds = ParseClassIds(row.TargetClassIds);
            var target = classIds.Length > 0
                ? "Lớp: " + string.Join(", ", classIds.Select(id => classNames.GetValueOrDefault(id, $"#{id}")))
                : row.SingleMemberName is not null ? "Học viên: " + row.SingleMemberName : "Học viên được phân công";
            return new { row.Id, row.Title, row.Content, row.CreatedAt, row.RecipientCount,
                ClassId = classIds.Length == 1 ? (long?)classIds[0] : null, ClassIds = classIds, Target = target };
        }).ToArray());
    }

    private static long[] ParseClassIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return (JsonSerializer.Deserialize<long[]>(value) ?? []).Where(id => id > 0).Distinct().ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
