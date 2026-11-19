using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Seed;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.Tests.BLL.Catalog;

public class HomeContentTests
{
    private static LandingContentService Service(SportsCenterDbContext db) => new(new UnitOfWork(db), new ConfigurationBuilder().Build());

    /// <summary>Two sports whose names differ in word order from the seed cards, plus the seeded rooms they train in.</summary>
    private static async Task<SportsCenterDbContext> SeededDbAsync()
    {
        var db = TestDb.Create();
        db.Sports.AddRange(
            new Sport { Id = 101, Name = "Golf", Status = "Active" },
            new Sport { Id = 102, Name = "Boxing & Võ thuật", Status = "Active" },
            new Sport { Id = 103, Name = "Bơi lội", Status = "Active" });
        db.Rooms.AddRange(
            new Room { Id = 201, CenterId = 1, Name = "Tổ Hợp Sân Tập Golf & 3D Simulator", Capacity = 30, Status = "Active" },
            new Room { Id = 202, CenterId = 1, Name = DbInitializer.SportsCatalog.Single(c => c.SportName == "Bơi lội").RoomName, Capacity = 30, Status = "Active" });
        await db.SaveChangesAsync();
        await LandingContentService.EnsureSeededAsync(db);
        return db;
    }

    private static JsonObject Card(JsonNode doc, string list, string id) =>
        doc[list == "sportsList" ? "coursesSection" : "facilitiesSection"]![list]!.AsArray()
            .OfType<JsonObject>().Single(c => c["id"]!.GetValue<string>() == id);

    [Fact]
    public async Task Seed_IsValid_AndHasEnglishForEveryText()
    {
        using var db = await SeededDbAsync();
        var doc = JsonNode.Parse((await db.SystemSettings.SingleAsync()).SettingValue!)!;

        Assert.Empty(HomeContentDocument.Validate(doc));
        Assert.Equal("SHAPING RESILIENCE THROUGH SPORTS", HomeContentDocument.Text(doc["heroBanner"]!["title"], "en"));
        Assert.Contains("{{sportCount}}", HomeContentDocument.Text(doc["heroBanner"]!["description"], "vi"));
    }

    [Fact]
    public async Task Seed_PointsCardsAtSportAndRoomRows()
    {
        using var db = await SeededDbAsync();
        var doc = JsonNode.Parse((await db.SystemSettings.SingleAsync()).SettingValue!)!;

        Assert.Equal("201", Card(doc, "sportsList", "101")["facilityId"]!.GetValue<string>());
        Card(doc, "sportsList", "102"); // "Võ thuật & Boxing" matched regardless of word order
        // Pool card name differs from the room; matched through the swimming sport card's link.
        Assert.Equal("Bể Bơi 4 Mùa Nước Ấm 25m", HomeContentDocument.Text(Card(doc, "facilitiesList", "202")["name"], "vi"));
    }

    [Fact]
    public async Task HidingASport_UpdatesDbCounts()
    {
        using var db = await SeededDbAsync();
        var service = Service(db);
        var admin = await service.GetAdminAsync(null, 1, default);
        Assert.Equal(new VisibilityCount(10, 10), admin.Stats.Sports);

        var courses = JsonNode.Parse(admin.Content!.Value.GetRawText())!["coursesSection"]!;
        Card(courses.Root, "sportsList", "101")["isVisible"] = false;
        Card(courses.Root, "sportsList", "yoga-pilates")["isVisible"] = false;
        var saved = await service.SaveSectionAsync("coursesSection", JsonSerializer.SerializeToElement(courses), 1, null, default);

        Assert.Equal(new VisibilityCount(8, 10), saved.Stats.Sports);
        Assert.False((await db.Sports.SingleAsync(s => s.Id == 101)).IsVisibleOnLanding);
    }

