using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller quản lý danh mục lớp học và phân công Huấn luyện viên phụ trách (UC-13).
/// </summary>
[ApiController]
[Route("api")]
public sealed class ClassesController(IClassService classService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách các lớp học đã mở và công bố tại một trung tâm thể thao.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách các lớp học trong danh mục.</returns>
    [HttpGet("centers/{centerId:long}/classes")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(
        long centerId,
        CancellationToken cancellationToken)
    {
        var classes = await classService.GetPublishedClassesAsync(centerId, cancellationToken);
        return Ok(classes);
    }

    /// <summary>
    /// Phân công Huấn luyện viên (Coach) vào một lớp học (Dành cho Quản lý / Admin).
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="request">Thông tin HLV và vai trò (Chính / Phụ).</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin phân công lớp học của HLV.</returns>
    [HttpPost("classes/{classId:long}/coaches")]
    [Authorize(Roles = "Manager,Admin")]
    [ProducesResponseType(typeof(ClassCoachResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClassCoachResponse>> AssignCoach(
        long classId,
        [FromBody] AssignCoachRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = TryGetCenterId(out var centerId)
                ? await classService.AssignCoachToClassAsync(centerId, classId, request, cancellationToken)
                : await classService.AssignCoachToClassAsync(classId, request, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lấy danh sách các Huấn luyện viên hiện được phân công cho một lớp học.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách Huấn luyện viên phụ trách lớp.</returns>
    [HttpGet("classes/{classId:long}/coaches")]
    [Authorize(Roles = "Manager,Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCoachResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ClassCoachResponse>>> GetAssignedCoaches(
        long classId,
        CancellationToken cancellationToken)
    {
        try
        {
            var coaches = TryGetCenterId(out var centerId)
                ? await classService.GetAssignedCoachesAsync(centerId, classId, cancellationToken)
                : await classService.GetAssignedCoachesAsync(classId, cancellationToken);
            return Ok(coaches);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Hủy phân công một Huấn luyện viên khỏi lớp học.
    /// </summary>
    /// <param name="classId">Mã định danh lớp học.</param>
    /// <param name="coachId">Mã định danh Huấn luyện viên.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    [HttpDelete("classes/{classId:long}/coaches/{coachId:long}")]
    [Authorize(Roles = "Manager,Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UnassignCoach(
        long classId,
        long coachId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (TryGetCenterId(out var centerId))
            {
                await classService.UnassignCoachFromClassAsync(centerId, classId, coachId, cancellationToken);
            }
            else
            {
                await classService.UnassignCoachFromClassAsync(classId, coachId, cancellationToken);
            }
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private bool TryGetCenterId(out long centerId)
    {
        centerId = 0;
        var value = User?.FindFirstValue("centerId");
        return !string.IsNullOrEmpty(value) && long.TryParse(value, out centerId);
    }
}
