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

    /// <summary>Shown on the public landing page; counted for the home-content "x/y visible" badges.</summary>
    [Column("is_visible_on_landing")]
    public bool IsVisibleOnLanding { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // Campus map placement (all optional; rooms without a zone are simply not drawn).
    [Column("zone_id")]
    public long? ZoneId { get; set; }

    [MaxLength(100)]
    [Column("name_en")]
    public string? NameEn { get; set; }

    /// <summary>Drawing template: pool25, basketball, badminton, pickleball, football5, football7, table_tennis, gym, combat, studio, golf_sim, locker_m, locker_f, wc, medical, reception, cafe, car_park, moto_park.</summary>
    [MaxLength(30)]
    [Column("map_kind")]
    public string? MapKind { get; set; }

    [Column("floor_no")]
    public int? FloorNo { get; set; }

    [Column("map_x")]
    public double? MapX { get; set; }

    [Column("map_z")]
    public double? MapZ { get; set; }

    [Column("map_width_m")]
    public double? MapWidthM { get; set; }

    [Column("map_depth_m")]
    public double? MapDepthM { get; set; }

    [Column("map_rotation_deg")]
    public double? MapRotationDeg { get; set; }

    /// <summary>Whether members can book this room/court from the map.</summary>
    [Column("bookable")]
    public bool Bookable { get; set; }
}
