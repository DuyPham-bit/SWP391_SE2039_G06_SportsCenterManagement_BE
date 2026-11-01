using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Filters;
using SportsCenterManagement.BLL.DTOs.Media;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.API.Controllers;

[Route("api/media")]
[Authorize(Roles = "COACH,MANAGER,ADMIN")]
[FlowExceptionFilter]
public class MediaUploadController : FlowControllerBase
{
    private readonly GoogleDriveService _googleDriveService;

    public MediaUploadController(GoogleDriveService googleDriveService)
    {
        _googleDriveService = googleDriveService;
    }

    /// <summary>
    /// Upload file ảnh lên Google Drive và tự động lưu vào cơ sở dữ liệu qua EF Core Code-First.
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadMediaResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UploadMediaResultDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            throw new FlowException(400, "Vui lòng chọn tệp hình ảnh để tải lên.");
        }

        // Ảnh hoặc video hướng dẫn, tối đa 50MB.
        if (file.Length > ImageUploadValidator.MaxFileSize)
        {
            throw new FlowException(400, "Dung lượng media tối đa là 50MB.");
        }

        // Kiểm tra content-type
        var contentType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!contentType.StartsWith("image/") && contentType is not ("video/mp4" or "video/webm"))
        {
            throw new FlowException(400, "Chỉ hỗ trợ ảnh hoặc video MP4/WEBM.");
        }

        using var stream = file.OpenReadStream();
        var currentUserId = CurrentUserId;

        var result = await _googleDriveService.UploadFileAsync(
            stream,
            file.FileName,
            contentType,
            file.Length,
            currentUserId,
            ct
        );

        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách toàn bộ ảnh trực tiếp từ Google Drive folder và DB đã đồng bộ.
    /// </summary>
    [HttpGet("drive-files")]
    public async Task<ActionResult<List<UploadMediaResultDto>>> GetDriveFiles(CancellationToken ct = default)
    {
        var files = await _googleDriveService.GetDriveFolderImagesAsync(CurrentUserId, ct);
        return Ok(files);
    }

    /// <summary>
    /// Kiểm tra tình trạng kết nối Google Drive API và thư mục lưu trữ
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<GoogleDriveStatusDto>> GetStatus(CancellationToken ct = default)
    {
        var status = await _googleDriveService.GetStatusAsync(CurrentUserId, ct);
        return Ok(status);
    }

    /// <summary>
    /// Lấy danh sách các tệp media đã tải lên gần đây từ database.
    /// </summary>
    [HttpGet("recent")]
    public async Task<ActionResult<List<UploadMediaResultDto>>> GetRecent([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var list = await _googleDriveService.GetRecentUploadsAsync(CurrentUserId, Math.Clamp(limit, 1, 100), ct);
        return Ok(list);
    }
}
