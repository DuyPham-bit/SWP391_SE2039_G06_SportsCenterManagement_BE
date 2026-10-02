using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SportsCenterManagement.BLL.DTOs.MembershipPackages;
using SportsCenterManagement.BLL.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller quản lý danh mục và thông tin các gói tập thể thao tại trung tâm.
/// </summary>
[ApiController]
[Route("api/centers/{centerId:long}/membership-packages")]
public sealed class MembershipPackagesController(IMembershipPackageService packageService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách các gói tập thể thao đang ở trạng thái hoạt động (Active) tại trung tâm.
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách các gói tập có thể đăng ký.</returns>
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

    /// <summary>
    /// Tạo mới một gói tập thể thao cho trung tâm (Dành cho Quản lý trung tâm).
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="request">Thông tin cấu hình gói tập (Tên, Giá, Thời hạn, Quyền lợi,...).</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin chi tiết gói tập vừa tạo.</returns>
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

    /// <summary>
    /// Cập nhật thông tin cấu hình một gói tập thể thao (Dành cho Quản lý trung tâm).
    /// </summary>
    /// <param name="packageId">Mã định danh gói tập cần sửa.</param>
    /// <param name="request">Thông tin cập nhật mới.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin gói tập sau khi cập nhật.</returns>
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
