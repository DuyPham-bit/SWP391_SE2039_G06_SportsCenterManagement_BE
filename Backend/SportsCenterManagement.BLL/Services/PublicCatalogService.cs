using SportsCenterManagement.BLL.DTOs.Catalog;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class PublicCatalogService(ICatalogRepository catalogRepository) : IPublicCatalogService
{
    public async Task<PublicCatalogResponse?> GetCatalogByCenterIdAsync(long centerId, CancellationToken ct = default)
    {
        var center = await catalogRepository.GetActiveCenterByIdAsync(centerId, ct);
        if (center is null) return null;

        var sports = await catalogRepository.GetActiveSportsByCenterIdAsync(centerId, ct);
        var rooms = await catalogRepository.GetActiveRoomsByCenterIdAsync(centerId, ct);
        var classes = await catalogRepository.GetPublishedClassesByCenterIdAsync(centerId, ct);

        var centerDto = new PublicCenterDto(
            center.Id,
            center.Name,
            center.Address,
            center.Phone,
            center.Email,
            center.Description,
            center.OpeningTime,
            center.ClosingTime);

        var sportsDto = sports
            .Select(s => new PublicSportDto(s.Id, s.Name, s.Description))
            .ToList();

        var roomsDto = rooms
            .Select(r => new PublicRoomDto(r.Id, r.Name, r.RoomType, r.Capacity, r.LocationDescription))
            .ToList();

        var classesDto = classes
            .Select(c => new PublicClassDto(c.Id, c.Name, c.SportId, c.RoomId, c.Level))
            .ToList();

        return new PublicCatalogResponse(centerDto, sportsDto, roomsDto, classesDto);
    }
}

