using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.DAL.Authorization;
using AuthRequests = SportsCenterManagement.BLL.DTOs.Auth.Requests;
using AuthResponses = SportsCenterManagement.BLL.DTOs.Auth.Responses;
using MembersRequests = SportsCenterManagement.BLL.DTOs.Members.Requests;
using MembersResponses = SportsCenterManagement.BLL.DTOs.Members.Responses;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class MembersController(
    IMemberService memberService,
    ICoreFlowService coreFlowService,
    SportsCenterManagement.API.Services.IR2StorageService storage,
    ILogger<MembersController> logger) : ControllerBase
{
    /// <summary>Counter staff upload a member's photo: file to R2, key to users.avatar_key.</summary>
    [HttpPost("centers/{centerId:long}/members/{memberId:long}/avatar")]
    [CenterCapability("lookup_member")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadMemberAvatar(long centerId, long memberId, IFormFile? file, CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            if (file is null) return BadRequest(new { message = "Vui lòng chọn ảnh hội viên." });
            var actorId = GetUserId();
            // Authorize before touching storage.
            var memberUserId = await memberService.GetMemberUserIdForStaffAsync(actorId, centerId, memberId, cancellationToken);
            var key = await storage.UploadMemberAvatarAsync(file, memberUserId, 5 * 1024 * 1024, cancellationToken);
            var (member, previousKey) = await memberService.SetMemberAvatarAsync(actorId, centerId, memberId, key, cancellationToken);
            await DeleteQuietlyAsync(previousKey, memberUserId);
            return Ok(member);
        });
    }

    [HttpDelete("centers/{centerId:long}/members/{memberId:long}/avatar")]
    [CenterCapability("lookup_member")]
    public async Task<IActionResult> RemoveMemberAvatar(long centerId, long memberId, CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var actorId = GetUserId();
            var memberUserId = await memberService.GetMemberUserIdForStaffAsync(actorId, centerId, memberId, cancellationToken);
            var (member, previousKey) = await memberService.SetMemberAvatarAsync(actorId, centerId, memberId, null, cancellationToken);
            await DeleteQuietlyAsync(previousKey, memberUserId);
            return Ok(member);
        });
    }

    private async Task DeleteQuietlyAsync(string? key, long memberUserId)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        try { await storage.DeleteMemberAvatarAsync(key, memberUserId, CancellationToken.None); }
        catch (Exception exception) { logger.LogWarning(exception, "Could not delete replaced member avatar {Key}.", key); }
    }

    [HttpGet("members/me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.GetMeAsync(GetUserId(), cancellationToken)));
    }

    [HttpPatch("members/me")]
    [RequirePermission(PermissionCodes.MemberSelfUpdate)]
    public async Task<IActionResult> UpdateMe(MembersRequests.UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.UpdateMeAsync(GetUserId(), request, cancellationToken)));
    }

    [HttpGet("centers/{centerId:long}/members")]
    [CenterCapability("lookup_member")]
    [RequirePermission(PermissionCodes.MemberCenterRead)]
    public async Task<IActionResult> SearchAtCenter(
        long centerId,
        [FromQuery] string? query,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string sortBy = "default",
        [FromQuery] string sortDirection = "asc",
        CancellationToken cancellationToken = default)
    {
        return await Run(async () => Ok(await memberService.SearchAtCenterAsync(
            GetUserId(), centerId, query ?? string.Empty, page, pageSize, sortBy, sortDirection, cancellationToken)));
    }

    [HttpPost("centers/{centerId:long}/members")]
    [CenterCapability("register_counter_package")]
    [RequirePermission(PermissionCodes.MemberCenterCreate)]
    public async Task<IActionResult> CreateAtCenter(long centerId, AuthRequests.RegisterRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () =>
        {
            var member = await memberService.CreateAtCenterAsync(GetUserId(), centerId, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, member);
        });
    }

    [HttpPatch("centers/{centerId:long}/members/{memberId:long}")]
    [RequirePermission(PermissionCodes.MemberCenterUpdate)]
    public async Task<IActionResult> UpdateAtCenter(
        long centerId,
        long memberId,
        MembersRequests.UpdateMemberAtCenterRequest request,
        CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.UpdateAtCenterAsync(
            GetUserId(), centerId, memberId, request, cancellationToken)));
    }

    [HttpPatch("centers/{centerId:long}/members/{memberId:long}/status")]
    [RequirePermission(PermissionCodes.MemberCenterManageStatus)]
    public async Task<IActionResult> SetMemberStatus(
        long centerId,
        long memberId,
        MembersRequests.SetMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.SetMemberStatusAsync(
            GetUserId(), centerId, memberId, request.Status, cancellationToken)));
    }

    [HttpPost("members/me/subscriptions")]
    [RequirePermission(PermissionCodes.SubscriptionSelfCreate)]
    public async Task<IActionResult> CreateMySubscription(MembersRequests.CreateSubscriptionRequest request, CancellationToken cancellationToken)
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

    [HttpPost("members/{memberId:long}/subscriptions")]
    [CenterCapability("register_counter_package")]
    [RequirePermission(PermissionCodes.SubscriptionCenterCreate)]
    public async Task<IActionResult> CreateForMember(
        long memberId,
        MembersRequests.CreateSubscriptionRequest request,
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

    [HttpGet("members/{memberId:long}/subscriptions")]
    [RequirePermission(PermissionCodes.SubscriptionRead)]
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
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
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
