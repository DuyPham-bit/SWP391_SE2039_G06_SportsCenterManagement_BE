using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AuditResponses = SportsCenterManagement.BLL.DTOs.Audit.Responses;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
public sealed class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet("/api/centers/{centerId:long}/audit-logs")]
    [Authorize(Policy = "Permission:audit.center.read")]
    [ProducesResponseType(typeof(AuditResponses.AuditLogPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenterLogs(
        long centerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await Run(() => auditLogService.GetCenterLogsAsync(
            GetUserId(), centerId, page, pageSize, cancellationToken));
    }

    [HttpGet("/api/admin/audit-logs")]
    [Authorize(Roles = RoleNames.SystemAdmin)]
    [ProducesResponseType(typeof(AuditResponses.AuditLogPageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSystemLogs(
        [FromQuery] long? centerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await Run(() => auditLogService.GetSystemLogsAsync(
            GetUserId(), centerId, page, pageSize, cancellationToken));
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<IActionResult> Run(Func<Task<AuditResponses.AuditLogPageResponse>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
