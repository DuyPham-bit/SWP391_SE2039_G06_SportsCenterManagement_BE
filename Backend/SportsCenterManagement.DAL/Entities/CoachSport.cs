using System.ComponentModel.DataAnnotations.Schema;

namespace SportsCenterManagement.DAL.Entities;

[Table("coach_sports")]
public class CoachSport
{
    [Column("coach_id")]
    public long CoachId { get; set; }

    [Column("sport_id")]
    public long SportId { get; set; }
}
