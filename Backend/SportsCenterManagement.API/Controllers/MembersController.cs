using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller quản lý hồ sơ hội viên và các nghiệp vụ đăng ký gói tập thành viên.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public sealed class MembersController(IMemberService memberService, ICoreFlowService coreFlowService) : ControllerBase
{
    /// <summary>
    /// Lấy thông tin hồ sơ cá nhân của Hội viên đang đăng nhập.
    /// </summary>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Hồ sơ chi tiết của hội viên.</returns>
    [HttpGet("members/me")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.GetMeAsync(GetUserId(), cancellationToken)));
    }

    /// <summary>
    /// Cập nhật thông tin hồ sơ cá nhân của Hội viên đang đăng nhập.
    /// </summary>
    /// <param name="request">Dữ liệu cập nhật (Họ tên, SĐT, Giới tính, Địa chỉ,...).</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Hồ sơ hội viên sau khi cập nhật thành công.</returns>
    [HttpPatch("members/me")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> UpdateMe(UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.UpdateMeAsync(GetUserId(), request, cancellationToken)));
    }

    /// <summary>
    /// Tìm kiếm danh sách hội viên tại một trung tâm thể thao có phân trang (Dành cho Quản lý / Lễ tân).
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="query">Từ khóa tìm kiếm theo tên, email, SĐT hoặc mã hội viên.</param>
    /// <param name="page">Số thứ tự trang (mặc định 1).</param>
    /// <param name="pageSize">Số lượng bản ghi mỗi trang (mặc định 20).</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách phân trang hồ sơ hội viên.</returns>
    [HttpGet("centers/{centerId:long}/members")]
    [Authorize(Roles = "Manager,Receptionist")]
    public async Task<IActionResult> SearchAtCenter(
        long centerId,
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return await Run(async () => Ok(await memberService.SearchAtCenterAsync(
            GetUserId(), centerId, query ?? string.Empty, page, pageSize, cancellationToken)));
    }

    /// <summary>
    /// Tạo mới hồ sơ hội viên trực tiếp tại quầy trung tâm (Dành cho Quản lý / Lễ tân).
    /// </summary>
    /// <param name="centerId">Mã định danh trung tâm thể thao.</param>
    /// <param name="request">Thông tin đăng ký của hội viên.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Hồ sơ hội viên vừa tạo.</returns>
    [HttpPost("centers/{centerId:long}/members")]
    [Authorize(Roles = "Manager,Receptionist")]
    public async Task<IActionResult> CreateAtCenter(long centerId, RegisterRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var member = await memberService.CreateAtCenterAsync(GetUserId(), centerId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, member);
        });
    }

    /// <summary>
    /// Hội viên tự tạo đơn đăng ký gói tập thể thao ở trạng thái chờ thanh toán (Pending).
    /// </summary>
    /// <param name="request">Mã định danh gói tập muốn mua.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin hóa đơn tạm và lượt đăng ký chờ thanh toán.</returns>
    [HttpPost("members/me/subscriptions")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> CreateMySubscription(CreateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var userId = GetUserId();
            await memberService.EnsureCanBuyPackageAsync(userId, request.PackageId, cancellationToken);
            var member = await memberService.GetMeAsync(userId, cancellationToken);
            var pending = await coreFlowService.CreatePendingMembershipAsync(
                member.MemberId, request.PackageId, null, cancellationToken);
            return Created($"/api/members/{member.MemberId}/subscriptions", pending);
        });
    }

    /// <summary>
    /// Lễ tân/Quản lý tạo đơn đăng ký gói tập cho một hội viên tại quầy.
    /// </summary>
    /// <param name="memberId">Mã định danh hội viên.</param>
    /// <param name="request">Mã gói tập đăng ký.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Thông tin đơn đăng ký và hóa đơn chờ thanh toán.</returns>
    [HttpPost("members/{memberId:long}/subscriptions")]
    [Authorize(Roles = "Manager,Receptionist")]
    public async Task<IActionResult> CreateForMember(
        long memberId,
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var actorId = GetUserId();
            await memberService.EnsureCanSellToMemberAsync(actorId, memberId, request.PackageId, cancellationToken);
            var pending = await coreFlowService.CreatePendingMembershipAsync(
                memberId, request.PackageId, actorId, cancellationToken);
            return Created($"/api/members/{memberId}/subscriptions", pending);
        });
    }

    /// <summary>
    /// Lấy danh sách lịch sử các gói tập (Subscriptions) của một hội viên.
    /// </summary>
    /// <param name="memberId">Mã định danh hội viên.</param>
    /// <param name="cancellationToken">Token hủy request.</param>
    /// <returns>Danh sách các gói tập đã và đang đăng ký.</returns>
    [HttpGet("members/{memberId:long}/subscriptions")]
    public async Task<IActionResult> GetSubscriptions(long memberId, CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.GetSubscriptionsAsync(
            GetUserId(), memberId, cancellationToken)));
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
            return Conflict(new { message = "Thông tin thành viên đã được sử dụng." });
        }
    }
}
