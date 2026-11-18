using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("uploaded_media")]
public class UploadedMedia : BaseEntity
{
    [Column("drive_file_id")]
    [MaxLength(255)]
    public string DriveFileId { get; set; } = null!;

    [Column("file_name")]
    [MaxLength(500)]
    public string FileName { get; set; } = null!;

    [Column("mime_type")]
    [MaxLength(100)]
    public string MimeType { get; set; } = null!;

    [Column("file_size")]
    public long? FileSize { get; set; }

    [Column("file_url")]
    [MaxLength(1000)]
    public string FileUrl { get; set; } = null!;

    [Column("web_view_link")]
    [MaxLength(1000)]
    public string? WebViewLink { get; set; }

    [Column("uploaded_by_user_id")]
    public long? UploadedByUserId { get; set; }

    // Snapshot the tenant at upload time; transferring an employee must not move their old media.
    [Column("center_id")]
    public long? CenterId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
