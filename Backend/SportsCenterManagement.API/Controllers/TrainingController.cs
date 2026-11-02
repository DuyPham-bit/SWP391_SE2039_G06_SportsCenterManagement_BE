using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Training;
using SportsCenterManagement.BLL.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

[Authorize]
public sealed class TrainingController(TrainingService training) : FlowControllerBase, IAsyncActionFilter
{
    [HttpGet("coaches/me/classes")]
    [Authorize(Roles = "COACH,MANAGER")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Classes(CancellationToken ct) => Ok(await training.ListClassesAsync(CurrentUserId, ct));

    [HttpGet("coaches/me/classes/{classId:long}/members")]
    [Authorize(Roles = "COACH,MANAGER")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Members(long classId, CancellationToken ct) => Ok(await training.ListMembersAsync(CurrentUserId, classId, ct));

    [HttpGet("coaches/me/training-plans")]
    [Authorize(Roles = "COACH,MANAGER")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> CoachPlans(CancellationToken ct) => Ok(await training.ListPlansAsync(CurrentUserId, false, ct));

    [HttpPost("coaches/me/training-plans")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> SavePlan(SaveTrainingPlanRequest request, CancellationToken ct) =>
        Ok(new { Id = await training.SavePlanAsync(CurrentUserId, request, false, ct) });

    [HttpPut("coaches/me/personal-training-plans")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> SavePersonalPlan(SaveTrainingPlanRequest request, CancellationToken ct) =>
        Ok(new { Id = await training.SavePlanAsync(CurrentUserId, request, true, ct) });

    [HttpDelete("coaches/me/training-plans/{planId:long}")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> DeletePlan(long planId, CancellationToken ct)
    { await training.DeletePlanAsync(CurrentUserId, planId, ct); return NoContent(); }

    [HttpGet("coaches/me/progress")]
    [Authorize(Roles = "COACH,MANAGER")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> CoachProgress(CancellationToken ct) => Ok(await training.ListProgressAsync(CurrentUserId, false, ct));

    [HttpPost("coaches/me/progress")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("create_training_plan")]
    public async Task<IActionResult> RecordProgress(RecordProgressRequest request, CancellationToken ct) =>
        Ok(new { Id = await training.RecordProgressAsync(CurrentUserId, request, ct) });

    [HttpGet("members/me/training-plans")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("view_my_progress")]
    public async Task<IActionResult> MemberPlans(CancellationToken ct) => Ok(await training.ListPlansAsync(CurrentUserId, true, ct));

    [HttpGet("members/me/progress")]
    [Authorize(Roles = "MEMBER")]
    [CenterCapability("view_my_progress")]
    public async Task<IActionResult> MemberProgress(CancellationToken ct) => Ok(await training.ListProgressAsync(CurrentUserId, true, ct));

    [HttpPost("coaches/me/notifications")]
    [Authorize(Roles = "COACH")]
    [CenterCapability("view_teaching_schedule")]
    public async Task<IActionResult> Send(SendNotificationRequest request, CancellationToken ct)
    { await training.SendNotificationAsync(CurrentUserId, request, ct); return NoContent(); }

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
