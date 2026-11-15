using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.Tests;

public class ReportServiceTests
{
    private static readonly DateOnly From = TestDb.Today.AddDays(-7);
    private static readonly DateOnly To = TestDb.Today.AddDays(1);

    [Fact]
    public async Task GetMembershipReportAsync_AggregatesMembersAndSubscriptionsPerCenter()
    {
        using var db = TestDb.Create();
        AddCenters(db);
        var memberA = Member(db, 1, "M-A", TestDb.Now);
        var memberB = Member(db, 1, "M-B", TestDb.Now);
        var memberC = Member(db, 1, "M-C", TestDb.Now.AddDays(-60));
        var otherCenterMember = Member(db, 2, "M-X", TestDb.Now);
        var basic = TestDb.Package(db, "Basic", 1);
        var premium = TestDb.Package(db, "Premium", 2);
        var otherPackage = new MembershipPackage
        {
            CenterId = 2, Name = "Other", DurationDays = 30, Price = 100, Status = "Active", CreatedAt = TestDb.Now
        };
        db.Add(otherPackage);
        db.SaveChanges();

        var today = TestDb.Today;
        Sub(db, memberA, basic, "Active", today.AddDays(-5), today.AddDays(20));
        Sub(db, memberA, premium, "Expired", today.AddDays(-90), today.AddDays(-60)); // active elsewhere: not expired
        Sub(db, memberB, basic, "Expired", today.AddDays(-90), today.AddDays(-60));
        Sub(db, memberC, premium, "Active", today.AddDays(-40), today.AddDays(-10)); // lapsed active counts as expired
        Sub(db, otherCenterMember, otherPackage, "Active", today.AddDays(-5), today.AddDays(20));

        var report = await Service(db).GetMembershipReportAsync(1, From, To, "day");

        Assert.Equal(3, report.TotalMembers);
        Assert.Equal(1, report.ActiveMembers);
        Assert.Equal(2, report.ExpiredMembers);
        Assert.Equal(2, report.InactiveMembers);
        Assert.Equal(2, report.NewMembers);
        Assert.Equal(2, report.Periods.Sum(p => p.NewMembers));
        Assert.Equal(1, report.Packages!.Single(p => p.Id == basic.Id).ActiveSubscribers);
        Assert.Equal(0, report.Packages!.Single(p => p.Id == premium.Id).ActiveSubscribers);
    }

    [Fact]
    public async Task GetSportRevenueReportAsync_SplitsPaymentsBySportAndUnallocated()
    {
        using var db = TestDb.Create();
        AddCenters(db);
        var swim = new Sport { CenterId = 1, Name = "Swim", Status = "Active" };
        var yoga = new Sport { CenterId = 1, Name = "Yoga", Status = "Active" };
        db.AddRange(swim, yoga);
        db.SaveChanges();
        var swimClass = Class(db, swim);
        var yogaClass = Class(db, yoga);
        var package = TestDb.Package(db, "Combo", 1);
        var subscription = TestDb.Subscription(db, package, "Active", TestDb.Today, TestDb.Today.AddDays(30));
        Enroll(db, swimClass, subscription.Id);
        Enroll(db, yogaClass, subscription.Id);

        Paid(db, "INV-1", 300, item => item.SubscriptionId = subscription.Id);
        Paid(db, "INV-2", 300, item => item.SubscriptionId = subscription.Id);
        Paid(db, "INV-3", 50, _ => { });

        var report = await Service(db).GetSportRevenueReportAsync(1, From, To);

        Assert.Equal(650, report.Net);
        Assert.Equal(300, report.Sports.Single(i => i.SportName == "Swim").Net);
        Assert.Equal(300, report.Sports.Single(i => i.SportName == "Yoga").Net);
        Assert.Equal(50, report.UnallocatedNet);
    }

