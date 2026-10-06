using ClassesResponses = SportsCenterManagement.BLL.DTOs.Classes.Responses;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IClassService
{
    Task<IReadOnlyList<ClassesResponses.ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default);
}
