using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.Tests;

public class AdminFlowTests
{
    private static readonly DateOnly Dec1 = new(2026, 12, 1);

    [Fact]
    public void Supersede_FuturePolicy_KeepsCurrentPolicyActiveUntilDayBefore()
    {
        var (to, status) = PayrollService.Supersede(new DateOnly(2026, 1, 1), null, Dec1);

        Assert.Equal("Active", status);
        Assert.Equal(new DateOnly(2026, 11, 30), to);
    }

    [Fact]
    public void Supersede_PolicyStartingOnOrAfterNewDate_IsArchived()
    {
        Assert.Equal("Archived", PayrollService.Supersede(Dec1, null, Dec1).Status);
        Assert.Equal("Archived", PayrollService.Supersede(Dec1.AddMonths(1), null, Dec1).Status);
    }

    [Fact]
    public void Supersede_EarlierEndDate_IsNotExtended()
    {
        var end = new DateOnly(2026, 6, 30);

        Assert.Equal(end, PayrollService.Supersede(new DateOnly(2026, 1, 1), end, Dec1).EffectiveTo);
    }

    [Fact]
    public async Task SaveDraftAsync_StaleVersion_IsRejectedInsteadOfOverwriting()
    {
        using var db = TestDb.Create();
        var service = new LandingContentService(db, new ConfigurationBuilder().Build());
        var first = await service.SaveDraftAsync(Json("""{"heroBanner":{"title":"A"}}"""), 1, default);
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
        var service = new LandingContentService(db, new ConfigurationBuilder().Build());
        await service.SaveDraftAsync(Json("""{"heroBanner":{}}"""), 1, default);

        await service.DiscardDraftAsync(5, default);

        Assert.Equal(5, (await db.AuditLogs.SingleAsync()).UserId);
    }

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();
}
