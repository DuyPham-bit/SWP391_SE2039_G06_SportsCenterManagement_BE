using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services.Catalog;

public sealed record CampusRoomDto(long Id, string Name, string? NameEn, string? Kind, int? Floor,
    double X, double Z, double Width, double Depth, double Rotation, int Capacity, bool Bookable, string Status);

public sealed record CampusZoneDto(long Id, string Code, string Kind, string NameVi, string NameEn,
    string? DescriptionVi, string? DescriptionEn, string Color, double X, double Z, double Width, double Depth,
    double AreaM2, double BuildingWidth, double BuildingDepth, int Floors, string? OpenTime, string? CloseTime,
    string ConfiguredStatus, string Status, string? ImageUrl, IReadOnlyList<string> Amenities, JsonElement Features,
    IReadOnlyList<CampusRoomDto> Rooms, DateTime UpdatedAt);

public sealed record CampusMapDto(long CenterId, string CenterName, string? OpeningTime, string? ClosingTime,
    double SiteWidth, double SiteDepth, double SiteAreaM2, double ZonesAreaM2, DateTime GeneratedAt,
    IReadOnlyList<CampusZoneDto> Zones);

public sealed record UpdateCampusZoneRequest(string NameVi, string NameEn, string? DescriptionVi, string? DescriptionEn,
    string ColorHex, string? OpenTime, string? CloseTime, string Status, string? ImageUrl, IReadOnlyList<string>? Amenities);

/// <summary>Single source of truth for the campus map (3D model, 2D plan, zone navigation and detail panel).</summary>
public sealed class CampusMapService(IUnitOfWork unitOfWork, IBusinessClock clock)
{
    // Data access goes through the unit of work (same scoped DbContext instance).
    private readonly SportsCenterDbContext db = unitOfWork.Context;

    public static readonly string[] ZoneStatuses = { "Open", "Maintenance", "Full", "Closed" };
    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public async Task<CampusMapDto> GetAsync(long? centerId, CancellationToken ct)
    {
        var center = centerId is { } id
            ? await db.Centers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct)
            : await db.Centers.AsNoTracking()
                .Where(c => db.CampusZones.Any(z => z.CenterId == c.Id))
                .OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        if (center == null) throw FlowException.NotFound("Không tìm thấy cơ sở có sơ đồ khuôn viên.");

        var zones = await db.CampusZones.AsNoTracking().Where(z => z.CenterId == center.Id)
            .OrderBy(z => z.SortOrder).ThenBy(z => z.Code).ToListAsync(ct);
        var zoneIds = zones.Select(z => z.Id).ToList();
        var rooms = await db.Rooms.AsNoTracking()
            .Where(r => r.ZoneId != null && zoneIds.Contains(r.ZoneId.Value))
            .OrderBy(r => r.FloorNo).ThenBy(r => r.Name).ToListAsync(ct);

        var now = clock.BusinessNow;
        var today = DateOnly.FromDateTime(now);
        var time = TimeOnly.FromDateTime(now);
        var roomIds = rooms.Select(r => r.Id).ToList();
        var busyRoomIds = (await db.ClassSessions.AsNoTracking()
                .Where(s => s.RoomId != null && roomIds.Contains(s.RoomId.Value) && s.SessionDate == today
                            && s.StartTime <= time && s.EndTime > time)
                .Select(s => new { s.RoomId, s.SessionStatus }).ToListAsync(ct))
            .Where(s => !s.SessionStatus.Contains("cancel", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.RoomId!.Value).ToHashSet();

        var zoneDtos = zones.Select(zone =>
        {
            var zoneRooms = rooms.Where(r => r.ZoneId == zone.Id)
                .Select(r => new CampusRoomDto(r.Id, r.Name, r.NameEn, r.MapKind, r.FloorNo,
                    r.MapX ?? zone.PosX, r.MapZ ?? zone.PosZ, r.MapWidthM ?? 0, r.MapDepthM ?? 0, r.MapRotationDeg ?? 0,
                    r.Capacity, r.Bookable, RoomStatus(r.Status, busyRoomIds.Contains(r.Id))))
                .ToList();
            return new CampusZoneDto(zone.Id, zone.Code, zone.Kind, zone.NameVi, zone.NameEn,
                zone.DescriptionVi, zone.DescriptionEn, zone.ColorHex, zone.PosX, zone.PosZ, zone.WidthM, zone.DepthM,
                Math.Round(zone.WidthM * zone.DepthM, 2), zone.BuildingWidthM, zone.BuildingDepthM, zone.Floors,
                Format(zone.OpenTime), Format(zone.CloseTime), zone.Status,
                ZoneStatus(zone.Status, zone.OpenTime, zone.CloseTime, time, zoneRooms.Select(r => r.Status).ToList()),
                zone.ImageUrl, ParseList(zone.AmenitiesJson), ParseElement(zone.FeaturesJson), zoneRooms, zone.UpdatedAt);
        }).ToList();

        // The site is the bounding box of the parcels; parcels are meant to tile it exactly.
        double minX = 0, maxX = 0, minZ = 0, maxZ = 0;
        if (zones.Count > 0)
        {
            minX = zones.Min(z => z.PosX - z.WidthM / 2); maxX = zones.Max(z => z.PosX + z.WidthM / 2);
            minZ = zones.Min(z => z.PosZ - z.DepthM / 2); maxZ = zones.Max(z => z.PosZ + z.DepthM / 2);
        }
        var siteWidth = maxX - minX;
        var siteDepth = maxZ - minZ;
        return new CampusMapDto(center.Id, center.Name, Format(center.OpeningTime), Format(center.ClosingTime),
            siteWidth, siteDepth, Math.Round(siteWidth * siteDepth, 2), Math.Round(zoneDtos.Sum(z => z.AreaM2), 2),
            clock.UtcNow, zoneDtos);
    }

