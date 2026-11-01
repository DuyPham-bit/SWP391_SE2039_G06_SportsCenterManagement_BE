using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.API.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-42 View Class Schedules and UC-30 View Teaching Schedule.</summary>
public sealed class ClassSchedulesController(IClassScheduleQueryService service) : FlowControllerBase, IAsyncActionFilter
{
    /// <summary>Malformed dates/pagination are rejected with 400 before any query runs.</summary>
    [Authorize(Roles = "MEMBER,MANAGER,ADMIN,RECEPTIONIST")]
    [HttpGet("class-sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] long? sportId,
        [FromQuery] long? coachId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] long? centerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        long? scopedCenterId = null;
        if (User.IsInRole("MEMBER") || User.IsInRole("MANAGER") || User.IsInRole("RECEPTIONIST"))
        {
            if (!long.TryParse(User.FindFirst("centerId")?.Value, out var assignedCenterId) || assignedCenterId <= 0)
                throw new InvalidOperationException("Tài khoản chưa được gán vào trung tâm hoạt động.") { Data = { ["StatusCode"] = 403 } };
            scopedCenterId = assignedCenterId;
        }
        else if (User.IsInRole("ADMIN") && centerId is > 0)
            scopedCenterId = centerId;

        return Ok(await service.GetSessionsAsync(
            new SessionScheduleQuery(sportId, coachId, from, to, page, pageSize, scopedCenterId), ct));
    }

    /// <summary>No coach id parameter on purpose: the coach is taken from the token.</summary>
    [Authorize(Roles = "COACH")]
    [HttpGet("coaches/me/teaching-schedule")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> GetTeachingSchedule(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct, [FromQuery] bool includeHistory = false) =>
        Ok(await service.GetTeachingScheduleAsync(CurrentUserId, from, to, ct, includeHistory));

    [NonAction]
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        // Only tagged business failures are mapped here; unexpected errors remain available to the middleware.
        if (executed.ExceptionHandled || executed.Exception is not InvalidOperationException exception ||
            exception.Data["StatusCode"] is not int statusCode) return;

        executed.Result = new ObjectResult(new
        {
            code = exception.Data["Code"] as string,
            message = exception.Message,
            detail = exception.Message,
            proposedHandling = exception.Data["ProposedHandling"] as string,
            isDraftPolicy = exception.Data["IsDraftPolicy"] is true
        }) { StatusCode = statusCode };
        executed.ExceptionHandled = true;
    }
}
