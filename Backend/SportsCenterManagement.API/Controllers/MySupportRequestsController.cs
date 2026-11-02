using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.BLL.DTOs.Support;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "MEMBER,COACH")]
[FlowExceptionFilter]
public sealed class MySupportRequestsController(SupportRequestService service) : FlowControllerBase
{
    [HttpGet("support-requests/me")]
    public async Task<ActionResult<SupportPage>> Mine([FromQuery] string? status, CancellationToken ct,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await service.ListMine(CurrentUserId, status, page, pageSize, ct));

    [HttpGet("support-requests/me/{id:long}")]
    public async Task<ActionResult<SupportDetails>> Get(long id, CancellationToken ct) =>
        Ok(await service.GetMine(CurrentUserId, id, ct));

    [HttpPost("support-requests/me")]
    public async Task<ActionResult<SupportDetails>> Submit(SubmitSupportRequest body, CancellationToken ct)
    {
        var result = await service.CreateMine(CurrentUserId, body, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Request.Id }, result);
    }

    [HttpPost("support-requests/me/{id:long}/messages")]
    public async Task<ActionResult<SupportDetails>> Reply(long id, ReplyToSupportRequest body, CancellationToken ct) =>
        Ok(await service.ReplyMine(CurrentUserId, id, body, ct));
}
