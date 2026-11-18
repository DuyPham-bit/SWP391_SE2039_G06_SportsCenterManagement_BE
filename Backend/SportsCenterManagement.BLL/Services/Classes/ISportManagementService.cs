using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Services.Classes;

/// <summary>Manages the sport catalog used by classes and coach specialties at a center.</summary>
public interface ISportManagementService
{
    Task<IReadOnlyList<ManagedSportResponse>> GetActiveAsync(long centerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ManagedSportResponse>> GetAllAsync(long centerId, CancellationToken cancellationToken = default);
    Task<ManagedSportResponse> CreateAsync(long actorUserId, long centerId, SaveSportRequest request, CancellationToken cancellationToken = default);
    Task<ManagedSportResponse> UpdateAsync(long actorUserId, long centerId, long sportId, SaveSportRequest request, CancellationToken cancellationToken = default);
}
