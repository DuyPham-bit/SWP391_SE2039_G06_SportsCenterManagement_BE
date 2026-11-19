using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Seed;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.Tests.BLL.Catalog;

public class CampusMapTests
{
    private sealed class FixedClock(DateTime businessNow) : IBusinessClock
    {
        public DateTime UtcNow => businessNow.AddHours(-7);
        public DateTime BusinessNow => businessNow;
        public DateOnly Today => DateOnly.FromDateTime(businessNow);
        public TimeZoneInfo TimeZone => TimeZoneInfo.Utc;
        public DateTime ToBusinessTime(DateTime utcDateTime) => utcDateTime.AddHours(7);
        public DateTime ToUtcTime(DateTime businessDateTime) => businessDateTime.AddHours(-7);
        public DateOnly ToBusinessDate(DateTime utcDateTime) => DateOnly.FromDateTime(utcDateTime.AddHours(7));
        public DateTime StartOfBusinessDayUtc(DateOnly date) => date.ToDateTime(TimeOnly.MinValue).AddHours(-7);
        public DateTime EndOfBusinessDayUtc(DateOnly date) => date.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7);
    }

    private static readonly DateTime Noon = new(2026, 11, 18, 12, 0, 0);

    private static async Task<(SportsCenterDbContext Db, CampusMapService Service)> SeededAsync(DateTime? now = null)
    {
        var db = TestDb.Create();
        db.Centers.Add(new Center { Name = "SCMS", Address = "HCM", Status = "Active", CreatedAt = TestDb.Now });
        await db.SaveChangesAsync();
        await CampusMapSeed.EnsureAsync(db, TestDb.Now);
        return (db, new CampusMapService(new UnitOfWork(db), new FixedClock(now ?? Noon)));
    }

    [Fact]
    public async Task Parcels_tile_the_13000_m2_site_without_overlap()
    {
        var (_, service) = await SeededAsync();
        var map = await service.GetAsync(null, CancellationToken.None);

        Assert.Equal(130, map.SiteWidth);
        Assert.Equal(100, map.SiteDepth);
        Assert.Equal(13000, map.SiteAreaM2);
        Assert.Equal(map.SiteAreaM2, map.ZonesAreaM2);
        foreach (var a in map.Zones)
        foreach (var b in map.Zones.Where(b => b.Id > a.Id))
        {
            var overlapX = Math.Min(a.X + a.Width / 2, b.X + b.Width / 2) - Math.Max(a.X - a.Width / 2, b.X - b.Width / 2);
            var overlapZ = Math.Min(a.Z + a.Depth / 2, b.Z + b.Depth / 2) - Math.Max(a.Z - a.Depth / 2, b.Z - b.Depth / 2);
            Assert.False(overlapX > 0.001 && overlapZ > 0.001, $"{a.Code} overlaps {b.Code}");
        }
    }

    [Fact]
    public async Task Rooms_sit_inside_their_zone_and_use_standard_court_sizes()
    {
        var (_, service) = await SeededAsync();
        var map = await service.GetAsync(null, CancellationToken.None);
        var rooms = map.Zones.SelectMany(z => z.Rooms.Select(r => (Zone: z, Room: r))).ToList();

        foreach (var (zone, room) in rooms)
        {
            Assert.True(room.X - room.Width / 2 >= zone.X - zone.Width / 2 - 0.001, room.Name);
            Assert.True(room.X + room.Width / 2 <= zone.X + zone.Width / 2 + 0.001, room.Name);
            Assert.True(room.Z - room.Depth / 2 >= zone.Z - zone.Depth / 2 - 0.001, room.Name);
            Assert.True(room.Z + room.Depth / 2 <= zone.Z + zone.Depth / 2 + 0.001, room.Name);
        }

        (double, double) Size(string kind) => rooms.Where(r => r.Room.Kind == kind)
            .Select(r => (Math.Max(r.Room.Width, r.Room.Depth), Math.Min(r.Room.Width, r.Room.Depth))).Distinct().Single();
        Assert.Equal((25, 12.5), Size("pool25"));
        Assert.Equal((28, 15), Size("basketball"));
        Assert.Equal((13.4, 6.1), Size("badminton"));
        Assert.Equal((13.4, 6.1), Size("pickleball"));
        Assert.Equal((40, 20), Size("football5"));
        Assert.Equal((45, 28), Size("football7"));
        Assert.Equal(4, rooms.Count(r => r.Room.Kind == "badminton"));
        Assert.Equal(4, rooms.Count(r => r.Room.Kind == "pickleball"));
    }

    /// <summary>Writes the seeded API payload as the frontend contract fixture when CAMPUS_FIXTURE_OUT is set.</summary>
    [Fact]
    public async Task Campus_map_payload_snapshot()
    {
        var (_, service) = await SeededAsync();
        var map = await service.GetAsync(null, CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(map,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web) { WriteIndented = true });
        Assert.Contains("\"zones\"", json);
        var output = Environment.GetEnvironmentVariable("CAMPUS_FIXTURE_OUT");
        if (!string.IsNullOrWhiteSpace(output)) await File.WriteAllTextAsync(output, json);
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent()
    {
        var (db, _) = await SeededAsync();
        var zones = db.CampusZones.Count();
        var rooms = db.Rooms.Count();
        await CampusMapSeed.EnsureAsync(db, TestDb.Now);
        Assert.Equal(zones, db.CampusZones.Count());
        Assert.Equal(rooms, db.Rooms.Count());
    }

    [Fact]
    public async Task Live_status_reflects_running_sessions_and_hours()
    {
        var (db, service) = await SeededAsync();
        var pool = db.Rooms.Single(r => r.MapKind == "pool25");
        db.ClassSessions.Add(new ClassSession
        {
            ClassId = 1, RoomId = pool.Id, SessionDate = DateOnly.FromDateTime(Noon),
            StartTime = new TimeOnly(11, 30), EndTime = new TimeOnly(12, 30), SessionStatus = "Scheduled", CreatedAt = TestDb.Now
        });
        await db.SaveChangesAsync();

        var map = await service.GetAsync(null, CancellationToken.None);
        var zoneA = map.Zones.Single(z => z.Code == "A");
        Assert.Equal("InUse", zoneA.Rooms.Single(r => r.Kind == "pool25").Status);
        Assert.Equal("Available", zoneA.Rooms.Single(r => r.Kind == "gym").Status);
        Assert.Equal("Open", zoneA.Status);

        var night = await new CampusMapService(new UnitOfWork(db), new FixedClock(Noon.Date.AddHours(23))).GetAsync(null, CancellationToken.None);
        Assert.Equal("Closed", night.Zones.Single(z => z.Code == "A").Status);
        Assert.Equal("Open", night.Zones.Single(z => z.Code == "G").Status); // landscape has no hours
    }

    [Theory]
    [InlineData("Maintenance", "Maintenance")]
    [InlineData("Open", "Full")]
    public void Zone_status_rules(string configured, string expected) =>
        Assert.Equal(expected, CampusMapService.ZoneStatus(configured, new TimeOnly(6, 0), new TimeOnly(22, 0),
            new TimeOnly(12, 0), configured == "Open" ? new[] { "InUse", "InUse", "Maintenance" } : new[] { "Available" }));

    [Fact]
    public async Task Updating_a_zone_changes_the_single_source_and_is_audited()
    {
        var (db, service) = await SeededAsync();
        var zone = db.CampusZones.Single(z => z.Code == "C");
        var updated = await service.UpdateZoneAsync(zone.Id, new UpdateCampusZoneRequest(
            "Khu C – Võ thuật & Yoga", "Zone C – Combat & Yoga", null, null, "#112233", "07:00", "20:00", "Maintenance", null,
            new[] { "locker", "locker", " wc " }), 9, CancellationToken.None);

        Assert.Equal("Khu C – Võ thuật & Yoga", updated.NameVi);
        Assert.Equal("Maintenance", updated.Status);
        Assert.Equal(new[] { "locker", "wc" }, updated.Amenities);
        Assert.Single(db.AuditLogs.Where(a => a.Action == "campus.zone.updated" && a.EntityId == zone.Id));

        await Assert.ThrowsAsync<FlowException>(() => service.UpdateZoneAsync(zone.Id, new UpdateCampusZoneRequest(
            "x", "x", null, null, "red", null, null, "Open", null, null), 9, CancellationToken.None));
        await Assert.ThrowsAsync<FlowException>(() => service.UpdateZoneAsync(zone.Id, new UpdateCampusZoneRequest(
            "x", "x", null, null, "#000000", "20:00", "07:00", "Open", null, null), 9, CancellationToken.None));
    }
}
