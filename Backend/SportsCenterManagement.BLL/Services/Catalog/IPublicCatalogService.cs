using SportsCenterManagement.BLL.DTOs.Catalog;

namespace SportsCenterManagement.BLL.Services.Catalog;

public interface IPublicCatalogService
{
    Task<PublicCatalogResponse?> GetCatalogByCenterIdAsync(long centerId, CancellationToken ct = default);
}

