using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "MEMBER,MANAGER,ADMIN,RECEPTIONIST")]
public sealed class ClassesController(IClassService classService, ICenterAccessService centerAccessService) : FlowControllerBase, IAsyncActionFilter
{
    [AllowAnonymous]
    [HttpGet("centers/{centerId:long}/classes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(long centerId, CancellationToken cancellationToken)
    {
        if (!await centerAccessService.IsActiveCenterAsync(centerId, cancellationToken))
            throw new InvalidOperationException("Cơ sở không tồn tại hoặc đã ngừng hoạt động.") { Data = { ["StatusCode"] = 404 } };
        if (User.Identity?.IsAuthenticated == true && (User.IsInRole("MANAGER") || User.IsInRole("RECEPTIONIST")))
            await centerAccessService.EnsureCenterAccessAsync(CurrentUserId, User.IsInRole("ADMIN"), centerId, cancellationToken);
        return Ok(await classService.GetPublishedClassesAsync(centerId, cancellationToken));
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpPost("classes/{classId:long}/coaches")]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(long classId, [FromBody] AssignCoachRequest request, CancellationToken cancellationToken)
    {
        await centerAccessService.EnsureClassAccessAsync(CurrentUserId, User.IsInRole("ADMIN"), classId, cancellationToken);
        try { return Ok(await classService.AssignCoachToClassAsync(CurrentUserId, classId, request, cancellationToken)); }
        catch (InvalidOperationException ex) when (ex.Data["StatusCode"] is int) { return StatusCode((int)ex.Data["StatusCode"]!, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpGet("classes/{classId:long}/coaches")]
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(long classId, CancellationToken cancellationToken)
    {
        await centerAccessService.EnsureClassAccessAsync(CurrentUserId, User.IsInRole("ADMIN"), classId, cancellationToken);
        try { return Ok(await classService.GetAssignedCoachesAsync(classId, cancellationToken)); }
        catch (InvalidOperationException ex) when (ex.Data["StatusCode"] is int) { return StatusCode((int)ex.Data["StatusCode"]!, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpDelete("classes/{classId:long}/coaches/{coachId:long}")]
    public async Task<IActionResult> UnassignCoach(long classId, long coachId, CancellationToken cancellationToken)
    {
        await centerAccessService.EnsureClassAccessAsync(CurrentUserId, User.IsInRole("ADMIN"), classId, cancellationToken);
        try
        {
            await classService.UnassignCoachFromClassAsync(CurrentUserId, classId, coachId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Data["StatusCode"] is int) { return StatusCode((int)ex.Data["StatusCode"]!, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
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
