using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Reports;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class RevenueReportTests : IClassFixture<BranchApiFactory>
{
    private static readonly DateOnly BusinessDate = new(2026, 10, 6);
    private const string ReportPath = "/api/reports/revenue/details?centerId=1&from=2026-10-06&to=2026-10-06";
    private readonly BranchApiFactory factory;

    public RevenueReportTests(BranchApiFactory factory)
    {
        this.factory = factory;
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>();
        if (!db.Payments.Any()) SeedFinance(db);
    }

    [Fact]
    public async Task SwaggerGeneratesBothSummaryAndDetailedReportSchemas()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"RevenueReportResponse\"", content);
        Assert.Contains("\"DetailedRevenueReportResponse\"", content);
        Assert.Contains("/api/reports/revenue/details", content);
    }

    [Fact]
    public async Task StaffHeaderCannotReplaceJwt()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Staff-Id", "30");
        using var response = await client.GetAsync(ReportPath);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("member@test.local", "1")]
    [InlineData("reception@test.local", "1")]
    [InlineData("manager@test.local", "2")]
    public async Task WrongRoleOrCenterIsForbidden(string email, string centerId)
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, email);
        using var response = await client.GetAsync(ReportPath.Replace("centerId=1", "centerId=" + centerId));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DetailsUseExistingFinancialTotalsAndRefundProcessingDate()
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, "manager@test.local");
        var report = await client.GetFromJsonAsync<DetailedRevenueReportResponse>(ReportPath + "&includeTransactions=true");
        Assert.NotNull(report);
        Assert.Equal(120m, report.GrossRevenue);
        Assert.Equal(30m, report.RefundAmount);
        Assert.Equal(90m, report.NetRevenue);
        Assert.Equal(2, report.SuccessfulPaymentCount);
        Assert.Equal(1, report.RefundedPaymentCount);
        Assert.Equal(1, report.VoidedPaymentCount);
        Assert.Equal(2, report.PaidInvoiceCount);
        Assert.Equal(60m, report.AverageTicket);
        Assert.Equal(2, report.PaymentMethods.Count);
        Assert.Equal(3, Assert.Single(report.Packages).SoldCount);
        Assert.Equal(120m, report.Packages[0].Revenue);
        Assert.Equal(90m, Assert.Single(report.Periods).NetRevenue);
        Assert.Equal(3, report.Transactions.Count);
        Assert.Contains(report.Transactions, row => row.PaymentId == 2 && row.RefundId == 2 && row.PaymentStatus == "Refunded");
        Assert.All(report.Transactions, row => Assert.Equal(DateTimeKind.Utc, row.PaidAtUtc.Kind));

        var existing = await client.GetFromJsonAsync<SportsCenterManagement.BLL.DTOs.CoreFlows.RevenueReportResponse>(
            ReportPath.Replace("/revenue/details", "/revenue"));
        Assert.NotNull(existing);
        Assert.Equal(existing.Gross, report.GrossRevenue);
        Assert.Equal(existing.Refunds, report.RefundAmount);
        Assert.Equal(existing.Net, report.NetRevenue);
    }

    [Fact]
    public async Task MonthlyReportIncludesPartialRefundLedgerWithoutDoubleCountingGross()
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, "manager@test.local");
        var report = await client.GetFromJsonAsync<DetailedRevenueReportResponse>(
            "/api/reports/revenue/details?centerId=1&from=2026-10-01&to=2026-10-31&groupBy=month");
        Assert.NotNull(report);
        Assert.Equal(170m, report.GrossRevenue);
        Assert.Equal(40m, report.RefundAmount);
        Assert.Equal(130m, report.NetRevenue);
        Assert.Single(report.Periods);
        Assert.Empty(report.Transactions);
    }

    [Theory]
    [InlineData("from=2026-10-07&to=2026-10-06")]
    [InlineData("from=2026-10-06&to=2026-10-06&groupBy=year")]
    [InlineData("from=2026-10-06&to=9999-12-31")]
    public async Task InvalidRangesAreRejected(string query)
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, "manager@test.local");
        using var response = await client.GetAsync("/api/reports/revenue/details?centerId=1&" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanChooseAnotherCenter()
    {
        using var client = factory.CreateClient();
        await BranchApiFactory.LoginAsync(client, "admin@test.local");
        var report = await client.GetFromJsonAsync<DetailedRevenueReportResponse>(ReportPath.Replace("centerId=1", "centerId=2"));
        Assert.NotNull(report);
        Assert.Equal(500m, report.GrossRevenue);
        Assert.Equal(100m, report.RefundAmount);
    }

    private static void SeedFinance(SportsCenterDbContext db)
    {
        var paidAt = VietnamTime.ToUtc(BusinessDate).AddHours(1);
        for (var id = 1; id <= 6; id++)
        {
            var amount = id switch { 1 => 100m, 2 => 50m, 3 => 200m, 4 => 999m, 5 => 500m, _ => 20m };
            var time = id == 2 ? paidAt.AddDays(-1) : id == 6 ? VietnamTime.ToUtc(BusinessDate) : paidAt;
            db.Invoices.Add(new Invoice
            {
                Id = id, InvoiceNumber = "REPORT-" + id, MemberId = 1, CenterId = id == 5 ? 2 : 1,
                TotalAmount = amount, Subtotal = amount, Status = id == 3 ? "Voided" : id == 4 ? "Issued" : "Paid",
                IssuedAt = time.AddMinutes(-5), PaidAt = id == 4 ? null : time
            });
            db.InvoiceItems.Add(new InvoiceItem
            {
                Id = id, InvoiceId = id, PackageId = id == 5 ? 2 : 1, Description = "Package",
                Quantity = id == 1 ? 2 : 1, UnitPrice = id == 1 ? amount / 2 : amount, Amount = amount
            });
            db.Payments.Add(new Payment
            {
                Id = id, InvoiceId = id, MemberId = 1, Amount = amount, PaymentMethod = id == 6 ? "MOMO" : "Cash",
                PaymentStatus = id == 3 ? "Voided" : id == 4 ? "Failed" : "Succeeded", PaidAt = time,
                CreatedAt = time, TransactionCode = "REPORT-TX-" + id
            });
        }
        PaymentRefund Refund(long id, long paymentId, decimal amount, DateTime time, string status) => new()
        {
            Id = id, PaymentId = paymentId, Amount = amount, ProcessedAt = time, CreatedAt = time,
            RequestedBy = 30, Reason = "Test refund", IdempotencyKey = "REFUND-" + id, Status = status
        };
        db.PaymentRefunds.AddRange(Refund(1, 1, 10m, paidAt.AddDays(1), "Succeeded"),
            Refund(2, 2, 30m, paidAt, "Succeeded"), Refund(3, 1, 40m, paidAt, "Pending"),
            Refund(4, 1, 40m, paidAt, "Failed"), Refund(5, 5, 100m, paidAt, "Succeeded"));
        db.SaveChanges();
    }
}
