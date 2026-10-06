using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("sports")]
public class Sport : Common.BaseEntity
{
    [Column("center_id")]
    public long? CenterId { get; set; }

    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

}
