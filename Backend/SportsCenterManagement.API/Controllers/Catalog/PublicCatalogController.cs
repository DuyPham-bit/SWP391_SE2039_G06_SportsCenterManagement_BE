using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SportsCenterManagement.API.Controllers.Catalog;

[ApiController]
[AllowAnonymous]
[Route("api/public/centers/{centerId:long}/catalog")]
public sealed class PublicCatalogController(IPublicCatalogService catalogService) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(long centerId, CancellationToken ct)
    {
        var catalog = await catalogService.GetCatalogByCenterIdAsync(centerId, ct);
        if (catalog is null)
        {
            return NotFound(new { message = "Trung tâm không tồn tại hoặc đã ngừng hoạt động." });
        }

        return Ok(new
        {
            center = catalog.Center,
            sports = catalog.Sports,
            rooms = catalog.Rooms,
            classes = catalog.Classes
        });
    }
}
