using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("system_settings")]
public class SystemSetting : Common.BaseEntity
{
    [MaxLength(100)]
    [Column("setting_key")]
    public string SettingKey { get; set; } = null!;

    [Column("setting_value")]
    public string? SettingValue { get; set; }

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Column("updated_by")]
    public long? UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
