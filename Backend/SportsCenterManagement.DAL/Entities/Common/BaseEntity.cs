using System.ComponentModel.DataAnnotations.Schema;

namespace SportsCenterManagement.DAL.Entities.Common;

public abstract class BaseEntity
{
    [Column("id")]
    public long Id { get; set; }
}
