using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/centers/{centerId:long}/catalog")]
public sealed class PublicCatalogController(SportsCenterDbContext db) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(long centerId, CancellationToken ct)
    {
        // Public catalog exposes center/sport/room details, never account or member records.
        var center = await db.Centers.AsNoTracking()
            .Where(x => x.Id == centerId && x.Status == "Active")
            .Select(x => new { x.Id, x.Name, x.Address, x.Phone, x.Email, x.Description, x.OpeningTime, x.ClosingTime })
            .SingleOrDefaultAsync(ct);
        if (center is null) return NotFound(new { message = "Trung tâm không tồn tại hoặc đã ngừng hoạt động." });
        var sports = await db.Sports.AsNoTracking()
            .Where(x => (x.CenterId == centerId || x.CenterId == null) && x.Status == "Active")
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Description }).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking()
            .Where(x => x.CenterId == centerId && x.Status == "Active")
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.RoomType, x.Capacity, x.LocationDescription }).ToListAsync(ct);
        var classes = await db.Classes.AsNoTracking()
            .Where(x => x.CenterId == centerId && x.Status == "Published")
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.SportId, x.RoomId, x.Level }).ToListAsync(ct);
        return Ok(new { center, sports, rooms, classes });
    }
}
