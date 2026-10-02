using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api")]
public sealed class ClassesController(IClassService classService) : ControllerBase
{
    [HttpGet("centers/{centerId:long}/classes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(
        long centerId,
        CancellationToken cancellationToken)
    {
        var classes = await classService.GetPublishedClassesAsync(centerId, cancellationToken);
        return Ok(classes);
    }

    [HttpPost("classes/{classId:long}/coaches")]
<<<<<<< Updated upstream
    [ProducesResponseType(typeof(ClassCoachResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(
        long classId,
        [FromBody] AssignCoachRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await classService.AssignCoachToClassAsync(classId, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
    [Authorize(Roles = "ADMIN,MANAGER")]
    [ProducesResponseType(typeof(ClassCoachResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(long classId,
        [FromBody] AssignCoachRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await classService.AssignCoachToClassAsync(classId, request, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }

    [HttpGet("classes/{classId:long}/coaches")]
<<<<<<< Updated upstream
    [ProducesResponseType(typeof(IReadOnlyList<ClassCoachResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(
        long classId,
=======
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(long classId,
>>>>>>> Stashed changes
        CancellationToken cancellationToken)
    {
        try
        {
<<<<<<< Updated upstream
            var coaches = await classService.GetAssignedCoachesAsync(classId, cancellationToken);
            return Ok(coaches);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
            return Ok(await classService.GetAssignedCoachesAsync(classId, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }

    [HttpDelete("classes/{classId:long}/coaches/{coachId:long}")]
<<<<<<< Updated upstream
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnassignCoach(
        long classId,
        long coachId,
=======
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> UnassignCoach(long classId, long coachId,
>>>>>>> Stashed changes
        CancellationToken cancellationToken)
    {
        try
        {
            await classService.UnassignCoachFromClassAsync(classId, coachId, cancellationToken);
            return NoContent();
        }
<<<<<<< Updated upstream
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }
}