    [Fact]
    public async Task Stats_CountOnlyRecordsShownAsCards_ElseEveryActiveRecord()
    {
        using var db = await SeededDbAsync();
        // An extra court of a cluster that has no card of its own.
        db.Rooms.Add(new Room { Id = 299, CenterId = 1, Name = "Sân Cầu lông #2", Capacity = 8, Status = "Active" });
        await db.SaveChangesAsync();
        var service = Service(db);

        var withCards = (await service.GetAdminAsync(null, 1, default)).Stats;
        Assert.Equal(new VisibilityCount(10, 10), withCards.Facilities); // all 10 facility cards count; unlinked room 299 does not
        Assert.Equal(new VisibilityCount(10, 10), withCards.Sports);

        // Catalog fallback (no cards and not custom): every active record counts.
        var noCards = await service.GetStatsAsync(null, new JsonObject(), default);
        Assert.Equal(new VisibilityCount(3, 3), noCards.Facilities);

        // Adding a custom card increments visible & total; clearing custom cards yields 0/0.
        var facilities = JsonNode.Parse((await service.GetAdminAsync(null, 1, default)).Content!.Value.GetRawText())!["facilitiesSection"]!;
        facilities["facilitiesList"]!.AsArray().Add(new JsonObject
        {
            ["id"] = "facilities-custom-1",
            ["name"] = new JsonObject { ["vi"] = "Sân mới", ["en"] = "New court" },
            ["isVisible"] = true
        });
        var added = await service.SaveSectionAsync("facilitiesSection", JsonSerializer.SerializeToElement(facilities), 1, null, default);
        Assert.Equal(new VisibilityCount(11, 11), added.Stats.Facilities);

        facilities["facilitiesList"] = new JsonArray();
        facilities["catalogSource"] = "custom";
        var cleared = await service.SaveSectionAsync("facilitiesSection", JsonSerializer.SerializeToElement(facilities), 1, null, default);
        Assert.Equal(new VisibilityCount(0, 0), cleared.Stats.Facilities);
    }

    [Fact]
    public async Task PublicRead_FallsBackToVietnamese_WhenEnglishIsEmpty()
    {
        using var db = await SeededDbAsync();
        var service = Service(db);
        var hero = JsonNode.Parse((await service.GetAdminAsync(null, 1, default)).Content!.Value.GetRawText())!["heroBanner"]!;
        hero["badge"] = new JsonObject { ["vi"] = "HUY HIỆU", ["en"] = "" };
        var saved = await service.SaveSectionAsync("heroBanner", JsonSerializer.SerializeToElement(hero), 1, null, default);

        Assert.Contains("heroBanner.badge", saved.MissingTranslations);
        var en = await service.GetPublicAsync("en", null, default);
        Assert.Equal("en", en.Lang);
        Assert.Equal("HUY HIỆU", en.Content!.Value.GetProperty("heroBanner").GetProperty("badge").GetString());
        Assert.Equal("SHAPING RESILIENCE THROUGH SPORTS", en.Content!.Value.GetProperty("heroBanner").GetProperty("title").GetString());
    }

    [Fact]
    public async Task QuickMetrics_CanBeAddedAndRemoved()
    {
        using var db = await SeededDbAsync();
        var service = Service(db);
        var hero = JsonNode.Parse((await service.GetAdminAsync(null, 1, default)).Content!.Value.GetRawText())!["heroBanner"]!;
        var metrics = hero["metrics"]!.AsArray();
        metrics.RemoveAt(0);
        metrics.Add(new JsonObject { ["id"] = "m9", ["value"] = "12", ["label"] = new JsonObject { ["vi"] = "Giải đấu", ["en"] = "Tournaments" } });
        metrics.Add(new JsonObject { ["id"] = "m10", ["value"] = "3", ["label"] = new JsonObject { ["vi"] = "Hồ bơi", ["en"] = "" } });
        await service.SaveSectionAsync("heroBanner", JsonSerializer.SerializeToElement(hero), 1, null, default);

        var published = (await service.GetPublicAsync("vi", null, default)).Content!.Value.GetProperty("heroBanner").GetProperty("metrics");
        Assert.Equal(5, published.GetArrayLength());
        Assert.Equal("Giải đấu", published[3].GetProperty("label").GetString());
    }

