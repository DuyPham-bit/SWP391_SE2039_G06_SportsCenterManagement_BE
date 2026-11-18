using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SportsCenterManagement.DAL.Entities;

/// <summary>
/// One land parcel of a center's campus map. Parcels tile the site, so their areas sum to the site area.
/// Coordinates are meters from the site center: X grows east, Z grows south.
/// </summary>
[Table("campus_zones")]
public class CampusZone : Common.BaseEntity
{
    [Column("center_id")]
    public long CenterId { get; set; }

    /// <summary>Stable short code shown on the map (A, B, C, OUT...).</summary>
    [MaxLength(20)]
    [Column("code")]
    public string Code { get; set; } = null!;

    /// <summary>building | outdoor | service | parking | landscape</summary>
    [MaxLength(20)]
    [Column("kind")]
    public string Kind { get; set; } = null!;

    [MaxLength(120)]
    [Column("name_vi")]
    public string NameVi { get; set; } = null!;

    [MaxLength(120)]
    [Column("name_en")]
    public string NameEn { get; set; } = null!;

    [MaxLength(500)]
    [Column("description_vi")]
    public string? DescriptionVi { get; set; }

    [MaxLength(500)]
    [Column("description_en")]
    public string? DescriptionEn { get; set; }

    [MaxLength(9)]
    [Column("color_hex")]
    public string ColorHex { get; set; } = null!;

    [Column("pos_x")]
    public double PosX { get; set; }

    [Column("pos_z")]
    public double PosZ { get; set; }

    [Column("width_m")]
    public double WidthM { get; set; }

    [Column("depth_m")]
    public double DepthM { get; set; }

    /// <summary>Building footprint inside the parcel (0 for open-air parcels).</summary>
    [Column("building_width_m")]
    public double BuildingWidthM { get; set; }

    [Column("building_depth_m")]
    public double BuildingDepthM { get; set; }

    [Column("floors")]
    public int Floors { get; set; }

    [Column("open_time")]
    public TimeOnly? OpenTime { get; set; }

    [Column("close_time")]
    public TimeOnly? CloseTime { get; set; }

    /// <summary>Open | Maintenance | Full | Closed</summary>
    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = null!;

    [MaxLength(500)]
    [Column("image_url")]
    public string? ImageUrl { get; set; }

    /// <summary>Amenity codes (locker, shower, wc, wc_accessible, first_aid...) as a JSON array.</summary>
    [Column("amenities_json")]
    public string AmenitiesJson { get; set; } = "[]";

    /// <summary>Map features (gates, exits, hydrants, paths, stands, lights...) as a JSON array.</summary>
    [Column("features_json")]
    public string FeaturesJson { get; set; } = "[]";

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
