using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities.Common;

namespace SportsCenterManagement.DAL.Entities;

[Table("rooms")]
public class Room : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [MaxLength(100)]
    [Column("room_type")]
    public string? RoomType { get; set; }

    [Column("capacity")]
    public int Capacity { get; set; }

    [MaxLength(255)]
    [Column("location_description")]
    public string? LocationDescription { get; set; }

    [MaxLength(30)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

}
