using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Implementations;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class PayOsReconciliationTests
{
    [Fact]
    public async Task MissedWebhookCanBeRecoveredFromSignedPayOsStatusWithoutDuplicatePayment()
    {
        await using var fixture = new RecoveryFixture();

        var result = await fixture.Service.SyncVietQrPaymentAsync(1, 34);
        var repeated = await fixture.Service.SyncVietQrPaymentAsync(1, 34);

        Assert.True(result.Success);
        Assert.True(repeated.Success);
        Assert.Equal("Paid", (await fixture.Db.Invoices.SingleAsync()).Status);
        Assert.Equal("Active", (await fixture.Db.MemberSubscriptions.SingleAsync(subscription => subscription.Id == 2)).Status);
        var payment = await fixture.Db.Payments.SingleAsync();
        Assert.Equal(2000m, payment.Amount);
        Assert.Equal("PAYOS:REF-34", payment.TransactionCode);
        Assert.Equal("PAYOS", payment.PaymentMethod);
        Assert.Equal("Succeeded", payment.PaymentStatus);
        Assert.Equal(HttpMethod.Get, fixture.Handler.LastMethod);
        Assert.Equal("https://payos.test/v2/payment-requests/34", fixture.Handler.LastUri);
        Assert.Equal("test-client", fixture.Handler.ClientId);
        Assert.Equal("test-api", fixture.Handler.ApiKey);
    }

    [Theory]
    [InlineData("PENDING", 0, 2000)]
    [InlineData("PROCESSING", 1000, 1000)]
    [InlineData("CANCELLED", 0, 2000)]
    [InlineData("EXPIRED", 0, 2000)]
    [InlineData("PAID", 1000, 1000)]
    [InlineData("PAID", 3000, 0)]
    public async Task UnpaidOrIncorrectAmountsCannotActivateSubscription(string status, int amountPaid, int amountRemaining)
    {
        await using var fixture = new RecoveryFixture();
        fixture.Handler.Status = status;
        fixture.Handler.AmountPaid = amountPaid;
        fixture.Handler.AmountRemaining = amountRemaining;

        Assert.False((await fixture.Service.SyncVietQrPaymentAsync(1, 34)).Success);
        await fixture.AssertUnchangedAsync();
    }

    [Theory]
    [InlineData("wrong-order")]
    [InlineData("wrong-price")]
    [InlineData("missing-reference")]
    [InlineData("missing-transactions")]
    [InlineData("wrong-transaction-amount")]
    public async Task InconsistentSignedProviderDataCannotMarkInvoicePaid(string failure)
    {
        await using var fixture = new RecoveryFixture();
        switch (failure)
        {
            case "wrong-order": fixture.Handler.OrderCode = 35; break;
            case "wrong-price": fixture.Handler.Amount = 3000; break;
            case "missing-reference": fixture.Handler.Reference = ""; break;
            case "missing-transactions": fixture.Handler.IncludeTransaction = false; break;
            case "wrong-transaction-amount": fixture.Handler.TransactionAmount = 1000; break;
        }

        Assert.False((await fixture.Service.SyncVietQrPaymentAsync(1, 34)).Success);
        await fixture.AssertUnchangedAsync();
    }

    [Fact]
    public async Task InvalidProviderSignatureCannotChangeInvoice()
    {
        await using var fixture = new RecoveryFixture();
        fixture.Handler.InvalidSignature = true;

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.SyncVietQrPaymentAsync(1, 34));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        await fixture.AssertUnchangedAsync();
    }

    [Fact]
    public async Task ProviderUnavailableCannotChangeInvoice()
    {
        await using var fixture = new RecoveryFixture();
        fixture.Handler.HttpStatus = HttpStatusCode.ServiceUnavailable;

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.SyncVietQrPaymentAsync(1, 34));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        await fixture.AssertUnchangedAsync();
    }

    [Fact]
    public async Task AnotherMemberCannotSyncInvoiceOrCallPayOs()
    {
        await using var fixture = new RecoveryFixture();

        var error = await Assert.ThrowsAsync<BusinessException>(() => fixture.Service.SyncVietQrPaymentAsync(2, 34));
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal(0, fixture.Handler.CallCount);
        await fixture.AssertUnchangedAsync();
    }

    [Fact]
    public async Task VoidedInvoiceRecordsLatePaymentWithoutReactivatingSubscription()
    {
        await using var fixture = new RecoveryFixture();
        (await fixture.Db.Invoices.SingleAsync()).Status = "Voided";
        (await fixture.Db.Payments.SingleAsync()).PaymentStatus = "Voided";
        (await fixture.Db.MemberSubscriptions.SingleAsync(subscription => subscription.Id == 2)).Status = "Cancelled";
        await fixture.Db.SaveChangesAsync();

        Assert.True((await fixture.Service.SyncVietQrPaymentAsync(1, 34)).Success);
        Assert.Equal("PaidAfterVoid", (await fixture.Db.Invoices.SingleAsync()).Status);
        Assert.Equal("Cancelled", (await fixture.Db.MemberSubscriptions.SingleAsync(subscription => subscription.Id == 2)).Status);
        Assert.Equal("Succeeded", (await fixture.Db.Payments.SingleAsync()).PaymentStatus);
    }

    private sealed class RecoveryFixture : IAsyncDisposable
    {
        public SportsCenterDbContext Db { get; }
        public SignedPaymentInfoHandler Handler { get; } = new();
        public PaymentService Service { get; }
        private readonly UnitOfWork unitOfWork;
        private readonly HttpClient httpClient;
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public RecoveryFixture()
        {
            connection.Open();
            Db = new SportsCenterDbContext(new DbContextOptionsBuilder<SportsCenterDbContext>()
                .UseSqlite(connection).ReplaceService<Microsoft.EntityFrameworkCore.Infrastructure.IModelCustomizer, BranchSqliteModelCustomizer>().Options);
            Db.Database.EnsureCreated();
            BranchTestData.Seed(Db);
            Db.MemberSubscriptions.Add(new MemberSubscription
            {
                Id = 2, MemberId = 1, PackageId = 1, Price = 2000m, Status = "PendingPayment", DurationDays = 30,
                StartDate = new DateOnly(2026, 10, 12), EndDate = new DateOnly(2026, 11, 11)
            });
            // Deliberately use different invoice and payment IDs to catch incorrect orderCode mappings.
            Db.Invoices.Add(new Invoice { Id = 340, InvoiceNumber = "TEST-340", MemberId = 1, CenterId = 1, TotalAmount = 2000m, Status = "Issued" });
            Db.InvoiceItems.Add(new InvoiceItem { Id = 1, InvoiceId = 340, SubscriptionId = 2, PackageId = 1, Description = "Test", Quantity = 1, Amount = 2000m, UnitPrice = 2000m });
            Db.Payments.Add(new Payment
            {
                Id = 34, InvoiceId = 340, MemberId = 1, Amount = 2000m, PaymentMethod = "PAYOS",
                GatewayReference = "34", PaymentStatus = "Pending", IdempotencyKey = "recovery-34", CreatedAt = DateTime.UtcNow
            });
            Db.SaveChanges();
            unitOfWork = new UnitOfWork(Db);
            httpClient = new HttpClient(Handler);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PayOS:BaseUrl"] = "https://payos.test", ["PayOS:ClientId"] = "test-client",
                ["PayOS:ApiKey"] = "test-api", ["PayOS:ChecksumKey"] = "test-checksum"
            }).Build();
            Service = new PaymentService(unitOfWork, new PaymentTestVnPay(), new PaymentTestMomo(),
                new PayOsService(configuration, httpClient), configuration);
        }

        public async Task AssertUnchangedAsync()
        {
            Assert.Equal("Issued", (await Db.Invoices.SingleAsync()).Status);
            Assert.Equal("PendingPayment", (await Db.MemberSubscriptions.SingleAsync(subscription => subscription.Id == 2)).Status);
            var payment = await Db.Payments.SingleAsync();
            Assert.Equal("Pending", payment.PaymentStatus);
            Assert.Null(payment.ProviderTransactionId);
        }

        public async ValueTask DisposeAsync()
        {
            httpClient.Dispose();
            await unitOfWork.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class SignedPaymentInfoHandler : HttpMessageHandler
    {
        public long OrderCode { get; set; } = 34;
        public int Amount { get; set; } = 2000;
        public int AmountPaid { get; set; } = 2000;
        public int AmountRemaining { get; set; }
        public string Status { get; set; } = "PAID";
        public string Reference { get; set; } = "REF-34";
        public int TransactionAmount { get; set; } = 2000;
        public bool IncludeTransaction { get; set; } = true;
        public bool InvalidSignature { get; set; }
        public HttpStatusCode HttpStatus { get; set; } = HttpStatusCode.OK;
        public HttpMethod? LastMethod { get; private set; }
        public string? LastUri { get; private set; }
        public string? ClientId { get; private set; }
        public string? ApiKey { get; private set; }
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastUri = request.RequestUri?.AbsoluteUri;
            ClientId = request.Headers.GetValues("x-client-id").Single();
            ApiKey = request.Headers.GetValues("x-api-key").Single();
            var transactions = IncludeTransaction
                ? $"[{{\"reference\":{JsonSerializer.Serialize(Reference)},\"amount\":{TransactionAmount}}}]" : "[]";
            var dataJson = $$"""
                {"transactions":{{transactions}},"status":{{JsonSerializer.Serialize(Status)}},
                 "orderCode":{{OrderCode}},"id":"link-34","createdAt":"2026-10-12T10:44:07.000Z",
                 "cancellationReason":null,"canceledAt":null,"amountRemaining":{{AmountRemaining}},
                 "amountPaid":{{AmountPaid}},"amount":{{Amount}}}
                """;
            var canonicalTransactions = IncludeTransaction
                ? $"[{{\"amount\":{TransactionAmount},\"reference\":{JsonSerializer.Serialize(Reference)}}}]" : "[]";
            var canonical = $"amount={Amount}&amountPaid={AmountPaid}&amountRemaining={AmountRemaining}&canceledAt=&cancellationReason=&createdAt=2026-10-12T10:44:07.000Z&id=link-34&orderCode={OrderCode}&status={Status}&transactions={canonicalTransactions}";
            var signature = InvalidSignature ? "invalid" : Convert.ToHexString(HMACSHA256.HashData(
                Encoding.UTF8.GetBytes("test-checksum"), Encoding.UTF8.GetBytes(canonical)));
            return Task.FromResult(new HttpResponseMessage(HttpStatus)
            {
                Content = new StringContent($$"""{"code":"00","desc":"success","data":{{dataJson}},"signature":"{{signature}}"}""",
                    Encoding.UTF8, "application/json")
            });
        }
    }
}
