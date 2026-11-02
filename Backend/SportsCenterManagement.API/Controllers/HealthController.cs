using Microsoft.AspNetCore.Mvc;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
[Route("health")]
[Route("api/health")]
public sealed class HealthController(SportsCenterDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? Ok(new { status = "Healthy", database = "Connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", database = "Disconnected" });
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "Healthy", check = "Liveness" });

    [HttpGet("ready")]
    public async Task<IActionResult> Ready(CancellationToken ct)
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        return canConnect
            ? Ok(new { status = "Healthy", check = "Readiness", database = "Connected" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "Unhealthy", check = "Readiness", database = "Disconnected" });
    }
}
