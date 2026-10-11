using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Authorization;
using SportsCenterManagement.BLL.DTOs.Support;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Authorize(Roles = "RECEPTIONIST,MANAGER")]
[CenterCapability("lookup_member")]
public sealed class SupportRequestsController(SupportRequestService service) : FlowControllerBase
{
    [HttpGet("centers/{centerId:long}/support-requests")]
    public async Task<ActionResult<SupportPage>> List(long centerId, [FromQuery] string? query, [FromQuery] string? status,
        CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
        Ok(await service.List(CurrentUserId, centerId, query, status, page, pageSize, ct));

    [HttpGet("centers/{centerId:long}/support-requests/{id:long}")]
    public async Task<ActionResult<SupportDetails>> Get(long centerId, long id, CancellationToken ct) =>
        Ok(await service.Get(CurrentUserId, centerId, id, ct));

    [HttpPost("centers/{centerId:long}/support-requests")]
    public async Task<ActionResult<SupportDetails>> Create(long centerId, CreateSupportRequest body, CancellationToken ct) =>
        Ok(await service.Create(CurrentUserId, centerId, body, ct));

    [HttpPatch("centers/{centerId:long}/support-requests/{id:long}")]
    public async Task<ActionResult<SupportDetails>> Update(long centerId, long id, UpdateSupportRequest body, CancellationToken ct) =>
        Ok(await service.Update(CurrentUserId, centerId, id, body, ct));
}
