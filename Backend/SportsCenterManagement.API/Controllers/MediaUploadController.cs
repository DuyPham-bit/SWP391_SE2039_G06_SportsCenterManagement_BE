using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.API.Services;
using SportsCenterManagement.BLL.DTOs.Media;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Controllers;

[Route("api/media")]
[Authorize(Roles = "COACH,MANAGER,ADMIN")]
[FlowExceptionFilter]
public sealed class MediaUploadController(
    IR2StorageService storageService,
    SportsCenterDbContext dbContext) : FlowControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadMediaResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UploadMediaResultDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new FlowException(400, "A media file is required.");
        if (file.Length > ImageUploadValidator.MaxFileSize)
            throw new FlowException(400, "Media files cannot exceed 50 MB.");

        var key = await storageService.UploadMediaAsync(file, ct);
        var media = await dbContext.UploadedMedias.SingleAsync(item =>
            item.DriveFileId == MediaAccessScope.R2FileIdPrefix + key, ct);
        return Ok(ToDto(media));
    }

    [HttpGet("files")]
    public async Task<ActionResult<List<UploadMediaResultDto>>> GetFiles(CancellationToken ct = default)
    {
        var scope = await MediaAccessScope.ForUser(dbContext, CurrentUserId, ct);
        return Ok(await MediaQuery(scope).ToListAsync(ct));
    }

    [HttpGet("status")]
    public async Task<ActionResult<MediaStorageStatusDto>> GetStatus(CancellationToken ct = default)
    {
        var scope = await MediaAccessScope.ForUser(dbContext, CurrentUserId, ct);
        var totalMedia = await scope.Library(dbContext).CountAsync(ct);
        return Ok(new MediaStorageStatusDto(true, totalMedia, "Cloudflare R2"));
    }

    [HttpGet("recent")]
    public async Task<ActionResult<List<UploadMediaResultDto>>> GetRecent([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var scope = await MediaAccessScope.ForUser(dbContext, CurrentUserId, ct);
        return Ok(await MediaQuery(scope).Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct));
    }

    [HttpPost("migrate-legacy")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<LegacyMediaMigrationResult>> MigrateLegacyMedia(CancellationToken ct = default) =>
        Ok(await storageService.MigrateLegacyMediaAsync(ct));

    private IQueryable<UploadMediaResultDto> MediaQuery(MediaAccessScope scope) =>
        scope.Library(dbContext).OrderByDescending(media => media.CreatedAt).Select(media => new UploadMediaResultDto(
            media.Id, media.DriveFileId, media.FileUrl, media.FileName, media.MimeType,
            media.FileSize, media.CreatedAt, media.FileUrl));

    private static UploadMediaResultDto ToDto(UploadedMedia media) => new(
        media.Id, media.DriveFileId, media.FileUrl, media.FileName, media.MimeType,
        media.FileSize, media.CreatedAt, media.FileUrl);
}
