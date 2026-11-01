using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using SportsCenterManagement.API.Models;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.API.Services;

public sealed class R2StorageService(
    IOptions<R2StorageOptions> options,
    TimeProvider timeProvider,
    SportsCenterDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<R2StorageService> logger) : IR2StorageService, IDisposable
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private const string RefundBillPrefix = "refund-bills/";

    private static readonly IReadOnlyDictionary<string, string> AllowedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    private readonly R2StorageOptions _options = options.Value;
    private IAmazonS3? _s3Client;

    // Object storage is optional for local/demo environments. Defer configuration
    // validation until an upload/delete actually needs R2, while retaining all
    // tenant and ownership checks below.
    private IAmazonS3 S3Client => _s3Client ??= CreateS3Client(_options);

    public async Task<string> UploadRefundBillAsync(
        IFormFile file,
        CancellationToken cancellationToken = default) =>
        await UploadImageAsync(file, "refund-bills", MaxFileSize, cancellationToken);

    public async Task<string> UploadImageAsync(
        IFormFile file,
        string folder,
        long maxFileSize,
        CancellationToken cancellationToken = default)
    {
        if (folder is not ("refund-bills" or "dashboard") && !folder.StartsWith("avatars/", StringComparison.Ordinal))
            throw new ArgumentException("R2 image folder is not allowed.", nameof(folder));
        var scope = await CurrentScope(cancellationToken);
        if (folder.StartsWith("avatars/", StringComparison.Ordinal))
        {
            if (folder != $"avatars/{scope.UserId}") throw BusinessException.Forbidden("Không có quyền tải ảnh đại diện này.");
        }
        else if (!scope.IsAdmin && (!scope.IsManager || !scope.CenterId.HasValue))
            throw BusinessException.Forbidden("Tài khoản chưa được giao quyền tại trung tâm.");
        if (file.Length <= 0)
            throw BusinessException.BadRequest("Vui lòng chọn ảnh để tải lên.");
        if (file.Length > maxFileSize)
            throw BusinessException.BadRequest($"Ảnh không được vượt quá {maxFileSize / 1024 / 1024}MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedTypes.TryGetValue(extension, out var contentType))
            throw BusinessException.BadRequest("Chỉ hỗ trợ ảnh JPG, JPEG, PNG hoặc WEBP.");
        if (!string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            throw BusinessException.BadRequest("Content-Type của ảnh không khớp với phần mở rộng.");

        await using var stream = file.OpenReadStream();
        await ValidateSignatureAsync(stream, extension, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var fileName = $"{now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var key = $"{folder}/{now:yyyy/MM}/{fileName}";

        var request = new PutObjectRequest
        {
            BucketName = Required(_options.BucketName, nameof(_options.BucketName)),
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            AutoCloseStream = false,
            DisablePayloadSigning = true
        };
        request.Headers.CacheControl = "public, max-age=31536000, immutable";

        await S3Client.PutObjectAsync(request, cancellationToken);
        var media = new UploadedMedia
        {
            DriveFileId = MediaAccessScope.R2FileIdPrefix + key,
            FileName = Path.GetFileName(file.FileName), MimeType = contentType, FileSize = file.Length,
            FileUrl = $"{_options.PublicDomain.TrimEnd('/')}/{key}", UploadedByUserId = scope.UserId,
            CenterId = scope.CenterId, CreatedAt = now.UtcDateTime
        };
        db.UploadedMedias.Add(media);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch
        {
            // Do not leave an upload without ownership metadata if the database rejects it.
            try { await S3Client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = request.BucketName, Key = key }, CancellationToken.None); }
            catch (Exception cleanup) { logger.LogError(cleanup, "Could not clean up R2 upload after metadata persistence failed."); }
            throw;
        }
        return key;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 250 ||
            !Regex.IsMatch(key, @"\A(?:refund-bills|dashboard|avatars)/[a-zA-Z0-9/_.-]+\z") ||
            key.Split('/').Any(part => part is "" or "." or ".."))
        {
            throw BusinessException.BadRequest("Đường dẫn ảnh hóa đơn R2 không hợp lệ.");
        }

        var scope = await CurrentScope(cancellationToken);
        // Object keys are case-sensitive even when SQL Server's default collation is not.
        var fileId = MediaAccessScope.R2FileIdPrefix + key;
        var metadata = await db.UploadedMedias.SingleOrDefaultAsync(m =>
            EF.Functions.Collate(m.DriveFileId, "Latin1_General_100_BIN2") == fileId, cancellationToken);
        if (key.StartsWith("avatars/", StringComparison.Ordinal))
        {
            // Legacy avatars already encode their owner; no other route may use this as a business-image delete.
            if (!key.StartsWith($"avatars/{scope.UserId}/", StringComparison.Ordinal)
                || (metadata is not null && metadata.UploadedByUserId != scope.UserId))
                throw BusinessException.NotFound("Không tìm thấy ảnh trong phạm vi được phép.");
        }
        else
        {
            if (!scope.IsAdmin && !scope.IsManager) throw BusinessException.Forbidden("Không có quyền xóa ảnh này.");
            var allowed = scope.IsAdmin || (scope.CenterId.HasValue && metadata?.CenterId == scope.CenterId);
            if (!scope.IsAdmin && scope.CenterId.HasValue && key.StartsWith(RefundBillPrefix, StringComparison.Ordinal))
            {
                // Old refund evidence has no upload metadata. Its invoice is the authoritative tenant boundary.
                var publicUrl = $"{_options.PublicDomain.TrimEnd('/')}/{key}";
                var centers = await (from refund in db.PaymentRefunds join payment in db.Payments on refund.PaymentId equals payment.Id
                    join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
                    where EF.Functions.Collate(refund.TransferProofUrl!, "Latin1_General_100_BIN2") == key
                        || EF.Functions.Collate(refund.TransferProofUrl!, "Latin1_General_100_BIN2") == publicUrl
                    select invoice.CenterId).Distinct().ToListAsync(cancellationToken);
                if (centers.Any(center => center != scope.CenterId)) allowed = false;
                else if (metadata is null) allowed = centers.Count > 0;
            }
            if (!allowed) throw BusinessException.NotFound("Không tìm thấy ảnh trong phạm vi được phép.");
        }

        await S3Client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = Required(_options.BucketName, nameof(_options.BucketName)),
            Key = key
        }, cancellationToken);
        if (metadata is not null)
        {
            db.UploadedMedias.Remove(metadata);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private Task<MediaAccessScope> CurrentScope(CancellationToken ct)
    {
        var id = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return MediaAccessScope.ForUser(db, long.TryParse(id, out var userId) ? userId : 0, ct);
    }

    private static async Task ValidateSignatureAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];
        var bytesRead = await stream.ReadAtLeastAsync(
            header,
            header.Length,
            throwOnEndOfStream: false,
            cancellationToken);
        stream.Position = 0;

        var valid = extension switch
        {
            ".jpg" or ".jpeg" => bytesRead >= 3 &&
                header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => bytesRead >= 8 &&
                header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".webp" => bytesRead >= 12 &&
                header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

        if (!valid)
            throw BusinessException.BadRequest("Nội dung tệp không đúng định dạng ảnh đã khai báo.");
    }

    private static string Required(string value, string propertyName) =>
        !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"R2Storage:{propertyName} is not configured.");

    private static IAmazonS3 CreateS3Client(R2StorageOptions options)
    {
        var serviceUrl = Required(options.ServiceUrl, nameof(options.ServiceUrl));
        var accessKey = Required(options.AccessKey, nameof(options.AccessKey));
        var secretKey = Required(options.SecretKey, nameof(options.SecretKey));
        _ = Required(options.BucketName, nameof(options.BucketName));
        _ = Required(options.PublicDomain, nameof(options.PublicDomain));

        return new AmazonS3Client(
            new BasicAWSCredentials(accessKey, secretKey),
            new AmazonS3Config
            {
                ServiceURL = serviceUrl,
                ForcePathStyle = true,
                AuthenticationRegion = "auto"
            });
    }

    public void Dispose() => _s3Client?.Dispose();
}
