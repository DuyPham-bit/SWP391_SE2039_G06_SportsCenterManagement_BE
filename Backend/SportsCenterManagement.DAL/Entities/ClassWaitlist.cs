using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("class_waitlist")]
public class ClassWaitlist : Common.BaseEntity
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("member_id")]
    public long MemberId { get; set; }

    [Column("joined_at")]
    public DateTime JoinedAt { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
