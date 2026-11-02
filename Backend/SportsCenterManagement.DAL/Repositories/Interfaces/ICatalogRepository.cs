using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.DAL.Repositories.Interfaces;

public interface ICatalogRepository
{
    Task<Center?> GetActiveCenterByIdAsync(long centerId, CancellationToken ct = default);
    Task<IReadOnlyList<Sport>> GetActiveSportsByCenterIdAsync(long centerId, CancellationToken ct = default);
    Task<IReadOnlyList<Room>> GetActiveRoomsByCenterIdAsync(long centerId, CancellationToken ct = default);
    Task<IReadOnlyList<ClassEntity>> GetPublishedClassesByCenterIdAsync(long centerId, CancellationToken ct = default);
}

