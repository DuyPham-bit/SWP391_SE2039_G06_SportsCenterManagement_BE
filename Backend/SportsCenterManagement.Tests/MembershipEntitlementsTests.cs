using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.Tests;

public class MembershipEntitlementsTests
{
    [Fact]
    public async Task ActivateAsync_RenewSamePackage_StartsTheDayAfterCurrentEnd()
    {
        using var db = TestDb.Create();
        var basic = TestDb.Package(db, "Basic", 1);
        TestDb.Subscription(db, basic, "Active", TestDb.Today.AddDays(-10), TestDb.Today.AddDays(19));
        var renewal = TestDb.Subscription(db, basic, "PendingPayment");

        await MembershipEntitlements.ActivateAsync(db, renewal, basic, TestDb.Today, TestDb.Now, default);

        Assert.Equal(TestDb.Today.AddDays(20), renewal.StartDate);
        Assert.Equal(TestDb.Today.AddDays(49), renewal.EndDate);
        Assert.Equal("Active", renewal.Status);
    }

    [Fact]
    public async Task ActivateAsync_HigherTier_MarksOldPackageUpgradedAndLinksIt()
    {
        using var db = TestDb.Create();
        var basic = TestDb.Package(db, "Basic", 1);
        var premium = TestDb.Package(db, "Premium", 2);
        var old = TestDb.Subscription(db, basic, "Active", TestDb.Today.AddDays(-5), TestDb.Today.AddDays(20));
        var upgrade = TestDb.Subscription(db, premium, "PendingPayment");

        await MembershipEntitlements.ActivateAsync(db, upgrade, premium, TestDb.Today, TestDb.Now, default);

        Assert.Equal(TestDb.Today, upgrade.StartDate);
        Assert.Equal(MembershipEntitlements.StatusUpgraded, old.Status);
        Assert.Equal(upgrade.Id, old.UpgradedBySubscriptionId);
    }
}
