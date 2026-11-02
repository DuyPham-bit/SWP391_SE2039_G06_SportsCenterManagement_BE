namespace SportsCenterManagement.BLL.Interfaces;

public interface ICenterAccessService
{
    Task<bool> IsActiveCenterAsync(long centerId, CancellationToken cancellationToken = default);

    Task EnsureCenterAccessAsync(long actorUserId, bool isSystemAdmin, long centerId, CancellationToken cancellationToken = default);

    Task EnsureClassAccessAsync(long actorUserId, bool isSystemAdmin, long classId, CancellationToken cancellationToken = default);
}
