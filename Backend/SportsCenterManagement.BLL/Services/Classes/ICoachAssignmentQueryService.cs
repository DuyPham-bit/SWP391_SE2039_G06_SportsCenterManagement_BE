using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

/// <summary>Provides active coaches and their current teaching slots for manager assignment decisions.</summary>
public interface ICoachAssignmentQueryService
{
    Task<IReadOnlyList<CoachAssignmentCandidateResponse>> GetCandidatesAsync(
        long centerId, CancellationToken cancellationToken = default);
}
