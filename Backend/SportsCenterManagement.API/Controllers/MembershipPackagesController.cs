using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SportsCenterManagement.BLL.DTOs.MembershipPackages;
using SportsCenterManagement.BLL.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/centers/{centerId:long}/membership-packages")]
public sealed class MembershipPackagesController(IMembershipPackageService packageService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPackageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPackageResponse>>> GetActive(
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

    [Authorize(Roles = "Manager")]
    [HttpPost]
    [ProducesResponseType(typeof(MembershipPackageResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        long centerId,
        CreateMembershipPackageRequest request,
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

    [Authorize(Roles = "Manager")]
    [HttpPatch("/api/membership-packages/{packageId:long}")]
    public async Task<IActionResult> Update(
        long packageId,
        UpdateMembershipPackageRequest request,
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
