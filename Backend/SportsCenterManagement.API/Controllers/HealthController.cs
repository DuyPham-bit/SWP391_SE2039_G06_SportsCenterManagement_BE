using Microsoft.AspNetCore.Mvc;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Route("health")]
[Route("api/health")]
public sealed class HealthController(SportsCenterManagement.BLL.Common.SystemHealthService health) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var canConnect = await health.CanConnectToDatabaseAsync(ct);
        return canConnect
            ? Ok(new { status = "Healthy", database = "Connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Disconnected" });
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "Healthy", check = "Liveness" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var canConnect = await health.CanConnectToDatabaseAsync(ct);
        return canConnect
            ? Ok(new { status = "Healthy", check = "Readiness", database = "Connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", check = "Readiness", database = "Disconnected" });
    }
}