    [Fact]
    public async Task GetSportRevenueReportAsync_LateCancelledBooking_StillAllocatesSport()
    {
        using var db = TestDb.Create();
        AddCenters(db);
        var boxing = new Sport { CenterId = 1, Name = "Boxing", Status = "Active" };
        db.Add(boxing);
        db.SaveChanges();
        var boxingClass = Class(db, boxing);
        var session = new ClassSession
        {
            ClassId = boxingClass.Id, SessionDate = TestDb.Today, StartTime = new TimeOnly(18, 0),
            EndTime = new TimeOnly(19, 0), SessionStatus = "Scheduled", CreatedAt = TestDb.Now
        };
        db.Add(session);
        var subscription = TestDb.Subscription(db, TestDb.Package(db, "Drop-in", 1), "Active", TestDb.Today, TestDb.Today.AddDays(30));
        db.Add(new SessionBooking
        {
            SessionId = session.Id, MemberId = 7, SubscriptionId = subscription.Id, BookedAt = TestDb.Now,
            Status = FlowStatuses.BookingCancelledLate
        });
        db.SaveChanges();
        Paid(db, "INV-LATE", 120, item => item.SubscriptionId = subscription.Id);

        var report = await Service(db).GetSportRevenueReportAsync(1, From, To);

        Assert.Equal(120, report.Sports.Single(i => i.SportName == "Boxing").Net);
        Assert.Equal(0, report.UnallocatedNet);
    }

    private static ReportService Service(SportsCenterDbContext db) =>
        new(new UnitOfWork(db), null!, new ConfigurationBuilder().Build());

    private static void AddCenters(SportsCenterDbContext db)
    {
        db.AddRange(
            new Center { Id = 1, Name = "C1", Address = "A", Status = "Active", CreatedAt = TestDb.Now },
            new Center { Id = 2, Name = "C2", Address = "B", Status = "Active", CreatedAt = TestDb.Now });
        db.SaveChanges();
    }

    private static MemberProfile Member(SportsCenterDbContext db, long centerId, string code, DateTime createdAt)
    {
        var member = new MemberProfile
        {
            UserId = db.MemberProfiles.Count() + 100, CenterId = centerId, MemberCode = code, FullName = code,
            CreatedAt = createdAt
        };
        db.Add(member);
        db.SaveChanges();
        return member;
    }

    private static void Sub(SportsCenterDbContext db, MemberProfile member, MembershipPackage package, string status,
        DateOnly start, DateOnly end)
    {
        db.Add(new MemberSubscription
        {
            MemberId = member.Id, PackageId = package.Id, StartDate = start, EndDate = end,
            DurationDays = 30, Price = package.Price, Status = status, CreatedAt = TestDb.Now
        });
        db.SaveChanges();
    }

    private static ClassEntity Class(SportsCenterDbContext db, Sport sport)
    {
        var entity = new ClassEntity
        {
            CenterId = 1, SportId = sport.Id, Name = sport.Name + " class", Capacity = 10, DurationMinutes = 60,
            Status = "Active", CreatedAt = TestDb.Now
        };
        db.Add(entity);
        db.SaveChanges();
        return entity;
    }

    private static void Enroll(SportsCenterDbContext db, ClassEntity cls, long subscriptionId)
    {
        db.Add(new ClassEnrollment
        {
            ClassId = cls.Id, MemberId = 7, SubscriptionId = subscriptionId, RegisteredAt = TestDb.Now, Status = "Active"
        });
        db.SaveChanges();
    }

    private static void Paid(SportsCenterDbContext db, string number, decimal amount, Action<InvoiceItem> link)
    {
        var invoice = new Invoice
        {
            InvoiceNumber = number, MemberId = 7, CenterId = 1, Subtotal = amount, TotalAmount = amount,
            Status = "Paid", IssuedAt = TestDb.Now, PaidAt = TestDb.Now
        };
        db.Add(invoice);
        db.SaveChanges();
        var item = new InvoiceItem
        {
            InvoiceId = invoice.Id, Description = number, Quantity = 1, UnitPrice = amount, Amount = amount
        };
        link(item);
        db.Add(item);
        db.Add(new Payment
        {
            InvoiceId = invoice.Id, MemberId = 7, PaymentMethod = "CASH", Amount = amount, PaymentStatus = "Succeeded",
            CreatedAt = TestDb.Now, AttemptedAt = TestDb.Now, PaidAt = TestDb.Now
        });
        db.SaveChanges();
    }
}
