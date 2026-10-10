using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "MEMBER,MANAGER,ADMIN,RECEPTIONIST")]
[Route("api")]
public sealed class ClassesController(IClassService classService, SportsCenterDbContext db) : CenterScopedControllerBase(db)
{
    [HttpGet("centers/{centerId:long}/classes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(
        long centerId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessCenterAsync(centerId, cancellationToken)) return NotFound();
        var classes = await classService.GetPublishedClassesAsync(centerId, cancellationToken);
        return Ok(classes);
    }

    [HttpPost("classes/{classId:long}/coaches")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(ClassCoachResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(
        long classId,
        [FromBody] AssignCoachRequest request,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClassAsync(classId, cancellationToken)) return NotFound();
        try
        {
            var result = await classService.AssignCoachToClassAsync(classId, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("classes/{classId:long}/coaches")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCoachResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(
        long classId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClassAsync(classId, cancellationToken)) return NotFound();
        try
        {
            var coaches = await classService.GetAssignedCoachesAsync(classId, cancellationToken);
            return Ok(coaches);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:long}/coaches/{coachId:long}")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnassignCoach(
        long classId,
        long coachId,
        CancellationToken cancellationToken)
    {
        if (!await CanAccessClassAsync(classId, cancellationToken)) return NotFound();
        try
        {
            await classService.UnassignCoachFromClassAsync(classId, coachId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
