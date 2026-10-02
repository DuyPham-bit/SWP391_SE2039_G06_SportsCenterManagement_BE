using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("audit_logs")]
public class AuditLog : Common.BaseEntity
{
    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("center_id")]
    public long? CenterId { get; set; }

    [MaxLength(100)]
    [Column("correlation_id")]
    public string? CorrelationId { get; set; }

    [MaxLength(100)]
    [Column("action")]
    public string Action { get; set; } = null!;

    [MaxLength(100)]
    [Column("entity_type")]
    public string EntityType { get; set; } = null!;

    [Column("entity_id")]
    public long? EntityId { get; set; }

    [Column("old_values")]
    public string? OldValues { get; set; }

    [Column("new_values")]
    public string? NewValues { get; set; }

    [MaxLength(45)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
