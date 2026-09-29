using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.Services;

namespace SportsCenterManagement.API.Controllers;

public sealed record ClassCatalogResponse(
    long Id,
    long CenterId,
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes);

[ApiController]
[Route("api/centers/{centerId:long}/classes")]
public sealed class ClassesController(SportsCenterDbContext db) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClassCatalogResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClassCatalogResponse>>> GetPublished(
        long centerId,
        CancellationToken cancellationToken)
    {
        var classes = await db.Classes.AsNoTracking()
            .Where(item => item.CenterId == centerId && item.Status == "Published")
            .OrderBy(item => item.Name)
            .Select(item => new ClassCatalogResponse(
                item.Id,
                item.CenterId,
                item.SportId,
                item.RoomId,
                item.Name,
                item.Description,
                item.Level,
                item.Capacity,
                item.DurationMinutes))
            .ToListAsync(cancellationToken);

        return Ok(classes);
    }
}
