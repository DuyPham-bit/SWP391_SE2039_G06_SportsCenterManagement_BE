using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Entities;

using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.DAL.Seed;

/// <summary>
/// Reference campus layout for the demo center: 130 m x 100 m site (X[-65,65], Z[-50,50], north = -Z)
/// split into 7 parcels that tile the site exactly (sum = 13,000 m2). Court sizes follow common standards:
/// pool 25 x 12.5 m (6 lanes), basketball 28 x 15 m, badminton/pickleball 13.4 x 6.1 m,
/// 5-a-side 40 x 20 m, 7-a-side 45 x 28 m (typical VN artificial-turf sizes).
/// Seeding is idempotent: it only creates zones that are missing and only places rooms that are not placed yet.
/// </summary>
public static class CampusMapSeed
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Old seeded room names that contradicted the map (e.g. a 50 m pool on a 25 m footprint).</summary>
    public static readonly (string OldName, string NewName)[] LegacyRoomRenames =
    {
        ("Bể bơi Olympic 50m", "Bể bơi 25m (6 làn)"),
        ("Cụm Sân Pickleball Ngoài Trời", "Sân Pickleball #1")
    };

    public static async Task RenameLegacyRoomsAsync(SportsCenterDbContext context)
    {
        foreach (var (oldName, newName) in LegacyRoomRenames)
        {
            var room = await context.Rooms.FirstOrDefaultAsync(r => r.Name == oldName);
            if (room == null || await context.Rooms.AnyAsync(r => r.Name == newName)) continue;
            room.Name = newName;
        }
        await context.SaveChangesAsync();
    }

    private sealed record ZoneDef(string Code, string Kind, string NameVi, string NameEn, string DescVi, string DescEn,
        string Color, double X, double Z, double W, double D, double Bw, double Bd, int Floors,
        TimeOnly? Open, TimeOnly? Close, string[] Amenities, object[] Features, int Sort);

    private sealed record RoomDef(string ZoneCode, string Name, string NameEn, string Kind, string RoomType, int Capacity,
        int Floor, double X, double Z, double W, double D, double Rot = 0, bool Bookable = true);

    private static TimeOnly T(int h, int m = 0) => new(h, m);

    private static object Space(string variant, double x, double z, double w, double d, int floor, string vi, string en) =>
        new { type = "space", variant, x, z, w, d, floor, labelVi = vi, labelEn = en };

    private static object Point(string type, double x, double z, string? variant = null, int floor = 0) =>
        new { type, x, z, variant, floor };

    private static object Path(bool canopy, params double[][] points) => new { type = "path", canopy, points };

    private static double[] P(double x, double z) => new[] { x, z };

    private static readonly ZoneDef[] Zones =
    {
        new("B", "building", "Khu B – Nhà thi đấu đa năng", "Zone B – Multi-sport Arena",
            "Tầng 1 sân bóng rổ 28×15 m có khán đài, tầng 2 bốn sân cầu lông, tầng 3 khu bóng bàn.",
            "Floor 1 basketball court 28×15 m with stand, floor 2 four badminton courts, floor 3 table tennis hall.",
            "#F59E0B", -40, -30, 50, 40, 44, 34, 3, T(6), T(22),
            new[] { "locker", "shower", "wc", "wc_accessible", "extinguisher", "water" },
            new object[]
            {
                new { type = "stand", x = -40.0, z = -19.5, w = 28.0, d = 4.0, floor = 1, rot = 0.0 },
                Space("locker_m", -59.5, -41, 5, 10, 1, "Thay đồ nam", "Men's locker"),
                Space("locker_f", -59.5, -30, 5, 10, 1, "Thay đồ nữ", "Women's locker"),
                Space("wc", -59.5, -20.5, 5, 7, 1, "WC nam/nữ/khuyết tật", "WC men/women/accessible"),
                Point("ramp", -16.5, -30), Point("extinguisher", -19, -27), Point("extinguisher", -61, -46),
                Point("exit", -62, -14, "building"), Point("exit", -65, -30, "site")
            }, 2),
        new("A", "building", "Khu A – Bơi lội & Gym", "Zone A – Aquatics & Gym",
            "Tầng 1 bể bơi 25 m (6 làn) cùng phòng thay đồ nam/nữ tách biệt, tầng 2 phòng Gym & Fitness.",
            "Floor 1 25 m pool (6 lanes) with separate men's/women's lockers, floor 2 gym & fitness.",
            "#0EA5E9", -40, 7.5, 50, 35, 44, 29, 2, T(5, 30), T(21, 30),
            new[] { "locker", "shower", "wc", "wc_accessible", "first_aid", "extinguisher" },
            new object[]
            {
                Space("locker_m", -23, -1, 8, 10, 1, "Thay đồ & tắm nam", "Men's locker & shower"),
                Space("locker_f", -23, 10, 8, 10, 1, "Thay đồ & tắm nữ", "Women's locker & shower"),
                Space("wc", -23, 19, 8, 5, 1, "WC khuyết tật", "Accessible WC"),
                Point("lifeguard", -44, -3, null, 1), Point("ramp", -16.5, 7.5),
                Point("extinguisher", -19, 4), Point("extinguisher", -61, 20),
                Point("exit", -62, 20, "building"), Point("exit", -65, 7.5, "site")
            }, 1),
        new("C", "building", "Khu C – Võ thuật, Golf & Yoga", "Zone C – Combat, Golf & Yoga",
            "Tầng 1 võ đài Boxing & Kickfit, tầng 2 phòng golf mô phỏng 3D, tầng 3 phòng Yoga & Pilates Reformer.",
            "Floor 1 boxing & kickfit ring, floor 2 3D golf simulator, floor 3 yoga & Pilates reformer studio.",
            "#F43F5E", -40, 37.5, 50, 25, 42, 19, 3, T(6), T(21),
            new[] { "locker", "shower", "wc", "extinguisher" },
            new object[]
            {
                Space("locker_m", -24, 33.5, 8, 7, 1, "Thay đồ nam", "Men's locker"),
                Space("locker_f", -24, 41.5, 8, 7, 1, "Thay đồ nữ", "Women's locker"),
                Point("ramp", -17.5, 37.5), Point("extinguisher", -20, 30),
                Point("exit", -61, 46, "building"), Point("exit", -65, 37.5, "site")
            }, 3),
        new("L", "service", "Sảnh chính – Lễ tân, Căn-tin & Y tế", "Main Lobby – Reception, Café & Clinic",
            "Cổng chính, quầy lễ tân và đặt sân, căn-tin, trạm y tế sơ cứu, WC nam/nữ/người khuyết tật.",
            "Main gate, reception & booking desk, café, first-aid clinic, men's/women's/accessible WC.",
            "#A78BFA", -5, 32.5, 20, 35, 16, 14, 1, T(5, 30), T(22),
            new[] { "reception", "cafe", "first_aid", "wc", "wc_accessible", "wifi", "extinguisher" },
            new object[]
            {
                Space("reception", -5, 36.5, 16, 6, 1, "Quầy lễ tân & đặt sân", "Reception & booking"),
                Space("cafe", -9.5, 29.5, 7, 8, 1, "Căn-tin", "Café"),
                Space("medical", -3.5, 29.5, 5, 8, 1, "Trạm y tế", "First-aid clinic"),
                Space("wc", 1, 29.5, 4, 8, 1, "WC nam/nữ/khuyết tật", "WC men/women/accessible"),
                new { type = "gate", x = -5.0, z = 50.0, w = 12.0, variant = "main" },
                Point("ramp", -5, 41), Point("sign", -12, 47, "directory"), Point("hydrant", 3, 48),
                Point("extinguisher", 2, 39)
            }, 0),
        new("G", "landscape", "Trục cảnh quan & lối đi có mái che", "Green Spine & Covered Walkway",
            "Trục đi bộ trung tâm có mái che, cây xanh, biển chỉ dẫn và lối thoát hiểm phía Bắc.",
            "Central covered walkway with trees, wayfinding signs and the north emergency exit.",
            "#84CC16", -5, -17.5, 20, 65, 0, 0, 0, null, null,
            new[] { "shade", "water", "sign" },
            new object[]
            {
                // Walk network (shared by every zone for wayfinding): nodes are path vertices, joined where coordinates match.
                Path(false, P(-5, 50), P(-5, 42)),
                Path(true, P(-5, 42), P(-5, 39.5)),
                Path(true, P(-5, 25.5), P(-5, 22), P(-5, 7.5), P(-5, 2.4), P(-5, -30), P(-5, -46)),
                Path(false, P(-5, 39.5), P(-5, 25.5)),
                Path(true, P(-5, 7.5), P(-16.5, 7.5)),
                Path(true, P(-5, -30), P(-16.5, -30)),
                Path(true, P(-5, 42), P(-14, 42), P(-14, 37.5), P(-17.5, 37.5)),
                Path(false, P(-5, 2.4), P(4, 2.4), P(12.6, 2.4), P(21.7, 2.4), P(30.8, 2.4), P(39.9, 2.4), P(42, 2.4), P(50, 2.4)),
                Path(false, P(22, 2.4), P(22, 1)),
                Path(false, P(42, 2.4), P(42, -7)),
                Path(false, P(50, 2.4), P(50, 8)),
                Path(false, P(-5, 42), P(6, 42), P(6, 47.5), P(41, 47.5)),
                new { type = "exit", x = -5.0, z = -50.0, variant = "site" },
                Point("sign", -7, -28), Point("sign", -7, 9.5), Point("sign", -7, 22), Point("sign", 3, 4),
                Point("hydrant", -14, -46), Point("hydrant", -14, -12), Point("hydrant", -14, 22),
                new { type = "tree_row", from = P(-12, -47), to = P(-12, 12), spacing = 6.0 },
                new { type = "tree_row", from = P(2, -47), to = P(2, -2), spacing = 6.0 }
            }, 5),
        new("O", "outdoor", "Khu sân ngoài trời – Bóng đá & Pickleball", "Outdoor Courts – Football & Pickleball",
            "Sân 7 người 45×28 m và sân 5 người 40×20 m cỏ nhân tạo có khán đài, 4 sân Pickleball 13,4×6,1 m, đèn pha LED.",
            "7-a-side 45×28 m and 5-a-side 40×20 m turf pitches with stands, four 13.4×6.1 m pickleball courts, LED floodlights.",
            "#10B981", 35, -12.5, 60, 75, 0, 0, 0, T(5, 30), T(22),
            new[] { "floodlight", "stand", "wc", "locker", "water", "extinguisher" },
            new object[]
            {
                new { type = "stand", x = 40.0, z = -23.5, w = 3.0, d = 30.0, floor = 0, rot = 90.0 },
                new { type = "stand", x = 53.0, z = -3.5, w = 20.0, d = 3.0, floor = 0, rot = 0.0 },
                Space("kiosk", 55, 13, 16, 10, 0, "Nhà thay đồ & WC ngoài trời (nam/nữ)", "Outdoor lockers & WC (men/women)"),
                Point("light", 6, -48), Point("light", 38, -48), Point("light", 6, -23.5), Point("light", 38, -23.5),
                Point("light", 6, 1), Point("light", 38, 1), Point("light", 41.5, -48.5), Point("light", 64.5, -48.5),
                Point("light", 41.5, -5.5), Point("light", 64.5, -5.5), Point("light", 8, 22.5), Point("light", 26, 22.5),
                Point("light", 44.5, 22.5), Point("hydrant", 64, 24), Point("hydrant", 6, -12), Point("extinguisher", 47, 8),
                Point("exit", 65, -20, "site"),
                new { type = "tree_row", from = P(8, 24), to = P(44, 24), spacing = 6.0 },
                new { type = "tree_row", from = P(64, -46), to = P(64, -10), spacing = 6.0 }
            }, 4),
        new("P", "parking", "Bãi đỗ xe ô tô & xe máy", "Car & Motorbike Parking",
            "26 ô ô tô (2 ô cho người khuyết tật, 4 ô sạc điện, mái pin mặt trời) và khu xe máy có mái che 160 chỗ.",
            "26 car bays (2 accessible, 4 EV chargers, solar canopy) and a covered 160-space motorbike area.",
            "#06B6D4", 35, 37.5, 60, 25, 0, 0, 0, T(5), T(23),
            new[] { "parking", "moto", "ev", "accessible_parking", "security" },
            new object[]
            {
                new { type = "parking_car", x = 23.25, z = 37.5, w = 32.5, d = 16.0, count = 26, accessible = 2, ev = 4 },
                new { type = "parking_moto", x = 53.0, z = 37.5, w = 20.0, d = 19.0, count = 160 },
                new { type = "gate", x = 23.0, z = 50.0, w = 8.0, variant = "vehicle" },
                Point("hydrant", 40, 26), Point("sign", 6, 44, "parking"),
                new { type = "tree_row", from = P(8, 27), to = P(62, 27), spacing = 7.0 }
            }, 6)
    };

    private static readonly RoomDef[] Rooms =
    {
        new("B", "Sân Bóng Rổ Tiêu Chuẩn Phong Trào", "Basketball Court (28×15 m)", "basketball", "Basketball Court", 25, 1, -40, -32, 28, 15),
        new("B", "Sân Cầu lông Tiêu chuẩn #1", "Badminton Court #1", "badminton", "Badminton Court", 6, 2, -52.3, -30, 6.1, 13.4),
        new("B", "Sân Cầu lông Tiêu chuẩn #2", "Badminton Court #2", "badminton", "Badminton Court", 6, 2, -44.1, -30, 6.1, 13.4),
        new("B", "Sân Cầu lông Tiêu chuẩn #3", "Badminton Court #3", "badminton", "Badminton Court", 6, 2, -35.9, -30, 6.1, 13.4),
        new("B", "Sân Cầu lông Tiêu chuẩn #4", "Badminton Court #4", "badminton", "Badminton Court", 6, 2, -27.7, -30, 6.1, 13.4),
        new("B", "Khu Bóng Bàn Trong Nhà", "Table Tennis Hall", "table_tennis", "Table Tennis Area", 20, 3, -40, -30, 32, 16),
        new("A", "Bể bơi 25m (6 làn)", "25 m Pool (6 lanes)", "pool25", "Swimming Pool", 30, 1, -44, 5, 25, 12.5),
        new("A", "Phòng Gym High-Performance", "High-Performance Gym", "gym", "Gym", 45, 2, -40, 7.5, 40, 25),
        new("C", "Võ đài Boxing & Kickfit", "Boxing & Kickfit Ring", "combat", "Combat Ring", 25, 1, -47, 37.5, 24, 15),
        new("C", "Tổ Hợp Sân Tập Golf & 3D Simulator", "Golf Academy & 3D Simulator", "golf_sim", "Golf Complex", 30, 2, -40, 37.5, 38, 15),
        new("C", "Phòng Yoga & Pilates Reformer", "Yoga & Pilates Reformer Studio", "studio", "Studio", 20, 3, -40, 37.5, 38, 15),
        new("O", "Sân Bóng Đá Cỏ Nhân Tạo (Sân 7)", "7-a-side Turf Pitch", "football7", "Outdoor Field", 20, 0, 22, -23.5, 28, 45),
        new("O", "Sân Bóng Đá Cỏ Nhân Tạo (Sân 5)", "5-a-side Turf Pitch", "football5", "Outdoor Field", 14, 0, 53, -27, 20, 40),
        new("O", "Sân Pickleball #1", "Pickleball Court #1", "pickleball", "Pickleball Court", 4, 0, 12.6, 13, 6.1, 13.4),
        new("O", "Sân Pickleball #2", "Pickleball Court #2", "pickleball", "Pickleball Court", 4, 0, 21.7, 13, 6.1, 13.4),
        new("O", "Sân Pickleball #3", "Pickleball Court #3", "pickleball", "Pickleball Court", 4, 0, 30.8, 13, 6.1, 13.4),
        new("O", "Sân Pickleball #4", "Pickleball Court #4", "pickleball", "Pickleball Court", 4, 0, 39.9, 13, 6.1, 13.4)
    };

    public static async Task EnsureAsync(SportsCenterDbContext context, DateTime utcNow)
    {
        var center = await context.Centers.FirstOrDefaultAsync(c => c.Id == 15)
                     ?? await context.Centers.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (center == null) return;

        var zones = await context.CampusZones.Where(z => z.CenterId == center.Id).ToListAsync();
        foreach (var def in Zones)
        {
            if (zones.Any(z => z.Code == def.Code)) continue;
            var zone = new CampusZone
            {
                CenterId = center.Id, Code = def.Code, Kind = def.Kind, NameVi = def.NameVi, NameEn = def.NameEn,
                DescriptionVi = def.DescVi, DescriptionEn = def.DescEn, ColorHex = def.Color,
                PosX = def.X, PosZ = def.Z, WidthM = def.W, DepthM = def.D,
                BuildingWidthM = def.Bw, BuildingDepthM = def.Bd, Floors = def.Floors,
                OpenTime = def.Open, CloseTime = def.Close, Status = "Open",
                AmenitiesJson = JsonSerializer.Serialize(def.Amenities, Json),
                FeaturesJson = JsonSerializer.Serialize(def.Features, Json),
                SortOrder = def.Sort, UpdatedAt = utcNow
            };
            context.CampusZones.Add(zone);
            zones.Add(zone);
        }
        await context.SaveChangesAsync();

        var byCode = zones.ToDictionary(z => z.Code);
        foreach (var def in Rooms)
        {
            var zone = byCode[def.ZoneCode];
            var room = await context.Rooms.FirstOrDefaultAsync(r => r.CenterId == center.Id && r.Name == def.Name);
            if (room == null)
            {
                room = new Room
                {
                    CenterId = center.Id, Name = def.Name, RoomType = def.RoomType, Capacity = def.Capacity,
                    Status = "Active", CreatedAt = utcNow
                };
                context.Rooms.Add(room);
            }
            if (room.ZoneId != null) continue; // already placed (possibly edited by staff) - keep it
            room.ZoneId = zone.Id;
            room.NameEn ??= def.NameEn;
            room.MapKind = def.Kind;
            room.FloorNo = def.Floor;
            room.MapX = def.X;
            room.MapZ = def.Z;
            room.MapWidthM = def.W;
            room.MapDepthM = def.D;
            room.MapRotationDeg = def.Rot;
            room.Bookable = def.Bookable;
            room.LocationDescription = def.Floor > 0 ? $"Khu {zone.Code} • Tầng {def.Floor}" : $"Khu {zone.Code} • Ngoài trời";
        }
        await context.SaveChangesAsync();
    }
}
