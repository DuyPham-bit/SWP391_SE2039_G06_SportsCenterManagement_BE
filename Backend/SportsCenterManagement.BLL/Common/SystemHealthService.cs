namespace SportsCenterManagement.BLL.Common;

/// <summary>Database reachability for the health endpoints and the readiness health check.</summary>
public sealed class SystemHealthService(IUnitOfWork unitOfWork)
{
    public Task<bool> CanConnectToDatabaseAsync(CancellationToken cancellationToken) =>
        unitOfWork.Context.Database.CanConnectAsync(cancellationToken);
}
