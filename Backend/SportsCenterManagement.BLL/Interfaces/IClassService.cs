using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IClassService
{
    Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

    Task<ClassCoachResponse> AssignCoachToClassAsync(
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long classId,
        CancellationToken cancellationToken = default);

    Task UnassignCoachFromClassAsync(
        long classId,
        long coachId,
        CancellationToken cancellationToken = default);
}
