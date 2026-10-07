using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.MembershipPackages;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[FlowExceptionFilter]
[Route("api/centers/{centerId:long}/membership-packages")]
public sealed class MembershipPackagesController(
    IMembershipPackageService packageService,
    SportsCenterDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPackageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPackageResponse>>> GetActive(
        long centerId,
        CancellationToken cancellationToken)
    {
        if (!await db.Centers.AnyAsync(center => center.Id == centerId && center.Status == "Active", cancellationToken))
            return NotFound(new { message = "Cơ sở không tồn tại hoặc đã ngừng hoạt động." });
        var packages = await packageService.GetActivePackagesAsync(centerId, cancellationToken);
        return Ok(packages);
    }

    [HttpGet("all")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPackageResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPackageResponse>>> GetAll(
        long centerId,
        CancellationToken cancellationToken)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, cancellationToken);
        var packages = await packageService.GetPackagesAsync(centerId, cancellationToken);
        return Ok(packages);
    }

    [HttpPost]
    [Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(MembershipPackageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipPackageResponse>> Create(
        long centerId,
        [FromBody] SaveMembershipPackageRequest request,
        CancellationToken cancellationToken)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, cancellationToken);
        try
        {
            var package = await packageService.CreatePackageAsync(CenterScope.GetUserId(User), centerId, request, cancellationToken);
            return CreatedAtAction(nameof(GetAll), new { centerId }, package);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Tên gói đã được dùng tại trung tâm này." });
        }
    }

    [HttpPut("{packageId:long}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(MembershipPackageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipPackageResponse>> Update(
        long centerId,
        long packageId,
        [FromBody] SaveMembershipPackageRequest request,
        CancellationToken cancellationToken)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, cancellationToken);
        try
        {
            return Ok(await packageService.UpdatePackageAsync(
                CenterScope.GetUserId(User), centerId, packageId, request, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Tên gói đã được dùng tại trung tâm này." });
        }
    }

    [HttpPatch("{packageId:long}/status")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    [ProducesResponseType(typeof(MembershipPackageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipPackageResponse>> SetStatus(
        long centerId,
        long packageId,
        [FromBody] Requests.SetMembershipPackageStatusRequest request,
        CancellationToken cancellationToken)
    {
        await CenterScope.EnsureCenterAccessAsync(db, User, centerId, cancellationToken);
        try
        {
            return Ok(await packageService.SetPackageStatusAsync(
                CenterScope.GetUserId(User), centerId, packageId, request.Status, cancellationToken));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("/api/membership-packages/{packageId:long}")]
    [RequirePermission(PermissionCodes.MembershipPackagesManage)]
    public async Task<IActionResult> UpdateFromDirectRoute(
        long packageId,
        [FromBody] Requests.UpdateMembershipPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(await packageService.UpdateAsync(userId, packageId, request, cancellationToken));
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
}

