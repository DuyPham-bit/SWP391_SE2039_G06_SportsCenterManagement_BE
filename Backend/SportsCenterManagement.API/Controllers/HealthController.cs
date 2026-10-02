using Microsoft.AspNetCore.Mvc;

namespace SportsCenterManagement.API.Controllers;

/// <summary>
/// Controller kiểm tra trạng thái hoạt động của hệ thống Backend (Health check).
/// </summary>
[ApiController]
[Route("health")]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>
    /// Kiểm tra hệ thống Backend có đang phản hồi và sẵn sàng phục vụ hay không.
    /// </summary>
    /// <returns>Trạng thái ok</returns>
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
