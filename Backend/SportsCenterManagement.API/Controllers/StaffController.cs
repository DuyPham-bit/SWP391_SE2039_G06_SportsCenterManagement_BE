using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using StaffRequests = SportsCenterManagement.BLL.DTOs.Staff.Requests;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/centers/{centerId:long}/staff")]
public sealed class StaffController(IStaffManagementService staffManagementService) : ControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCodes.StaffCenterRead)]
    public async Task<IActionResult> Search(
        long centerId,
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await Run(async () => Ok(await staffManagementService.SearchAtCenterAsync(
            GetUserId(), centerId, query, page, pageSize, cancellationToken)));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.StaffCenterCreate)]
    public async Task<IActionResult> Create(
        long centerId,
        StaffRequests.CreateCenterStaffRequest request,
        CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var staff = await staffManagementService.CreateAtCenterAsync(
                GetUserId(), centerId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, staff);
        });
    }

    [HttpPatch("{staffUserId:long}")]
    [RequirePermission(PermissionCodes.StaffCenterUpdate)]
    public async Task<IActionResult> Update(
        long centerId,
        long staffUserId,
        StaffRequests.UpdateCenterStaffRequest request,
        CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await staffManagementService.UpdateAtCenterAsync(
            GetUserId(), centerId, staffUserId, request, cancellationToken)));
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Username, email, phone, or staff code is already in use." });
        }
    }
}
