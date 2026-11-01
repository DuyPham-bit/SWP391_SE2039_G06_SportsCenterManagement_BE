using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SportsCenterManagement.BLL.DTOs.Media;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public class GoogleDriveService
{
    private readonly SportsCenterDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleDriveService> _logger;
    private readonly string _storageRootPath;

    public GoogleDriveService(
        SportsCenterDbContext dbContext,
        IConfiguration configuration,
        ILogger<GoogleDriveService> logger,
        string storageRootPath)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
        _storageRootPath = storageRootPath;
    }

    public string ResolveCredentialsPath()
    {
        var credentialsPath = _configuration["GoogleDrive:CredentialsPath"] ?? "credentials.json";

        if (File.Exists(credentialsPath))
            return credentialsPath;

        var baseDirCandidate = Path.Combine(AppContext.BaseDirectory, credentialsPath);
        if (File.Exists(baseDirCandidate))
            return baseDirCandidate;

        var currentDirCandidate = Path.Combine(Directory.GetCurrentDirectory(), credentialsPath);
        if (File.Exists(currentDirCandidate))
            return currentDirCandidate;

        var parentCandidate = Path.Combine(Directory.GetCurrentDirectory(), "SportsCenterManagement.API", credentialsPath);
        if (File.Exists(parentCandidate))
            return parentCandidate;

        return credentialsPath;
    }

    protected virtual DriveService CreateDriveService()
    {
        var authMode = _configuration["GoogleDrive:AuthMode"] ?? "ServiceAccount";

        if (string.Equals(authMode, "OAuth", StringComparison.OrdinalIgnoreCase))
        {
            var clientId = _configuration["GoogleDrive:ClientId"];
            var clientSecret = _configuration["GoogleDrive:ClientSecret"];
            var refreshToken = _configuration["GoogleDrive:RefreshToken"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new FlowException(500, "Cấu hình Google Drive OAuth (ClientId, ClientSecret, RefreshToken) chưa đầy đủ.");
            }

            var tokenResponse = new TokenResponse
            {
                RefreshToken = refreshToken
            };

            var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = new ClientSecrets
                {
                    ClientId = clientId.Trim(),
                    ClientSecret = clientSecret.Trim()
                },
                Scopes = new[] { DriveService.ScopeConstants.Drive }
            });

            var credential = new UserCredential(flow, "scms_user", tokenResponse);

            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "SCMS-SportsCenterManagement"
            });
        }
        else
        {
            var resolvedPath = ResolveCredentialsPath();

            if (!File.Exists(resolvedPath))
            {
                throw new FlowException(500, $"Không tìm thấy tệp credentials.json xác thực Google Drive tại: {resolvedPath}. Vui lòng đặt file credentials.json trong thư mục API.");
            }

            var credential = CredentialFactory.FromFile(resolvedPath, JsonCredentialParameters.ServiceAccountCredentialType)
                .CreateScoped(DriveService.ScopeConstants.Drive);

            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "SCMS-SportsCenterManagement"
            });
        }
    }

    private async Task<UploadMediaResultDto> SaveLocalFileAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long? fileSize,
        long? currentUserId,
        long? centerId,
        CancellationToken cancellationToken)
    {
        var uploadsDir = Path.Combine(_storageRootPath, "uploads");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var ext = Path.GetExtension(fileName);
        var safeFileName = $"{Guid.NewGuid():N}{ext}";
        var physicalPath = Path.Combine(uploadsDir, safeFileName);

        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using (var destStream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write))
        {
            await fileStream.CopyToAsync(destStream, cancellationToken);
        }

        var publicUrl = $"/uploads/{safeFileName}";
        var entity = new UploadedMedia
        {
            DriveFileId = $"local_{Guid.NewGuid():N}",
            FileName = fileName,
            MimeType = mimeType,
            FileSize = fileSize ?? fileStream.Length,
            FileUrl = publicUrl,
            WebViewLink = publicUrl,
            UploadedByUserId = currentUserId,
            CenterId = centerId,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.UploadedMedias.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new UploadMediaResultDto(
            entity.Id,
            entity.DriveFileId,
            entity.FileUrl,
            entity.FileName,
            entity.MimeType,
            entity.FileSize,
            entity.CreatedAt,
            entity.FileUrl
        );
    }

    public async Task<UploadMediaResultDto> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string mimeType,
        long? fileSize,
        long currentUserId,
        CancellationToken cancellationToken = default)
    {
        var scope = await MediaAccessScope.ForUser(_dbContext, currentUserId, cancellationToken);
        var validated = await ImageUploadValidator.ValidateAsync(fileStream, fileName, mimeType, cancellationToken);
        fileName = validated.FileName;
        mimeType = validated.MimeType;
        fileSize = fileStream.Length;

        var storageProvider = _configuration["GoogleDrive:StorageProvider"] ?? "GoogleDrive";
        if (string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("StorageProvider được chỉ định là 'Local'. Lưu trực tiếp vào bộ nhớ máy chủ.");
            return await SaveLocalFileAsync(fileStream, fileName, mimeType, fileSize, currentUserId, scope.CenterId, cancellationToken);
        }

        try
        {
            using var driveService = CreateDriveService();
            var folderId = GetFolderId();

            _logger.LogInformation(">>> Đang tải file lên thư mục Google Drive ID: '{FolderId}'", folderId);

            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                MimeType = mimeType,
                Parents = new List<string> { folderId.Trim() }
            };

            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var request = driveService.Files.Create(fileMetadata, fileStream, mimeType);
            request.SupportsAllDrives = true;
            request.Fields = "id, name, mimeType, size, webViewLink, thumbnailLink";

            var progress = await request.UploadAsync(cancellationToken);
            if (progress.Status != UploadStatus.Completed)
            {
                _logger.LogError(progress.Exception, "Lỗi khi upload file lên Google Drive: {Error}", progress.Exception?.Message);
                throw progress.Exception ?? new Exception($"Upload failed with status: {progress.Status}");
            }

            var driveFile = request.ResponseBody;
            var fileId = driveFile.Id;

            // Cấp quyền xem công khai reader/anyone để ảnh có thể hiển thị trực tiếp
            try
            {
                var permission = new Google.Apis.Drive.v3.Data.Permission
                {
                    Role = "reader",
                    Type = "anyone"
                };
                var permReq = driveService.Permissions.Create(permission, fileId);
                permReq.SupportsAllDrives = true;
                await permReq.ExecuteAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Không thể mở quyền xem công khai cho file {FileId}", fileId);
                throw new FlowException(502, "Không thể cấp quyền xem ảnh Google Drive.");
            }

            // Định dạng URL ảnh công khai từ Google Usercontent theo ID
            var publicUrl = $"https://lh3.googleusercontent.com/d/{fileId}";

            // Lưu vào Database thông qua Code-First Entity
            var entity = new UploadedMedia
            {
                DriveFileId = fileId,
                FileName = fileName,
                MimeType = mimeType,
                FileSize = fileSize ?? fileStream.Length,
                FileUrl = publicUrl,
                WebViewLink = driveFile.WebViewLink,
                UploadedByUserId = currentUserId,
                CenterId = scope.CenterId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.UploadedMedias.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new UploadMediaResultDto(
                entity.Id,
                entity.DriveFileId,
                entity.FileUrl,
                entity.FileName,
                entity.MimeType,
                entity.FileSize,
                entity.CreatedAt,
                driveFile.ThumbnailLink ?? publicUrl
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Upload lên Google Drive thất bại: {Error}. Đang tự động lưu file cục bộ (Local Fallback)...", ex.Message);
            try
            {
                return await SaveLocalFileAsync(fileStream, fileName, mimeType, fileSize, currentUserId, scope.CenterId, cancellationToken);
            }
            catch (Exception localEx) when (localEx is not OperationCanceledException)
            {
                _logger.LogError(localEx, "Lưu file cục bộ cũng thất bại: {Error}", localEx.Message);
                throw new FlowException(500, "Không thể lưu ảnh. Vui lòng thử lại hoặc liên hệ quản trị viên.");
            }
        }
    }

    /// <summary>
    /// Lấy danh sách ảnh trực tiếp từ thư mục Google Drive đã cấu hình, đồng thời đồng bộ vào DB
    /// </summary>
    private string GetFolderId()
    {
        var folderId = _configuration["GoogleDrive:FolderId"]?.Trim();
        if (string.IsNullOrWhiteSpace(folderId) || folderId == "YOUR_GOOGLE_DRIVE_FOLDER_ID")
            throw new FlowException(503, "Chưa cấu hình thư mục Google Drive.");
        return folderId;
    }

    protected virtual async Task<List<Google.Apis.Drive.v3.Data.File>> ReadDriveImagesAsync(CancellationToken ct)
    {
        var folderId = GetFolderId();
        using var driveService = CreateDriveService();
        var request = driveService.Files.List();
        request.SupportsAllDrives = true;
        request.IncludeItemsFromAllDrives = true;
        request.PageSize = 100;
        request.Fields = "nextPageToken,incompleteSearch,files(id,name,mimeType,size,createdTime,webViewLink,thumbnailLink)";
        request.Q = $"'{folderId.Replace("'", "\\'")}' in parents and trashed = false";
        var files = new List<Google.Apis.Drive.v3.Data.File>();
        var pageTokens = new HashSet<string>();
        do
        {
            var response = await request.ExecuteAsync(ct);
            if (response.IncompleteSearch == true) throw new FlowException(502, "Google Drive trả về danh sách chưa đầy đủ.");
            if (response.Files is not null) files.AddRange(response.Files.Where(IsSupportedDriveImage));
            request.PageToken = response.NextPageToken;
            if (!string.IsNullOrEmpty(request.PageToken) && !pageTokens.Add(request.PageToken))
                throw new FlowException(502, "Google Drive không thể tải trang ảnh kế tiếp.");
        } while (!string.IsNullOrEmpty(request.PageToken));
        return files.DistinctBy(f => f.Id).ToList();
    }

    public async Task<List<UploadMediaResultDto>> GetDriveFolderImagesAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        var scope = await MediaAccessScope.ForUser(_dbContext, currentUserId, cancellationToken);
        var dbList = await scope.Library(_dbContext).AsTracking().OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
        if (string.Equals(_configuration["GoogleDrive:StorageProvider"], "Local", StringComparison.OrdinalIgnoreCase))
            return dbList.Select(ToDto).ToList();

        List<Google.Apis.Drive.v3.Data.File> files;
        try
        {
            // Obtain a complete snapshot before changing any database metadata.
            files = await ReadDriveImagesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Không thể đồng bộ Google Drive. Giữ nguyên metadata và dùng thư viện đã lưu.");
            return dbList.Select(ToDto).ToList();
        }

        var byFileId = dbList.GroupBy(m => m.DriveFileId).ToDictionary(g => g.Key, g => g.First());
        var results = new List<UploadMediaResultDto>();
        foreach (var file in files)
        {
            if (!byFileId.TryGetValue(file.Id, out var entity))
            {
                // A shared Drive folder is not proof that an unknown file belongs to this tenant.
                if (!scope.IsAdmin) continue;
                entity = new UploadedMedia
                {
                    DriveFileId = file.Id,
                    CreatedAt = file.CreatedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow
                };
                _dbContext.UploadedMedias.Add(entity);
                byFileId[file.Id] = entity;
            }
            entity.FileName = file.Name ?? "drive_image.jpg";
            entity.MimeType = ImageUploadValidator.GetMimeTypeForFileName(entity.FileName)!;
            entity.FileSize = file.Size;
            entity.FileUrl = $"https://lh3.googleusercontent.com/d/{file.Id}";
            entity.WebViewLink = file.WebViewLink;
        }
        await _dbContext.SaveChangesAsync(cancellationToken);
        foreach (var file in files)
            if (byFileId.TryGetValue(file.Id, out var visible)) results.Add(ToDto(visible, file.ThumbnailLink));
        results.AddRange(dbList.Where(m => m.DriveFileId.StartsWith("local_", StringComparison.Ordinal)).Select(ToDto));
        // Missing files are hidden after a successful snapshot. Metadata is retained for recovery/auditing.
        return results.OrderByDescending(m => m.CreatedAt).ToList();
    }

    private static UploadMediaResultDto ToDto(UploadedMedia media) => ToDto(media, null);

    private static UploadMediaResultDto ToDto(UploadedMedia media, string? thumbnail) => new(
        media.Id, media.DriveFileId, media.FileUrl, media.FileName, media.MimeType,
        media.FileSize, media.CreatedAt, thumbnail ?? media.FileUrl);

    /// <summary>
    /// Kiểm tra trạng thái kết nối Google Drive API và quyền truy cập thư mục
    /// </summary>
    public async Task<GoogleDriveStatusDto> GetStatusAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        var scope = await MediaAccessScope.ForUser(_dbContext, currentUserId, cancellationToken);
        var visibleCount = await scope.Library(_dbContext).CountAsync(cancellationToken);
        var folderId = _configuration["GoogleDrive:FolderId"]?.Trim() ?? "";
        if (string.Equals(_configuration["GoogleDrive:StorageProvider"], "Local", StringComparison.OrdinalIgnoreCase))
            return new GoogleDriveStatusDto(true, "", "", visibleCount, "Đang sử dụng thư viện ảnh trên máy chủ.");
        var authMode = _configuration["GoogleDrive:AuthMode"] ?? "ServiceAccount";
        string accountInfo = "";

        try
        {
            if (string.Equals(authMode, "OAuth", StringComparison.OrdinalIgnoreCase))
            {
                accountInfo = "OAuth2 Personal Google Drive";
            }
            else
            {
                var resolvedPath = ResolveCredentialsPath();
                if (File.Exists(resolvedPath))
                {
                    var json = await File.ReadAllTextAsync(resolvedPath, cancellationToken);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("client_email", out var emailProp))
                    {
                        accountInfo = emailProp.GetString() ?? "";
                    }
                }
            }

            var images = await ReadDriveImagesAsync(cancellationToken);
            var imageCount = scope.IsAdmin ? images.Count : visibleCount;

            return new GoogleDriveStatusDto(
                IsConnected: true,
                ServiceAccountEmail: accountInfo,
                FolderId: folderId,
                TotalImages: imageCount,
                Message: "Kết nối Google Drive API thành công. Thư mục đã sẵn sàng."
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Không thể kiểm tra kết nối Google Drive.");
            return new GoogleDriveStatusDto(
                IsConnected: false,
                ServiceAccountEmail: accountInfo,
                FolderId: folderId,
                TotalImages: visibleCount,
                Message: "Không thể kết nối Google Drive. Đang dùng thư viện đã lưu; vui lòng kiểm tra cấu hình hoặc thử lại."
            );
        }
    }

    public async Task<List<UploadMediaResultDto>> GetRecentUploadsAsync(long currentUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        var scope = await MediaAccessScope.ForUser(_dbContext, currentUserId, cancellationToken);
        return await scope.Library(_dbContext)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new UploadMediaResultDto(
                m.Id,
                m.DriveFileId,
                m.FileUrl,
                m.FileName,
                m.MimeType,
                m.FileSize,
                m.CreatedAt,
                m.FileUrl
            ))
            .ToListAsync(cancellationToken);
    }

    private static bool IsSupportedDriveImage(Google.Apis.Drive.v3.Data.File file)
    {
        if (string.IsNullOrWhiteSpace(file.Name) || !ImageUploadValidator.IsSupportedFileName(file.Name)) return false;
        // Drive can store AVIF as a generic binary file. Its supported extension still identifies a gallery candidate.
        return string.IsNullOrWhiteSpace(file.MimeType) ||
            file.MimeType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            file.MimeType.Equals("binary/octet-stream", StringComparison.OrdinalIgnoreCase) ||
            file.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }
}
