using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize(Roles = "MEMBER")]
[Route("api")]
public sealed class ClassEnrollmentsController(SportsCenterDbContext db, ICoreFlowService coreFlowService) : ControllerBase
{
    [HttpGet("members/me/enrollments")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(cancellationToken);
        if (memberId is null) return NotFound(new { message = "Không tìm thấy hồ sơ Member." });
        var result = await (
            from enrollment in db.ClassEnrollments
            join classEntity in db.Classes on enrollment.ClassId equals classEntity.Id
            where enrollment.MemberId == memberId.Value
            orderby enrollment.RegisteredAt descending
            select new { enrollment.Id, enrollment.ClassId, classEntity.Name, enrollment.SubscriptionId, enrollment.RegisteredAt, enrollment.CancelledAt, enrollment.Status, enrollment.CancellationReason }
        ).ToListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPost("classes/{classId:long}/enrollments")]
    public async Task<IActionResult> Enroll(long classId, [FromBody] ClassEnrollmentRequest request, CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(cancellationToken);
        if (memberId is null) return NotFound(new { message = "Không tìm thấy hồ sơ Member." });
        try
        {
            var enrollment = await coreFlowService.EnrollMemberAsync(classId, memberId.Value, request.SubscriptionId, GetCurrentUserId(), cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new { enrollment.Id, enrollment.ClassId, enrollment.MemberId, enrollment.SubscriptionId, enrollment.RegisteredAt, enrollment.Status });
        }
        catch (InvalidOperationException ex)
        {
            var conflict = ex.Message.Contains("full", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already enrolled", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("allowance", StringComparison.OrdinalIgnoreCase);
            var status = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            return StatusCode(status, new { message = ex.Message });
        }
    }

    [HttpDelete("classes/{classId:long}/enrollments/{enrollmentId:long}")]
    public async Task<IActionResult> Cancel(long classId, long enrollmentId, [FromBody] CancelClassEnrollmentRequest? request, CancellationToken cancellationToken)
    {
        var memberId = await GetMemberIdAsync(cancellationToken);
        if (memberId is null) return NotFound(new { message = "Không tìm thấy hồ sơ Member." });
        var exists = await db.ClassEnrollments.AnyAsync(x => x.Id == enrollmentId && x.ClassId == classId && x.MemberId == memberId.Value, cancellationToken);
        if (!exists) return NotFound(new { message = "Không tìm thấy ghi danh." });
        try
        {
            var enrollment = await coreFlowService.CancelClassEnrollmentAsync(classId, memberId.Value, request?.Reason, cancellationToken);
            return Ok(new { enrollment.Id, enrollment.ClassId, enrollment.Status, enrollment.CancelledAt, enrollment.CancellationReason });
        }
        catch (InvalidOperationException ex)
        {
            var status = ex.Message.Contains("2 tiếng", StringComparison.Ordinal) ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest;
            return StatusCode(status, new { message = ex.Message });
        }
    }

    private async Task<long?> GetMemberIdAsync(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        return userId > 0
            ? await db.MemberProfiles.Where(x => x.UserId == userId).Select(x => (long?)x.Id).SingleOrDefaultAsync(cancellationToken)
            : null;
    }

    private long GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : 0;
    }
}
