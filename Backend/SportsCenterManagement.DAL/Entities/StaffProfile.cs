using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("staff_profiles")]
public class StaffProfile : Common.BaseEntity
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(50)]
    [Column("staff_code")]
    public string StaffCode { get; set; } = null!;

    [MaxLength(150)]
    [Column("full_name")]
    public string FullName { get; set; } = null!;

    [MaxLength(100)]
    [Column("position")]
    public string Position { get; set; } = null!;

    [Column("hire_date")]
    public DateOnly? HireDate { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

}
