namespace SportsCenterManagement.BLL.Services.Media;

public interface IObjectStorageClient
{
    Task DeleteAsync(string key, CancellationToken ct = default);
}

