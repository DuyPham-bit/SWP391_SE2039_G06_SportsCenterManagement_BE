using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize(Roles = "MEMBER")]
[Route("api")]
public sealed class ClassEnrollmentsController(IClassEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet("members/me/enrollments")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await enrollmentService.GetMyEnrollmentsAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("classes/{classId:long}/enrollments")]
    public async Task<IActionResult> Enroll(
        long classId,
        [FromBody] ClassEnrollmentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var enrollment = await enrollmentService.EnrollAsync(userId, classId, request.SubscriptionId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, enrollment);
    }

    [HttpDelete("classes/{classId:long}/enrollments/{enrollmentId:long}")]
    public async Task<IActionResult> Cancel(
        long classId,
        long enrollmentId,
        [FromBody] CancelClassEnrollmentRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await enrollmentService.CancelEnrollmentAsync(userId, classId, enrollmentId, request?.Reason, cancellationToken);
        return Ok(result);
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : 0;
    }
}
