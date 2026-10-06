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
public sealed class MembersController(IMemberService memberService, ICoreFlowService coreFlowService) : ControllerBase
{
    [HttpGet("members/me")]
    [RequirePermission(PermissionCodes.MemberSelfRead)]
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
    [RequirePermission(PermissionCodes.MemberCenterRead)]
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

    [HttpPost("centers/{centerId:long}/members")]
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
        MembersRequests.UpdateMemberProfileRequest request,
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
