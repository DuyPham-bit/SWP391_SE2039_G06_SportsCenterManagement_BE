namespace SportsCenterManagement.BLL.DTOs.Media;

public sealed record GoogleDriveStatusDto(
    bool IsConnected,
    string ServiceAccountEmail,
    string FolderId,
    int TotalImages,
    string Message
);
