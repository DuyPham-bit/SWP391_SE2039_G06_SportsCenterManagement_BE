using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using Xunit;

namespace SportsCenterManagement.Tests;

public sealed class PaymentJwtIntegrationTests : IDisposable
{
    private readonly BranchApiFactory database = new();
    private readonly WebApplicationFactory<Program> application;
    private readonly HttpClient client;

    public PaymentJwtIntegrationTests()
    {
        application = database.WithWebHostBuilder(builder =>
        {
            foreach (var key in new[] { "VnPay:TmnCode", "VnPay:HashSecret", "Momo:PartnerCode", "Momo:AccessKey",
                         "Momo:SecretKey", "PayOS:ClientId", "PayOS:ApiKey", "PayOS:ChecksumKey" })
                builder.UseSetting(key, "test-only");
            foreach (var key in new[] { "VnPay:BaseUrl", "VnPay:ReturnUrl", "Momo:PaymentUrl", "Momo:ReturnUrl",
                         "Momo:NotifyUrl", "PayOS:BaseUrl", "PayOS:ReturnUrl", "PayOS:CancelUrl" })
                builder.UseSetting(key, "https://gateway.test/");
            builder.ConfigureServices(services =>
            {
                var vnPay = new Mock<IVnPayService>(MockBehavior.Strict);
                vnPay.Setup(gateway => gateway.CreatePaymentUrl(It.IsAny<string>(), It.IsAny<decimal>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>()))
                    .Returns("https://gateway.test/vnpay");
                vnPay.Setup(gateway => gateway.ProcessCallback(It.IsAny<IDictionary<string, string>>()))
                    .Returns(new VnPayCallbackResult { IsValidSignature = false });
                services.RemoveAll<IVnPayService>();
                services.AddSingleton(vnPay.Object);

                var momo = new Mock<IMoMoService>(MockBehavior.Strict);
                momo.Setup(gateway => gateway.CreatePaymentUrlAsync(It.IsAny<string>(), It.IsAny<decimal>(),
                        It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string reference, decimal amount, string _, CancellationToken _) => new MomoCreatePaymentResponse
                    {
                        ResultCode = 0, OrderId = reference, RequestId = reference, Amount = (long)amount,
                        PartnerCode = "test-only", PayUrl = "https://test-payment.momo.vn/payment"
                    });
                momo.Setup(gateway => gateway.ProcessCallback(It.IsAny<IDictionary<string, string>>()))
                    .Returns(new MomoCallbackResult { IsValidSignature = false });
                services.RemoveAll<IMoMoService>();
                services.AddSingleton(momo.Object);

                var payOs = new Mock<IPayOsService>(MockBehavior.Strict);
                payOs.Setup(gateway => gateway.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((long orderCode, decimal amount, string _, string _, CancellationToken _) => new PayOsCreatePaymentResponse
                    {
                        Code = "00", Data = new PayOsPaymentData
                        { OrderCode = orderCode, Amount = (int)amount, CheckoutUrl = "https://gateway.test/payos" }
                    });
                payOs.Setup(gateway => gateway.VerifyWebhookSignature(It.IsAny<PayOsWebhookRequest>())).Returns(false);
                services.RemoveAll<IPayOsService>();
                services.AddSingleton(payOs.Object);
            });
        });
        client = application.CreateClient();
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    [Theory]
    [InlineData("/api/payments/create-vnpay-url")]
    [InlineData("/api/payments/create-momo-url")]
    [InlineData("/api/payments/create-payos-url")]
    [InlineData("/api/payments/counter-checkout")]
    [InlineData("/api/payments/counter-void/unknown")]
    [InlineData("/api/payments/unknown/reconcile")]
    [InlineData("/api/payments/1/refunds")]
    [InlineData("/api/payments/refunds/1/reconcile")]
    [InlineData("/api/invoices/1/payments")]
    public async Task FakeIdHeadersCannotReplaceJwt(string path)
    {
        client.DefaultRequestHeaders.Add("X-Member-Id", "1");
        client.DefaultRequestHeaders.Add("X-Staff-Id", "20");
        using var response = await client.PostAsJsonAsync(path, new { memberId = 1, packageId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/payments/create-vnpay-url")]
    [InlineData("/api/payments/create-momo-url")]
    [InlineData("/api/payments/create-payos-url")]
    public async Task LoginJwtMapsUserToMemberAndPreservesIdempotency(string path)
    {
        var auth = await BranchApiFactory.LoginAsync(client, "member@test.local");
        Assert.Equal(10, auth.UserId);
        client.DefaultRequestHeaders.Add("X-Member-Id", "999");
        using var first = await client.PostAsJsonAsync(path, new { packageId = 1, memberId = 999 });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await client.PostAsJsonAsync(path, new { packageId = 1 });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await retry.Content.ReadAsStringAsync());
        await WithDatabase(async db =>
        {
            Assert.Equal(1, await db.Invoices.CountAsync());
            Assert.Equal(1, (await db.Invoices.SingleAsync()).MemberId);
            Assert.Equal(1, (await db.Payments.SingleAsync()).MemberId);
        });
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("signature")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("unsigned")]
    [InlineData("algorithm")]
    [InlineData("missing-id")]
    [InlineData("zero-id")]
    [InlineData("negative-id")]
    [InlineData("missing-expiration")]
    [InlineData("opaque")]
    public async Task InvalidJwtCannotReachPayment(string failure)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(10, failure));
        using var response = await client.PostAsJsonAsync("/api/payments/create-vnpay-url", new { packageId = 1 });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await WithDatabase(async db => Assert.Empty(await db.Payments.ToListAsync()));
    }

    [Theory]
    [InlineData("member@test.local", HttpStatusCode.Forbidden)]
    [InlineData("other-reception@test.local", HttpStatusCode.NotFound)]
    public async Task CheckoutEnforcesDatabaseRoleAndCenter(string email, HttpStatusCode expected)
    {
        await BranchApiFactory.LoginAsync(client, email);
        using var response = await CheckoutAsync();
        Assert.Equal(expected, response.StatusCode);
        await WithDatabase(async db => Assert.Empty(await db.Payments.ToListAsync()));
    }

    [Fact]
    public async Task CounterCheckoutUsesJwtActorAndDoesNotDuplicateOnRetry()
    {
        await BranchApiFactory.LoginAsync(client, "reception@test.local");
        client.DefaultRequestHeaders.Add("X-Staff-Id", "40");
        using var first = await CheckoutAsync();
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var retry = await CheckoutAsync();
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await WithDatabase(async db =>
        {
            Assert.Equal(20, (await db.Invoices.SingleAsync()).CreatedBy);
            Assert.Equal(20, (await db.Payments.SingleAsync()).ProcessedBy);
            Assert.Equal("Paid", (await db.Invoices.SingleAsync()).Status);
            Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.Action == "Payment.CounterRecorded"));
        });
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("locked")]
    public async Task RevokedAccountCannotReuseExistingLoginJwt(string state)
    {
        await BranchApiFactory.LoginAsync(client, "reception@test.local");
        await WithDatabase(async db =>
        {
            var user = await db.Users.SingleAsync(user => user.Id == 20);
            if (state == "inactive") user.Status = "Inactive";
            else user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();
        });
        using var response = await CheckoutAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("role", HttpStatusCode.Forbidden)]
    [InlineData("center", HttpStatusCode.NotFound)]
    [InlineData("staff", HttpStatusCode.Forbidden)]
    public async Task ChangedRoleOrStaffScopeTakesEffectWithoutTokenExpiry(string change, HttpStatusCode expected)
    {
        await BranchApiFactory.LoginAsync(client, "reception@test.local");
        await WithDatabase(async db =>
        {
            if (change == "role") (await db.Users.SingleAsync(user => user.Id == 20)).RoleId = 4;
            else
            {
                var staff = await db.StaffProfiles.SingleAsync(staff => staff.UserId == 20);
                if (change == "center") staff.CenterId = 2;
                else staff.Status = "Inactive";
            }
            await db.SaveChangesAsync();
        });
        using var response = await CheckoutAsync();
        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SignedStaleAdminClaimsCannotGrantCounterAccess()
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(10));
        using var response = await CheckoutAsync();
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/payments/vnpay-callback")]
    [InlineData("GET", "/api/payments/vnpay-ipn")]
    [InlineData("GET", "/api/payments/momo-callback")]
    [InlineData("POST", "/api/payments/momo-ipn")]
    [InlineData("POST", "/api/payments/payos-ipn")]
    public async Task GatewayCallbacksStayAnonymousAndRejectInvalidSignatures(string method, string path)
    {
        using var response = method == "GET" ? await client.GetAsync(path) : await client.PostAsJsonAsync(path, new { });
        Assert.Equal(path == "/api/payments/payos-ipn" ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
        await WithDatabase(async db => Assert.Empty(await db.Payments.ToListAsync()));
    }

    [Fact]
    public async Task LoginRetainsFiveAttemptLockout()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failure = await client.PostAsJsonAsync("/api/auth/login",
                new { email = "member@test.local", password = "WrongPassword" });
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }
        using var locked = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = BranchApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        await WithDatabase(async db =>
        {
            var user = await db.Users.SingleAsync(user => user.Id == 10);
            Assert.True(user.LockedUntil > DateTime.UtcNow.AddMinutes(14));
        });
    }

    [Fact]
    public async Task LoginKeepsDuyResponseAndJwtClaimContract()
    {
        var auth = await BranchApiFactory.LoginAsync(client, "reception@test.local");
        var token = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        Assert.Equal(20, auth.UserId);
        Assert.Equal("RECEPTIONIST", auth.Role);
        Assert.Equal("20", token.Subject);
        Assert.Equal("20", token.Claims.Single(claim => claim.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("RECEPTIONIST", token.Claims.Single(claim => claim.Type == ClaimTypes.Role).Value);
        Assert.Equal("1", token.Claims.Single(claim => claim.Type == "centerId").Value);
        Assert.Equal(SecurityAlgorithms.HmacSha256, token.Header.Alg);
        Assert.Equal(configuration["Jwt:Issuer"], token.Issuer);
        Assert.Equal(configuration["Jwt:Audience"], Assert.Single(token.Audiences));
        Assert.True(Math.Abs((auth.ExpiresAt.UtcDateTime - token.ValidTo).TotalSeconds) < 1);
    }

    [Fact]
    public async Task Pbkdf2AccountCanLoginAndCreatePaymentWithJwt()
    {
        await WithDatabase(async db =>
        {
            (await db.Users.SingleAsync(user => user.Id == 10)).PasswordHash = PasswordHashing.Hash(BranchApiFactory.Password);
            await db.SaveChangesAsync();
        });
        await BranchApiFactory.LoginAsync(client, "member@test.local");
        using var response = await client.PostAsJsonAsync("/api/payments/create-vnpay-url", new { packageId = 1 });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PasswordChangeReturnsUsableJwtFromSameIssuer()
    {
        await BranchApiFactory.LoginAsync(client, "member@test.local");
        const string newPassword = "Different@Password456!";
        using var change = await client.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = BranchApiFactory.Password, newPassword });
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        var auth = await change.Content.ReadFromJsonAsync<SportsCenterManagement.API.DTOs.Auth.AuthResponse>();
        Assert.NotNull(auth);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var payment = await client.PostAsJsonAsync("/api/payments/create-vnpay-url", new { packageId = 1 });
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        using var oldLogin = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "member@test.local", password = BranchApiFactory.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        using var newLogin = await client.PostAsJsonAsync("/api/auth/login", new { email = "member@test.local", password = newPassword });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task SwaggerExposesBearerJwtAuthorization()
    {
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"bearerFormat\": \"JWT\"", content);
        Assert.Contains("\"scheme\": \"bearer\"", content);
    }

    private Task<HttpResponseMessage> CheckoutAsync() => client.PostAsJsonAsync("/api/payments/counter-checkout",
        new { memberId = 1, packageId = 1, paymentMethod = "CASH", amountReceived = 100 });

    private async Task WithDatabase(Func<SportsCenterDbContext, Task> action)
    {
        using var scope = application.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<SportsCenterDbContext>());
    }

    private string CreateToken(long userId, string? failure = null)
    {
        if (failure == "opaque") return "legacy-opaque-token";
        var configuration = application.Services.GetRequiredService<IConfiguration>();
        var secret = failure == "signature" ? new string('x', 64) : configuration["Jwt:Key"]!;
        var subject = failure == "zero-id" ? "0" : failure == "negative-id" ? "-1" : userId.ToString();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, "Admin"), new("centerId", "1"), new("memberId", "999")
        };
        if (failure != "missing-id") claims.Add(new Claim("sub", subject));
        var token = new JwtSecurityToken(
            issuer: failure == "issuer" ? "wrong-issuer" : configuration["Jwt:Issuer"],
            audience: failure == "audience" ? "wrong-audience" : configuration["Jwt:Audience"],
            claims: claims,
            notBefore: failure == "future" ? DateTime.UtcNow.AddMinutes(5) : DateTime.UtcNow.AddMinutes(-10),
            expires: failure == "missing-expiration" ? null : failure == "expired"
                ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(10),
            signingCredentials: failure == "unsigned" ? null : new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                failure == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Dispose()
    {
        client.Dispose();
        application.Dispose();
        database.Dispose();
    }
}
