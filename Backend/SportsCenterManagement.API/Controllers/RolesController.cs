using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Roles;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller quản lý vai trò người dùng và phân quyền chức năng trong hệ thống (UC-15).
/// </summary>
[ApiController]
[Authorize(Roles = "Manager")]
[Route("api")]
public sealed class RolesController(IRolePermissionService rolePermissionService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách toàn bộ vai trò người dùng hiện có trong hệ thống kèm thống kê số lượng user và permission.
    /// </summary>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách các vai trò.</returns>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleResponse>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await rolePermissionService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    /// <summary>
    /// Lấy danh sách tất cả các quyền hạn (Permissions) được định nghĩa trong hệ thống.
    /// </summary>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh mục toàn bộ quyền hạn.</returns>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionResponse>>> GetAllPermissions(CancellationToken cancellationToken)
    {
        var permissions = await rolePermissionService.GetAllPermissionsAsync(cancellationToken);
        return Ok(permissions);
    }

    /// <summary>
    /// Lấy danh sách các quyền hạn cụ thể đang được gán cho một vai trò.
    /// </summary>
    /// <param name="roleId">Mã định danh vai trò.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin vai trò và danh sách quyền hạn được gán.</returns>
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

    /// <summary>
    /// Tạo mới một vai trò người dùng trong hệ thống.
    /// </summary>
    /// <param name="request">Tên vai trò và mô tả.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin vai trò vừa tạo.</returns>
    [HttpPost("roles")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleResponse>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = await rolePermissionService.CreateRoleAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetRoles), new { id = role.Id }, role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cập nhật tên và mô tả của một vai trò người dùng.
    /// </summary>
    /// <param name="roleId">Mã định danh vai trò.</param>
    /// <param name="request">Thông tin cập nhật mới.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin vai trò sau khi cập nhật.</returns>
    [HttpPut("roles/{roleId:long}")]
    [ProducesResponseType(typeof(RoleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RoleResponse>> UpdateRole(
        long roleId,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var role = await rolePermissionService.UpdateRoleAsync(roleId, request, cancellationToken);
            return Ok(role);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Xóa một vai trò người dùng khỏi hệ thống (Chỉ được xóa khi không còn người dùng nào mang vai trò này).
    /// </summary>
    /// <param name="roleId">Mã định danh vai trò cần xóa.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpDelete("roles/{roleId:long}")]
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

    /// <summary>
    /// Cập nhật lại danh sách quyền hạn (Permissions) được gán cho một vai trò.
    /// </summary>
    /// <param name="roleId">Mã định danh vai trò.</param>
    /// <param name="request">Danh sách ID các quyền hạn mới.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin vai trò cùng danh sách quyền hạn đã cập nhật.</returns>
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
        }
    }
}
