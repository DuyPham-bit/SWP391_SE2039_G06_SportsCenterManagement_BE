using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Checkins;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Implementations;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class CheckinTests(BranchApiFactory factory) : IClassFixture<BranchApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/centers/1/checkins")]
    [InlineData("GET", "/api/centers/1/checkins/eligibility?memberId=1")]
    [InlineData("POST", "/api/centers/1/checkins")]
    public async Task StaffHeaderWithoutJwtCannotAuthenticate(string method, string path)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-Staff-Id", "20");
        if (method == "POST") request.Content = JsonContent.Create(new { memberId = 1 });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("member@test.local")]
    [InlineData("other-reception@test.local")]
    public async Task WrongRoleOrCenterIsForbidden(string email)
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, email);
        using var response = await client.PostAsJsonAsync("/api/centers/1/checkins", new { memberId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RealLoginCanCheckInIdempotentlyAndRecordsJwtActor()
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, "reception@test.local");
        client.DefaultRequestHeaders.Add("X-Staff-Id", "40");
        using var firstResponse = await client.PostAsJsonAsync("/api/centers/1/checkins", new { memberCode = "MB001" });
        using var repeatResponse = await client.PostAsJsonAsync("/api/centers/1/checkins", new { memberId = 1 });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeatResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<CounterCheckinResponse>();
        var repeat = await repeatResponse.Content.ReadFromJsonAsync<CounterCheckinResponse>();
        Assert.NotNull(first);
        Assert.NotNull(repeat);
        Assert.False(first.IsDuplicate);
        Assert.True(repeat.IsDuplicate);
        Assert.Equal(first.CheckinId, repeat.CheckinId);
        Assert.Equal(20, first.CheckedInBy);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
        Assert.Equal(1, await db.CenterCheckins.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(row => row.Action == "CounterCheckIn"));
        using var listResponse = await client.GetAsync("/api/centers/1/checkins");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Single((await listResponse.Content.ReadFromJsonAsync<List<CheckinListItemResponse>>())!);
    }

    [Fact]
    public async Task ExpiredMembershipCannotCheckIn()
    {
        await using var db = BranchTestData.CreateDatabase();
        (await db.MemberSubscriptions.SingleAsync()).EndDate = VietnamTime.GetDate(DateTime.UtcNow).AddDays(-1);
        await db.SaveChangesAsync();
        await using var unitOfWork = new UnitOfWork(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CheckinService(unitOfWork).CheckInAsync(
            20, 1, new CounterCheckinRequest { MemberId = 1 }, null, null));
        Assert.Empty(db.CenterCheckins);
    }

    [Fact]
    public async Task LockedStaffCannotCheckIn()
    {
        await using var db = BranchTestData.CreateDatabase();
        (await db.Users.SingleAsync(row => row.Id == 20)).LockedUntil = DateTime.UtcNow.AddMinutes(15);
        await db.SaveChangesAsync();
        await using var unitOfWork = new UnitOfWork(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new CheckinService(unitOfWork).CheckInAsync(
            20, 1, new CounterCheckinRequest { MemberId = 1 }, null, null));
    }

    [Theory]
    [InlineData("2026-10-05T16:59:59Z", "2026-10-05")]
    [InlineData("2026-10-05T17:00:00Z", "2026-10-06")]
    public void BusinessDateUsesVietnamMidnight(string utc, string day)
    {
        Assert.Equal(DateOnly.Parse(day), VietnamTime.GetDate(DateTime.Parse(utc,
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)));
    }
}
