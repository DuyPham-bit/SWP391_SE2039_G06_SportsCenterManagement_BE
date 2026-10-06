using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using AccessControlRequests = SportsCenterManagement.BLL.DTOs.AccessControl.Requests;
using AccessControlResponses = SportsCenterManagement.BLL.DTOs.AccessControl.Responses;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[RequirePermission(PermissionCodes.RolePermissionManage)]
[Route("api/admin/roles")]
public sealed class RolePermissionsController(IAccessControlService accessControlService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccessControlResponses.RolePermissionMatrixResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMatrix(CancellationToken cancellationToken)
    {
        return Ok(await accessControlService.GetRolePermissionMatrixAsync(cancellationToken));
    }

    [HttpPut("{roleId:long}/permissions")]
    public async Task<IActionResult> ReplacePermissions(
        long roleId,
        AccessControlRequests.ReplaceRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await accessControlService.ReplaceRolePermissionsAsync(
                actorUserId, roleId, request, cancellationToken));
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
            return BadRequest(new { message = exception.Message });
        }
    }
}
