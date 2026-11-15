using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.Tests;

/// <summary>Fresh in-memory database per test; transactions are no-ops in this provider.</summary>
internal static class TestDb
{
    public static readonly DateTime Now = DateTime.UtcNow;
    public static readonly DateOnly Today = DateOnly.FromDateTime(Now.AddHours(7));

    public static SportsCenterDbContext Create() =>
        new(new DbContextOptionsBuilder<SportsCenterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    public static MembershipPackage Package(SportsCenterDbContext db, string name, int tier)
    {
        var package = new MembershipPackage
        {
            CenterId = 1, Name = name, DurationDays = 30, Price = 100, AllowedSports = 0,
            TierLevel = tier, IsAllAccess = true, Status = "Active", CreatedAt = Now
        };
        db.Add(package);
        db.SaveChanges();
        return package;
    }

    public static MemberSubscription Subscription(SportsCenterDbContext db, MembershipPackage package, string status,
        DateOnly? start = null, DateOnly? end = null)
    {
        var subscription = new MemberSubscription
        {
            MemberId = 7, PackageId = package.Id, StartDate = start, EndDate = end,
            DurationDays = package.DurationDays, Price = package.Price, Status = status, CreatedAt = Now
        };
        db.Add(subscription);
        db.SaveChanges();
        return subscription;
    }
}