    public async Task<CampusZoneDto> UpdateZoneAsync(long zoneId, UpdateCampusZoneRequest request, long userId, CancellationToken ct)
    {
        var zone = await db.CampusZones.SingleOrDefaultAsync(z => z.Id == zoneId, ct)
                   ?? throw FlowException.NotFound("Không tìm thấy khu.");
        if (string.IsNullOrWhiteSpace(request.NameVi) || string.IsNullOrWhiteSpace(request.NameEn))
            throw FlowException.BadRequest("Tên khu (VI và EN) là bắt buộc.");
        if (request.NameVi.Trim().Length > 120 || request.NameEn.Trim().Length > 120)
            throw FlowException.BadRequest("Tên khu tối đa 120 ký tự.");
        if (!HexColor.IsMatch(request.ColorHex ?? string.Empty))
            throw FlowException.BadRequest("Màu nhận diện phải có dạng #RRGGBB.");
        if (!ZoneStatuses.Contains(request.Status))
            throw FlowException.BadRequest("Trạng thái khu không hợp lệ.");
        var open = ParseTime(request.OpenTime);
        var close = ParseTime(request.CloseTime);
        if (open is null != close is null) throw FlowException.BadRequest("Cần nhập đủ giờ mở và giờ đóng cửa.");
        if (open is { } o && close is { } c && c <= o) throw FlowException.BadRequest("Giờ đóng cửa phải sau giờ mở cửa.");

        var previous = new { zone.NameVi, zone.NameEn, zone.ColorHex, zone.Status, zone.OpenTime, zone.CloseTime };
        zone.NameVi = request.NameVi.Trim();
        zone.NameEn = request.NameEn.Trim();
        zone.DescriptionVi = Trim(request.DescriptionVi, 500);
        zone.DescriptionEn = Trim(request.DescriptionEn, 500);
        zone.ColorHex = request.ColorHex!.ToUpperInvariant();
        zone.OpenTime = open;
        zone.CloseTime = close;
        zone.Status = request.Status;
        zone.ImageUrl = Trim(request.ImageUrl, 500);
        if (request.Amenities != null)
            zone.AmenitiesJson = JsonSerializer.Serialize(request.Amenities.Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim()).Distinct().Take(30));
        zone.UpdatedAt = clock.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId, Action = "campus.zone.updated", EntityType = "CampusZone", EntityId = zone.Id,
            OldValues = JsonSerializer.Serialize(previous),
            NewValues = JsonSerializer.Serialize(new { zone.NameVi, zone.NameEn, zone.ColorHex, zone.Status, zone.OpenTime, zone.CloseTime }),
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);

        var map = await GetAsync(zone.CenterId, ct);
        return map.Zones.Single(z => z.Id == zone.Id);
    }

    public async Task<long> GetZoneCenterIdAsync(long zoneId, CancellationToken ct) =>
        await db.CampusZones.Where(z => z.Id == zoneId).Select(z => (long?)z.CenterId).SingleOrDefaultAsync(ct)
        ?? throw FlowException.NotFound("Không tìm thấy khu.");

    /// <summary>Available | InUse | Maintenance | Inactive</summary>
    public static string RoomStatus(string roomStatus, bool hasRunningSession) =>
        roomStatus.Equals("Maintenance", StringComparison.OrdinalIgnoreCase) ? "Maintenance"
        : !roomStatus.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "Inactive"
        : hasRunningSession ? "InUse" : "Available";

    /// <summary>Configured Maintenance/Closed wins, then opening hours, then "Full" when every active room is in use.</summary>
    public static string ZoneStatus(string configured, TimeOnly? open, TimeOnly? close, TimeOnly now, IReadOnlyList<string> roomStatuses)
    {
        if (configured is "Maintenance" or "Closed" or "Full") return configured;
        if (open is { } o && close is { } c && (now < o || now >= c)) return "Closed";
        var active = roomStatuses.Where(s => s is "Available" or "InUse").ToList();
        return active.Count > 0 && active.All(s => s == "InUse") ? "Full" : "Open";
    }

    private static string? Format(TimeOnly? time) => time?.ToString("HH:mm");

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null
        : TimeOnly.TryParse(value, out var parsed) ? parsed
        : throw FlowException.BadRequest("Giờ không hợp lệ (HH:mm).");

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim() is var v && v.Length > max ? v[..max] : value.Trim();

    private static IReadOnlyList<string> ParseList(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static JsonElement ParseElement(string json)
    {
        try { return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(json) ? "[]" : json); }
        catch (JsonException) { return JsonSerializer.Deserialize<JsonElement>("[]"); }
    }
}
