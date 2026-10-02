using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("centers")]
public class Center : Common.BaseEntity
{
    [MaxLength(150)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    [Column("address")]
    public string Address { get; set; } = null!;

    [MaxLength(20)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(150)]
    [Column("email")]
    public string? Email { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("opening_time")]
    public TimeOnly? OpeningTime { get; set; }

    [Column("closing_time")]
    public TimeOnly? ClosingTime { get; set; }

    [MaxLength(100)]
    [Column("time_zone_id")]
    public string? TimeZoneId { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
