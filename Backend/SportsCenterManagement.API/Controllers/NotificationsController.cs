using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.BLL.DTOs.Notifications;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
[FlowExceptionFilter]
public sealed class NotificationsController(
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
    public async Task<IActionResult> Mine(CancellationToken ct) =>
        Ok(await centerNotificationService.GetMineAsync(CurrentUserId, ct));

    [HttpPut("notifications/{notificationId:long}/read")]
    public async Task<IActionResult> Read(long notificationId, CancellationToken ct)
    {
        var updated = await centerNotificationService.MarkAsReadAsync(notificationId, CurrentUserId, ct);
        return updated ? NoContent() : NotFound();
    }

    [HttpPut("notifications/me/read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await centerNotificationService.MarkAllAsReadAsync(CurrentUserId, ct);
        return NoContent();
    }

    [HttpGet("coaches/me/notifications")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Sent(CancellationToken ct) =>
        Ok(await centerNotificationService.GetCoachSentNotificationsAsync(CurrentUserId, ct));
}
