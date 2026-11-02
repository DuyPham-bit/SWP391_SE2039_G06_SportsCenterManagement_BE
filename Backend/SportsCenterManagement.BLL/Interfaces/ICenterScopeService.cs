namespace SportsCenterManagement.BLL.Interfaces;

public interface ICenterScopeService
{
    Task EnsureCenterAccessAsync(long userId, bool isAdmin, long centerId, CancellationToken ct = default);
    Task EnsureClassAccessAsync(long userId, bool isAdmin, long classId, CancellationToken ct = default);
}

