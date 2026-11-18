using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.Tests.BLL.Catalog;

public class LandingContentServiceTests
{
    [Fact]
    public async Task SaveDraftAsync_StaleVersion_IsRejectedInsteadOfOverwriting()
    {
        using var db = TestDb.Create();
        var service = new LandingContentService(new UnitOfWork(db), new ConfigurationBuilder().Build());
        var first = await service.SaveDraftAsync(Json("""{"heroBanner":{"title":"A"}}"""), 1, default);
        // The service treats versions less than 1 ms apart as equal (Postgres precision), so the second
        // save must land at least 1 ms later or the stale write is indistinguishable from a fresh one.
        await Task.Delay(5);
        await service.SaveDraftAsync(Json("""{"heroBanner":{"title":"B"}}"""), 2, default, first.UpdatedAt);

        var error = await Assert.ThrowsAsync<FlowException>(() =>
            service.SaveDraftAsync(Json("""{"heroBanner":{"title":"C"}}"""), 1, default, first.UpdatedAt));

        Assert.Equal(409, error.StatusCode);
        Assert.Contains("\"B\"", (await db.SystemSettings.SingleAsync()).SettingValue);
    }

    [Fact]
    public async Task DiscardDraftAsync_WritesAuditLog()
    {
        using var db = TestDb.Create();
        var service = new LandingContentService(new UnitOfWork(db), new ConfigurationBuilder().Build());
        await service.SaveDraftAsync(Json("""{"heroBanner":{}}"""), 1, default);

        await service.DiscardDraftAsync(5, default);

        Assert.Equal(5, (await db.AuditLogs.SingleAsync()).UserId);
    }

    [Fact]
    public async Task SaveAsync_SyncsCenterPhoneFromFooterHotline()
    {
        using var db = TestDb.Create();
        db.Centers.Add(new Center
        {
            Id = 1,
            Name = "Olympic Center",
            Address = "123 Main",
            Phone = "0859859367",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = new LandingContentService(new UnitOfWork(db), new ConfigurationBuilder().Build());
        await service.SyncCenterPhoneAsync(Json("""
        {
          "ctaAndFooter": { "contactPhone": "0353716249" }
        }
        """), 1, default);
        await db.SaveChangesAsync();

        Assert.Equal("0353716249", (await db.Centers.SingleAsync()).Phone);
    }

    [Fact]
    public void Validate_ZonesLayout_AcceptsCustomModel3dUrlAndRejectsUnsafeScheme()
    {
        var valid = (string url) => Json($$"""
        {
          "heroBanner": { "badge": "B", "title": "T", "description": "D", "primaryCtaText": "C" },
          "whyUs": { "title": "W" },
          "coursesSection": { "title": "S" },
          "facilitiesSection": { "title": "F" },
          "zonesLayout": { "title": "Z", "model3dUrl": "{{url}}" },
          "ctaAndFooter": {}
        }
        """);

        LandingContentService.Validate(valid("/3d-campus.html"));
        LandingContentService.Validate(valid("https://r2.example.com/campus-3d/2026/11/model.html"));

        var error = Assert.Throws<FlowException>(() => LandingContentService.Validate(valid("javascript:alert(1)")));
        Assert.Equal(400, error.StatusCode);
    }

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();
}
