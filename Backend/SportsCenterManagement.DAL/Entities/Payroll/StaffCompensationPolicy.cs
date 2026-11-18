using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("staff_compensation_policies")]
public sealed class StaffCompensationPolicy : BaseEntity
{
    [Column("user_id")] public long UserId { get; set; }
    [Column("center_id")] public long? CenterId { get; set; }
    [Precision(14, 2), Column("monthly_base_salary")] public decimal MonthlyBaseSalary { get; set; }
    [Precision(14, 2), Column("monthly_allowance")] public decimal MonthlyAllowance { get; set; }
    [Column("effective_from")] public DateOnly EffectiveFrom { get; set; }
    [Column("effective_to")] public DateOnly? EffectiveTo { get; set; }
    [MaxLength(30), Column("status")] public string Status { get; set; } = "Active";
    [Column("updated_by")] public long UpdatedBy { get; set; }
    [Column("updated_at")] public DateTime UpdatedAt { get; set; }
}
