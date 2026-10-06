using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Authorization;
using MembershipPackagesRequests = SportsCenterManagement.BLL.DTOs.MembershipPackages.Requests;
using MembershipPackagesResponses = SportsCenterManagement.BLL.DTOs.MembershipPackages.Responses;
using SportsCenterManagement.BLL.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/centers/{centerId:long}/membership-packages")]
public sealed class MembershipPackagesController(IMembershipPackageService packageService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPackagesResponses.MembershipPackageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPackagesResponses.MembershipPackageResponse>>> GetActive(
        long centerId,
        CancellationToken cancellationToken)
    {
        try
        {
            var packages = await packageService.GetActivePackagesAsync(centerId, cancellationToken);
            return Ok(packages);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [RequirePermission(PermissionCodes.MembershipPackagesManage)]
    [HttpPost]
    [ProducesResponseType(typeof(MembershipPackagesResponses.MembershipPackageResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        long centerId,
        MembershipPackagesRequests.CreateMembershipPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await packageService.CreateAsync(GetUserId(), centerId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Tên gói đã được dùng tại trung tâm này." });
        }
    }

    [RequirePermission(PermissionCodes.MembershipPackagesManage)]
    [HttpPatch("/api/membership-packages/{packageId:long}")]
    public async Task<IActionResult> Update(
        long packageId,
        MembershipPackagesRequests.UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await packageService.UpdateAsync(GetUserId(), packageId, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Tên gói đã được dùng tại trung tâm này." });
        }
    }

    private long GetUserId()
    {
        return long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
