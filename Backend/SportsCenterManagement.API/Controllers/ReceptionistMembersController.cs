using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Authentication;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Members.Activation;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Authorization;

namespace SportsCenterManagement.API.Controllers;

/// <summary>Counter quick-create of members with a phone-number default password and email activation link.</summary>
[ApiController]
[Authorize]
[Route("api/receptionist/members")]
public sealed class ReceptionistMembersController(
    IMemberActivationService activationService,
    PasswordResetEmailSender emailSender,
    IConfiguration configuration) : ControllerBase
{
    private static readonly int CooldownSeconds = (int)MemberActivationService.ResendCooldown.TotalSeconds;

    [HttpPost("quick-create")]
    [CenterCapability("register_counter_package")]
    [RequirePermission(PermissionCodes.MemberCenterCreate)]
    [ProducesResponseType(typeof(ActivationDispatchResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> QuickCreate(QuickCreateMemberRequest request, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var issue = await activationService.QuickCreateAsync(GetUserId(), request, cancellationToken);
            var sent = await DispatchAsync(issue, cancellationToken);
            var body = new ActivationDispatchResponse(issue.Member, sent,
                sent
                    ? "Đã tạo tài khoản và gửi email xác nhận cho hội viên."
                    : "Đã tạo tài khoản nhưng chưa gửi được email xác nhận. Vui lòng bấm \"Gửi lại mail\".",
                sent ? CooldownSeconds : 0);
            return StatusCode(StatusCodes.Status201Created, body);
        });

    [HttpPost("{memberId:long}/resend-verification")]
    [CenterCapability("register_counter_package")]
    [RequirePermission(PermissionCodes.MemberCenterCreate)]
    [ProducesResponseType(typeof(ActivationDispatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> Resend(long memberId, CancellationToken cancellationToken) =>
        Run(async () =>
        {
            var issue = await activationService.ReissueAsync(GetUserId(), memberId, cancellationToken);
            if (!await DispatchAsync(issue, cancellationToken))
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { message = "Chưa gửi được email xác nhận. Vui lòng thử lại sau." });
            return Ok(new ActivationDispatchResponse(issue.Member, true,
                "Đã gửi lại email xác nhận cho hội viên.", CooldownSeconds));
        });

    /// <summary>Polled by the counter screen so pending badges flip as soon as the member activates.</summary>
    [HttpGet("verification-status")]
    [CenterCapability("lookup_member")]
    [RequirePermission(PermissionCodes.MemberCenterRead)]
    [ProducesResponseType(typeof(IReadOnlyList<MemberVerificationStatus>), StatusCodes.Status200OK)]
    public Task<IActionResult> VerificationStatus([FromQuery] long[] ids, CancellationToken cancellationToken) =>
        Run(async () => Ok(await activationService.GetVerificationStatusAsync(GetUserId(), ids, cancellationToken)));

    private async Task<bool> DispatchAsync(ActivationIssue issue, CancellationToken cancellationToken)
    {
        var link = ActivationLinkBuilder.Build(configuration, Request, issue.RawToken);
        var sent = link is not null && emailSender.IsConfigured && await emailSender.SendMemberActivationAsync(
            issue.UserId, issue.Member.FullName, issue.Member.Phone ?? string.Empty, issue.Member.Email,
            link, issue.ExpiresAt, $"member-activation-{MemberActivationService.HashToken(issue.RawToken)[..32]}",
            cancellationToken);
        // An undelivered link is useless; revoking it also lifts the resend cooldown immediately.
        if (!sent) await activationService.RevokeAsync(issue.RawToken, CancellationToken.None);
        return sent;
    }

    private long GetUserId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<IActionResult> Run(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (BusinessException exception) when (exception.Code == "RESEND_COOLDOWN")
        {
            Response.Headers.RetryAfter = exception.ProposedHandling;
            return StatusCode(StatusCodes.Status429TooManyRequests, new
            {
                code = exception.Code,
                message = exception.Message,
                retryAfterSeconds = int.TryParse(exception.ProposedHandling, out var wait) ? wait : CooldownSeconds
            });
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
            return Conflict(new { message = "Số điện thoại hoặc email đã được sử dụng." });
        }
    }
}
