using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
[RequirePermission(PermissionCodes.MemberCenterRead)]
[CenterCapability("checkin_member")]
public sealed class CheckinsController(ICheckinService checkinService) : FlowControllerBase, IAsyncActionFilter
{
    [HttpGet("centers/{centerId:long}/checkins/summary")]
    public async Task<ActionResult<CheckinSummary>> Summary(long centerId, CancellationToken ct) =>
        Ok(await checkinService.GetTodaySummaryAsync(CurrentUserId, centerId, ct));

    [HttpPost("centers/{centerId:long}/checkins")]
    public async Task<ActionResult<CheckinResponse>> CheckIn(
        long centerId,
        [FromBody] CheckinRequest request,
        CancellationToken cancellationToken)
    {
        var result = await checkinService.CheckInMemberAsync(CurrentUserId, centerId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("centers/{centerId:long}/checkins/today")]
    public async Task<ActionResult<IReadOnlyList<CheckinResponse>>> GetTodayCheckins(
        long centerId,
        CancellationToken cancellationToken)
    {
        var result = await checkinService.GetTodayCheckinsAsync(CurrentUserId, centerId, cancellationToken);
        return Ok(result);
    }

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
