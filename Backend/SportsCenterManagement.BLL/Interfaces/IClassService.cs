using SportsCenterManagement.BLL.DTOs.Classes;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IClassService
{
    Task<long> CreateClassAsync(CreateClassRequest request, CancellationToken cancellationToken = default);
    Task<long> CreateScheduleAsync(long classId, CreateClassScheduleRequest request, CancellationToken cancellationToken = default);
    Task PublishClassAsync(long classId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassSessionResponse>> GetClassSessionsAsync(long classId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CenterScheduleResponse>> GetPublishedScheduleAsync(long centerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SportOptionResponse>> GetSportsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassOptionResponse>> GetRoomsAsync(long centerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default);

<<<<<<< Updated upstream
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
=======
    Task<ClassCoachResponse> AssignCoachToClassAsync(long classId, AssignCoachRequest request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(long classId,
        CancellationToken cancellationToken = default);
    Task UnassignCoachFromClassAsync(long classId, long coachId,
>>>>>>> Stashed changes
        CancellationToken cancellationToken = default);
}
