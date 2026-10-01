using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IClassService
{
    Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default);
}
