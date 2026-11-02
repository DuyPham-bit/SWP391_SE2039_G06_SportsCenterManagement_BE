using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.API.Services;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("api/refund-bills")]
[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class RefundBillStorageController(IR2StorageService storageService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(UploadRefundBillResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<UploadRefundBillResponse>> Upload(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null)
            return BadRequest(new ProblemDetails { Detail = "Vui lòng chọn ảnh hóa đơn để tải lên." });

        var key = await storageService.UploadRefundBillAsync(file, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new UploadRefundBillResponse(key));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete([FromQuery] string key, CancellationToken cancellationToken)
    {
        if (!key.StartsWith("refund-bills/", StringComparison.Ordinal)) return BadRequest();
        await storageService.DeleteAsync(key, cancellationToken);
        return NoContent();
    }
}

public sealed record UploadRefundBillResponse(string Key);
