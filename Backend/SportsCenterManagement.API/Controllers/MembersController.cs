using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.BLL.DTOs.Auth;
using SportsCenterManagement.BLL.DTOs.Members;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class MembersController(IMemberService memberService, ICoreFlowService coreFlowService) : ControllerBase
{
    [HttpGet("members/me")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.GetMeAsync(GetUserId(), cancellationToken)));
    }

    [HttpPatch("members/me")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> UpdateMe(UpdateMemberProfileRequest request, CancellationToken cancellationToken)
    {
        return await Run(async () => Ok(await memberService.UpdateMeAsync(GetUserId(), request, cancellationToken)));
    }

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
