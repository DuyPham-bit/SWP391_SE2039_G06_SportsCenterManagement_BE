using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "MEMBER,MANAGER,ADMIN,RECEPTIONIST")]
public sealed class ClassesController(IClassService classService, SportsCenterDbContext? db = null) : FlowControllerBase
{
    [AllowAnonymous]
    [HttpGet("centers/{centerId:long}/classes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(long centerId, CancellationToken cancellationToken)
    {
        if (db is not null)
        {
            if (!await db.Centers.AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken))
                throw FlowException.NotFound("Cơ sở không tồn tại hoặc đã ngừng hoạt động.");
            if (User.Identity?.IsAuthenticated == true && (User.IsInRole("MANAGER") || User.IsInRole("RECEPTIONIST")))
                await CenterScope.EnsureCenterAccessAsync(db, User, centerId, cancellationToken);
        }
        return Ok(await classService.GetPublishedClassesAsync(centerId, cancellationToken));
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpPost("classes/{classId:long}/coaches")]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(long classId, [FromBody] AssignCoachRequest request, CancellationToken cancellationToken)
    {
        if (db is not null)
        {
            await CenterScope.EnsureClassAccessAsync(db, User, classId, cancellationToken);
        }
        try { return Ok(await classService.AssignCoachToClassAsync(classId, request, cancellationToken)); }
        catch (FlowException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpGet("classes/{classId:long}/coaches")]
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(long classId, CancellationToken cancellationToken)
    {
        if (db is not null)
        {
            await CenterScope.EnsureClassAccessAsync(db, User, classId, cancellationToken);
        }
        try { return Ok(await classService.GetAssignedCoachesAsync(classId, cancellationToken)); }
        catch (FlowException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [Authorize(Roles = "MANAGER,ADMIN")]
    [HttpDelete("classes/{classId:long}/coaches/{coachId:long}")]
    public async Task<IActionResult> UnassignCoach(long classId, long coachId, CancellationToken cancellationToken)
    {
        if (db is not null)
        {
            await CenterScope.EnsureClassAccessAsync(db, User, classId, cancellationToken);
        }
        try
        {
            await classService.UnassignCoachFromClassAsync(classId, coachId, cancellationToken);
            return NoContent();
        }
        catch (FlowException ex) { return StatusCode(ex.StatusCode, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
