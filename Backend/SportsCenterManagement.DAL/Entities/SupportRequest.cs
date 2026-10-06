using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("support_requests")]
public class SupportRequest : Common.BaseEntity
{
    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("handled_by")]
    public long? HandledBy { get; set; }

    [MaxLength(200)]
    [Column("subject")]
    public string Subject { get; set; } = null!;

    [Column("description")]
    public string Description { get; set; } = null!;

    [MaxLength(30)]
    [Column("priority")]
    public string Priority { get; set; } = null!;

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

}
