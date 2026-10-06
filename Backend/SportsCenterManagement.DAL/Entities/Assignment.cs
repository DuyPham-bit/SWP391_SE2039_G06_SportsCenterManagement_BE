using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("assignments")]
public class Assignment : Common.BaseEntity
{
    [Column("coach_id")]
    public long? CoachId { get; set; }

    [Column("class_id")]
    public long? ClassId { get; set; }

    [Column("member_id")]
    public long? MemberId { get; set; }

    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }

    [Column("due_date")]
    public DateTime? DueDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
