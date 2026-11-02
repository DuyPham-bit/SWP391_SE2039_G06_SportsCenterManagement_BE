namespace SportsCenterManagement.API.Services;

public interface IR2StorageService
{
    Task<string> UploadImageAsync(IFormFile file, string folder, long maxFileSize, CancellationToken cancellationToken = default);
    Task<string> UploadRefundBillAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<string> UploadMediaAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<LegacyMediaMigrationResult> MigrateLegacyMediaAsync(CancellationToken cancellationToken = default);
}

public sealed record LegacyMediaMigrationResult(int Migrated, int Skipped, IReadOnlyList<long> FailedMediaIds);