    [Theory]
    [InlineData("title", """{"vi":"  ","en":"Title"}""", "bắt buộc có bản tiếng Việt")]
    [InlineData("title", """{"vi":"","en":""}""", "heroBanner.title")]
    [InlineData("posterUrl", "\"javascript:alert(1)\"", "heroBanner.posterUrl")]
    [InlineData("primaryCtaLink", "\"#/<script>\"", "liên kết nội bộ")]
    public async Task InvalidHero_IsRejected_AndNothingIsSaved(string field, string value, string expected)
    {
        using var db = await SeededDbAsync();
        var service = Service(db);
        var before = (await db.SystemSettings.AsNoTracking().SingleAsync()).SettingValue;
        var hero = JsonNode.Parse((await service.GetAdminAsync(null, 1, default)).Content!.Value.GetRawText())!["heroBanner"]!;
        hero[field] = JsonNode.Parse(value);

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            service.SaveSectionAsync("heroBanner", JsonSerializer.SerializeToElement(hero), 1, null, default));

        Assert.Equal(400, error.StatusCode);
        Assert.Contains(expected, error.Message);
        Assert.Equal(before, (await db.SystemSettings.AsNoTracking().SingleAsync()).SettingValue);
    }

    [Fact]
    public async Task LegacyFlatContent_IsUpgraded_KeepingKnownEnglish()
    {
        using var db = TestDb.Create();
        db.SystemSettings.Add(new SystemSetting
        {
            SettingKey = "site.landing-content",
            SettingValue = """
            {"heroBanner":{"badge":"Huy hiệu riêng","title":"SHAPING RESILIENCE THROUGH SPORTS",
              "description":"Phát triển thể lực, tôi luyện ý chí và bứt phá mọi giới hạn cùng hệ thống 9 cụm sân, phòng tập phục vụ 9 bộ môn thể thao.",
              "primaryCtaText":"ĐĂNG KÝ HỘI VIÊN MỚI"},
             "authShowcase":{"stat1Value":"10","stat3Value":"2.5K+"}}
            """
        });
        await db.SaveChangesAsync();

        await LandingContentService.EnsureSeededAsync(db);

        var doc = JsonNode.Parse((await db.SystemSettings.SingleAsync()).SettingValue!)!;
        Assert.Equal(2, doc["schemaVersion"]!.GetValue<int>());
        Assert.Equal("Huy hiệu riêng", HomeContentDocument.Text(doc["heroBanner"]!["badge"], "vi"));
        Assert.Equal("", HomeContentDocument.Text(doc["heroBanner"]!["badge"], "en"));
        Assert.Equal("REGISTER NEW MEMBER", HomeContentDocument.Text(doc["heroBanner"]!["primaryCtaText"], "en"));
        Assert.Contains("{{facilityCount}} cụm sân", HomeContentDocument.Text(doc["heroBanner"]!["description"], "vi"));
        Assert.Equal("{{sportCount}}", doc["authShowcase"]!["stat1Value"]!.GetValue<string>());
        Assert.Equal("2.5K+", doc["authShowcase"]!["stat3Value"]!.GetValue<string>());
    }

    [Fact]
    public async Task LegacyFlatSave_KeepsExistingEnglish()
    {
        using var db = await SeededDbAsync();
        var service = Service(db);
        var flat = (await service.GetAsync(default)).Content!.Value; // old API: Vietnamese only

        await service.SaveAsync(flat, 1, default);

        var en = await service.GetPublicAsync("en", null, default);
        Assert.Equal("REGISTER NEW MEMBER", en.Content!.Value.GetProperty("heroBanner").GetProperty("primaryCtaText").GetString());
    }
}
