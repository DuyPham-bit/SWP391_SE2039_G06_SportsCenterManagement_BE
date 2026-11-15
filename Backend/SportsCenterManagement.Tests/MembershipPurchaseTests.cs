using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.Tests;

public class MembershipPurchaseTests
{
    [Fact]
    public async Task CreatePendingMembershipAsync_RetryWithOtherSport_ReplacesPendingSelection()
    {
        using var db = TestDb.Create();
        var (member, package, yoga, boxing) = Seed(db);
        var service = new CoreFlowService(new UnitOfWork(db), new ConfigurationBuilder().Build());

        var first = await service.CreatePendingMembershipAsync(member.Id, package.Id, null, default, [yoga.Id]);
        var retry = await service.CreatePendingMembershipAsync(member.Id, package.Id, null, default, [boxing.Id]);

        Assert.Equal(first.InvoiceId, retry.InvoiceId);
        var picked = await db.SubscriptionSports.Where(row => row.SubscriptionId == retry.SubscriptionId)
            .Select(row => row.SportId).ToListAsync();
        Assert.Equal([boxing.Id], picked);
    }

    [Fact]
    public async Task EnsureCanBuyPackageAsync_DoesNotPinMemberToCenter()
    {
        using var db = TestDb.Create();
        var (member, package, _, _) = Seed(db);
        var service = new MemberService(new UnitOfWork(db), null!, null!);

        await service.EnsureCanBuyPackageAsync(member.UserId, package.Id);

        db.ChangeTracker.Clear();
        Assert.Null((await db.MemberProfiles.SingleAsync(x => x.Id == member.Id)).CenterId);
    }

    private static (MemberProfile Member, MembershipPackage Package, Sport Yoga, Sport Boxing) Seed(SportsCenterDbContext db)
    {
        db.Add(new Center { Id = 1, Name = "C1", Address = "A", Status = "Active", CreatedAt = TestDb.Now });
        var user = new User
        {
            Username = "member", Email = "m@example.test", PasswordHash = "x", RoleId = 1, Status = "Active",
            CreatedAt = TestDb.Now
        };
        db.Add(user);
        db.SaveChanges();
        var member = new MemberProfile { UserId = user.Id, MemberCode = "M1", FullName = "M1", CreatedAt = TestDb.Now };
        var package = new MembershipPackage
        {
            CenterId = 1, Name = "Single", DurationDays = 30, Price = 100, AllowedSports = 1, TierLevel = 1,
            IsAllAccess = false, Status = "Active", CreatedAt = TestDb.Now
        };
        var yoga = new Sport { CenterId = 1, Name = "Yoga", Status = "Active" };
        var boxing = new Sport { CenterId = 1, Name = "Boxing", Status = "Active" };
        db.AddRange(member, package, yoga, boxing);
        db.SaveChanges();
        return (member, package, yoga, boxing);
    }
}
