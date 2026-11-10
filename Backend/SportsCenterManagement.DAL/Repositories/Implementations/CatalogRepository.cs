using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.DAL.Repositories.Implementations;

public sealed class CatalogRepository(SportsCenterDbContext db) : ICatalogRepository
{
    public async Task<Center?> GetActiveCenterByIdAsync(long centerId, CancellationToken ct = default)
    {
        return await ActiveCenter(centerId).AsNoTracking().SingleOrDefaultAsync(ct);
    }

    public Task<bool> IsActiveCenterAsync(long centerId, CancellationToken ct = default)
    {
        return ActiveCenter(centerId).AnyAsync(ct);
    }

    private IQueryable<Center> ActiveCenter(long centerId) =>
        db.Centers.Where(x => x.Id == centerId && x.Status == "Active");

    public async Task<IReadOnlyList<Sport>> GetActiveSportsByCenterIdAsync(long centerId, CancellationToken ct = default)
    {
        return await db.Sports
            .AsNoTracking()
            .Where(x => (x.CenterId == centerId || x.CenterId == null) && x.Status == "Active")
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Room>> GetActiveRoomsByCenterIdAsync(long centerId, CancellationToken ct = default)
    {
        return await db.Rooms
            .AsNoTracking()
            .Where(x => x.CenterId == centerId && x.Status == "Active")
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClassEntity>> GetPublishedClassesByCenterIdAsync(long centerId, CancellationToken ct = default)
    {
        return await db.Classes
            .AsNoTracking()
            .Where(x => x.CenterId == centerId && x.Status == "Published")
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }
}

