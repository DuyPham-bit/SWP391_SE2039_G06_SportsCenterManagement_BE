using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Filters;

namespace SportsCenterManagement.API.Controllers.Catalog;

/// <summary>
/// Bilingual home page content. Public reads are localized (?lang=vi|en, falling back to vi);
/// admin reads return both languages plus DB-counted sport/facility visibility.
/// Images and videos are uploaded through POST /api/media/upload, which returns the file URL.
/// </summary>
[FlowExceptionFilter]
public sealed class HomeContentController(LandingContentService contentService) : FlowControllerBase
{
    public sealed record PublishRequest(JsonElement Content);
    public sealed record DraftRequest(JsonElement Content, DateTime? ExpectedUpdatedAt = null);

    [HttpGet("home-content")]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PublicHomeContentResponse>> GetPublic([FromQuery] string? lang, [FromQuery] long? centerId, CancellationToken ct) =>
        Ok(await contentService.GetPublicAsync(lang, centerId, ct));

    [HttpGet("admin/home-content")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<AdminHomeContentResponse>> GetAdmin([FromQuery] long? centerId, CancellationToken ct) =>
        Ok(await contentService.GetAdminAsync(centerId, CurrentUserId, ct));

    [HttpPut("admin/home-content")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<AdminHomeContentResponse>> Publish(PublishRequest request, [FromQuery] long? centerId, CancellationToken ct) =>
        Ok(await contentService.PublishAsync(request.Content, CurrentUserId, centerId, ct));

    [HttpGet("admin/home-content/draft")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<LandingContentResponse>> GetDraft(CancellationToken ct) =>
        Ok(await contentService.GetAdminDraftAsync(ct));

    [HttpPut("admin/home-content/draft")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<LandingContentResponse>> SaveDraft(DraftRequest request, CancellationToken ct) =>
        Ok(await contentService.SaveDraftAsync(request.Content, CurrentUserId, ct, request.ExpectedUpdatedAt));

    [HttpDelete("admin/home-content/draft")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<IActionResult> DiscardDraft(CancellationToken ct)
    {
        await contentService.DiscardDraftAsync(CurrentUserId, ct);
        return NoContent();
    }

    /// <summary>Saves one section (heroBanner, whyUs, coursesSection, facilitiesSection, zonesLayout, ctaAndFooter, authShowcase).</summary>
    [HttpPut("admin/home-content/{section}")]
    [Authorize(Roles = "ADMIN,MANAGER")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<AdminHomeContentResponse>> SaveSection(string section, PublishRequest request, [FromQuery] long? centerId, CancellationToken ct) =>
        Ok(await contentService.SaveSectionAsync(section, request.Content, CurrentUserId, centerId, ct));
}
