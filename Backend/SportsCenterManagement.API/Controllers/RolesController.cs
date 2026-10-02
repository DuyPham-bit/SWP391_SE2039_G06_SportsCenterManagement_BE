<<<<<<< Updated upstream
=======
using System.Security.Claims;
>>>>>>> Stashed changes
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

<<<<<<< Updated upstream
/// <summary>Endpoints for system roles and permissions management (UC-15).</summary>
[ApiController]
[Route("api")]
public sealed class RolesController(IRolePermissionService rolePermissionService) : ControllerBase
{
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await rolePermissionService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetAllPermissions(CancellationToken cancellationToken)
    {
        var permissions = await rolePermissionService.GetAllPermissionsAsync(cancellationToken);
        return Ok(permissions);
    }

    [HttpGet("roles/{roleId:long}/permissions")]
    [ProducesResponseType(typeof(RolePermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RolePermissionsResponse>> GetRolePermissions(
        long roleId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await rolePermissionService.GetRolePermissionsAsync(roleId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("roles")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleResponse>> CreateRole(
        [FromBody] CreateRoleRequest request,
=======
[ApiController]
[Route("api")]
[Authorize(Roles = "ADMIN,MANAGER")]
public sealed class RolesController(IRolePermissionService service) : ControllerBase
{
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken cancellationToken) =>
        Ok(await service.GetRolesAsync(cancellationToken));

    [HttpGet("permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetPermissions(CancellationToken cancellationToken) =>
        Ok(await service.GetPermissionsAsync(cancellationToken));

    [HttpPost("roles")]
    public async Task<ActionResult<RoleResponse>> CreateRole(SaveRoleRequest request,
>>>>>>> Stashed changes
        CancellationToken cancellationToken)
    {
        try
        {
<<<<<<< Updated upstream
            var role = await rolePermissionService.CreateRoleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
            var role = await service.CreateRoleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRoles), new { }, role);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }

    [HttpPut("roles/{roleId:long}")]
<<<<<<< Updated upstream
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleResponse>> UpdateRole(
        long roleId,
        [FromBody] UpdateRoleRequest request,
=======
    public async Task<ActionResult<RoleResponse>> UpdateRole(long roleId, SaveRoleRequest request,
>>>>>>> Stashed changes
        CancellationToken cancellationToken)
    {
        try
        {
<<<<<<< Updated upstream
            var role = await rolePermissionService.UpdateRoleAsync(roleId, request, cancellationToken);
            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!long.TryParse(id, out var actorUserId)) return Unauthorized();
            return Ok(await service.UpdateRoleAsync(roleId, request, actorUserId, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }

    [HttpDelete("roles/{roleId:long}")]
<<<<<<< Updated upstream
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteRole(
        long roleId,
        CancellationToken cancellationToken)
    {
        try
        {
            await rolePermissionService.DeleteRoleAsync(roleId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("roles/{roleId:long}/permissions")]
    [ProducesResponseType(typeof(RolePermissionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RolePermissionsResponse>> UpdateRolePermissions(
        long roleId,
        [FromBody] UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            long? currentUserId = null;
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (long.TryParse(userIdClaim, out var parsedId))
            {
                currentUserId = parsedId;
            }

            var result = await rolePermissionService.UpdateRolePermissionsAsync(
                roleId, request, currentUserId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
=======
    public async Task<IActionResult> DeleteRole(long roleId, CancellationToken cancellationToken)
    {
        try
        {
            await service.DeleteRoleAsync(roleId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
>>>>>>> Stashed changes
        }
    }
}
