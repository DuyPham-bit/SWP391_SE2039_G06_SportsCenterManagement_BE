using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Route("api/landing-content")]
[FlowExceptionFilter]
public sealed class LandingContentController(LandingContentService contentService) : FlowControllerBase
{
    public sealed record SaveRequest(JsonElement Content);

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<LandingContentResponse>> Get(CancellationToken ct) => Ok(await contentService.GetAsync(ct));

    [HttpPut]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<LandingContentResponse>> Save(SaveRequest request, CancellationToken ct) =>
        Ok(await contentService.SaveAsync(request.Content, CurrentUserId, ct));
}
