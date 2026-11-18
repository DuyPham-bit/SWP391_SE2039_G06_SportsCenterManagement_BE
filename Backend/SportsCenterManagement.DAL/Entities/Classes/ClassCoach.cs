using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("class_coaches")]
public class ClassCoach
{
    [Column("class_id")]
    public long ClassId { get; set; }

    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("assigned_date")]
    public DateOnly? AssignedDate { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; }

}
