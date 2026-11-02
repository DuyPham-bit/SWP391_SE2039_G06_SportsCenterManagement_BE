using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Context;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

/// <summary>UC-12 Manage Classes &amp; Rooms (Manager/Admin only).</summary>
[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ClassManagementController(IClassManagementService service, SportsCenterDbContext db) : FlowControllerBase, IAsyncActionFilter
{
    [HttpPost("classes")]
    public async Task<IActionResult> Create([FromBody] CreateClassRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, request.CenterId, ct);
        var result = await service.CreateClassAsync(CurrentUserId, request, ct);
        return CreatedAtAction(nameof(Get), new { classId = result.Id }, result);
    }

    [HttpGet("classes/{classId:long}")]
    public async Task<IActionResult> Get(long classId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.GetClassAsync(classId, ct));
    }

    [HttpPut("classes/{classId:long}")]
    public async Task<IActionResult> Update(long classId, [FromBody] UpdateClassRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.UpdateClassAsync(CurrentUserId, classId, request, ct));
    }

    [HttpPost("classes/{classId:long}/publish")]
    public async Task<IActionResult> Publish(long classId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.PublishClassAsync(CurrentUserId, classId, ct));
    }

    /// <summary>Soft cancel; the response reports how many members/bookings were affected.</summary>
    [HttpDelete("classes/{classId:long}")]
    public async Task<IActionResult> Cancel(long classId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.CancelClassAsync(CurrentUserId, classId, ct));
    }

    /// <summary>
    /// Reopens a cancelled class as a draft. Historical cancellations are deliberately not reversed.
    /// </summary>
    [HttpPost("classes/{classId:long}/restore")]
    public async Task<IActionResult> Restore(long classId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.RestoreClassAsync(CurrentUserId, classId, ct));
    }

    [HttpGet("classes/{classId:long}/schedules")]
    public async Task<IActionResult> GetSchedules(long classId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.GetSchedulesAsync(classId, ct));
    }

    [HttpPost("classes/{classId:long}/schedules")]
    public async Task<IActionResult> CreateSchedule(long classId, [FromBody] CreateClassScheduleRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return StatusCode(StatusCodes.Status201Created, await service.CreateScheduleAsync(CurrentUserId, classId, request, ct));
    }

    [HttpPut("classes/{classId:long}/schedules/{scheduleId:long}")]
    public async Task<IActionResult> UpdateSchedule(long classId, long scheduleId, [FromBody] UpdateClassScheduleRequest request, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.UpdateScheduleAsync(CurrentUserId, classId, scheduleId, request, ct));
    }

    [HttpDelete("classes/{classId:long}/schedules/{scheduleId:long}")]
    public async Task<IActionResult> CancelSchedule(long classId, long scheduleId, CancellationToken ct)
    {
        await CenterScope.EnsureClassAccessAsync(db, User, classId, ct);
        return Ok(await service.CancelScheduleAsync(CurrentUserId, classId, scheduleId, ct));
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
