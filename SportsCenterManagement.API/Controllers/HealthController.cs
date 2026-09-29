using Microsoft.AspNetCore.Mvc;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Route("health")]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok" });
}
