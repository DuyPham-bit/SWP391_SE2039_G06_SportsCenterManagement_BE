using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("permissions")]
public class Permission : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("code")]
    public string Code { get; set; } = null!;

    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(255)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(50)]
    [Column("module")]
    public string? Module { get; set; }
}
