using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SportsCenterManagement.API.Models;
using SportsCenterManagement.API.Services;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Authorize]
[Route("api/r2-images")]
public sealed class R2ImageStorageController(
    IR2StorageService storageService,
    IOptions<R2StorageOptions> options) : ControllerBase
{
    [HttpPost("dashboard")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult<R2ImageUploadResponse>> UploadDashboardImage(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new ProblemDetails { Detail = "Vui lòng chọn ảnh." });
        var key = await storageService.UploadImageAsync(file, "dashboard", 10 * 1024 * 1024, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, BuildResponse(key));
    }

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<R2ImageUploadResponse>> UploadAvatar(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null) return BadRequest(new ProblemDetails { Detail = "Vui lòng chọn ảnh đại diện." });
        var userId = CurrentUserId();
        var key = await storageService.UploadImageAsync(file, $"avatars/{userId}", 5 * 1024 * 1024, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, BuildResponse(key));
    }

    [HttpDelete("dashboard")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<IActionResult> DeleteDashboardImage([FromQuery] string key, CancellationToken cancellationToken)
    {
        if (!key.StartsWith("dashboard/", StringComparison.Ordinal)) return BadRequest();
        await storageService.DeleteAsync(key, cancellationToken);
        return NoContent();
    }

    [HttpDelete("avatar")]
    public async Task<IActionResult> DeleteUploadedAvatar([FromQuery] string key, CancellationToken cancellationToken)
    {
        var prefix = $"avatars/{CurrentUserId()}/";
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) return BadRequest();
        await storageService.DeleteAsync(key, cancellationToken);
        return NoContent();
    }

    private R2ImageUploadResponse BuildResponse(string key) =>
        new(key, $"{options.Value.PublicDomain.TrimEnd('/')}/{key}");

    private long CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return long.TryParse(claim, out var userId) ? userId : throw new UnauthorizedAccessException();
    }
}

public sealed record R2ImageUploadResponse(string Key, string PublicUrl);
