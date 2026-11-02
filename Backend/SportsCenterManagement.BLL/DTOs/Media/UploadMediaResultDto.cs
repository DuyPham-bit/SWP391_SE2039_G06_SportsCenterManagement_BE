namespace SportsCenterManagement.BLL.DTOs.Media;

public sealed record UploadMediaResultDto(
    long Id,
    string FileId,
    string Url,
    string FileName,
    string MimeType,
    long? FileSize,
    DateTime CreatedAt,
    string? ThumbnailUrl = null
);
