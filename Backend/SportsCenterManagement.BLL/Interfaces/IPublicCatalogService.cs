using SportsCenterManagement.BLL.DTOs.Catalog;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IPublicCatalogService
{
    Task<PublicCatalogResponse?> GetCatalogByCenterIdAsync(long centerId, CancellationToken ct = default);
}

