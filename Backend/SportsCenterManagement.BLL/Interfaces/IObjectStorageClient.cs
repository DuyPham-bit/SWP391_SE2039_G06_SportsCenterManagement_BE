namespace SportsCenterManagement.BLL.Interfaces;

public interface IObjectStorageClient
{
    Task DeleteAsync(string key, CancellationToken ct = default);
}

