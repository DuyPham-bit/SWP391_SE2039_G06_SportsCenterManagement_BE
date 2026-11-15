using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;

namespace SportsCenterManagement.Tests;

public class PaymentServiceRefundTests
{
    [Fact]
    public async Task RefundPaymentAsync_FullCashRefundOfUpgrade_RestoresReplacedPackage()
    {
        using var db = TestDb.Create();
        var (old, upgrade, payment) = await UpgradePaidInCashAsync(db, oldEnd: TestDb.Today.AddDays(20));

        await Service(db).RefundPaymentAsync(1, null, payment.Id, "refund-1",
            new CreateRefundRequest { Amount = payment.Amount, Reason = "Khách đổi ý" }, "127.0.0.1");

        Assert.Equal("Refunded", upgrade.Status);
        Assert.Equal("Active", old.Status);
        Assert.Null(old.UpgradedBySubscriptionId);
    }

    [Fact]
    public async Task RefundPaymentAsync_PartialRefundOfUpgrade_KeepsUpgradeActive()
    {
        using var db = TestDb.Create();
        var (old, upgrade, payment) = await UpgradePaidInCashAsync(db, oldEnd: TestDb.Today.AddDays(20));

        await Service(db).RefundPaymentAsync(1, null, payment.Id, "refund-2",
            new CreateRefundRequest { Amount = 50, Reason = "Giảm giá bù" }, "127.0.0.1");

        Assert.Equal("Active", upgrade.Status);
        Assert.Equal(MembershipEntitlements.StatusUpgraded, old.Status);
    }

    [Fact]
    public async Task RefundPaymentAsync_RefundAboveAmountPaid_IsRejected()
    {
        using var db = TestDb.Create();
        var (_, _, payment) = await UpgradePaidInCashAsync(db, oldEnd: TestDb.Today.AddDays(20));

        await Assert.ThrowsAnyAsync<Exception>(() => Service(db).RefundPaymentAsync(1, null, payment.Id, "refund-3",
            new CreateRefundRequest { Amount = payment.Amount + 1, Reason = "Hoàn quá số tiền" }, "127.0.0.1"));
    }

    private static PaymentService Service(SportsCenterDbContext db) =>
        new(new UnitOfWork(db), null!, null!, new ConfigurationBuilder().Build());

    private static async Task<(MemberSubscription Old, MemberSubscription Upgrade, Payment Payment)> UpgradePaidInCashAsync(
        SportsCenterDbContext db, DateOnly oldEnd)
    {
        var basic = TestDb.Package(db, "Basic", 1);
        var premium = TestDb.Package(db, "Premium", 2);
        var old = TestDb.Subscription(db, basic, "Active", TestDb.Today.AddDays(-5), oldEnd);
        var upgrade = TestDb.Subscription(db, premium, "PendingPayment");
        await MembershipEntitlements.ActivateAsync(db, upgrade, premium, TestDb.Today, TestDb.Now, default);

        var invoice = new Invoice
        {
            InvoiceNumber = "INV-TEST", MemberId = 7, CenterId = 1, Subtotal = 200, TotalAmount = 200,
            Status = "Paid", IssuedAt = TestDb.Now, CreatedBy = 1
        };
        db.Add(invoice);
        db.SaveChanges();
        db.Add(new InvoiceItem
        {
            InvoiceId = invoice.Id, Description = premium.Name, Quantity = 1, UnitPrice = 200, Amount = 200,
            SubscriptionId = upgrade.Id
        });
        var payment = new Payment
        {
            InvoiceId = invoice.Id, MemberId = 7, PaymentMethod = "CASH", Amount = 200, PaymentStatus = "Succeeded",
            CreatedAt = TestDb.Now, AttemptedAt = TestDb.Now, PaidAt = TestDb.Now
        };
        db.Add(payment);
        db.SaveChanges();
        return (old, upgrade, payment);
    }
}
