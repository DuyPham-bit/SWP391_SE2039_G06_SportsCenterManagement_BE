using System.Net;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Services;

namespace SportsCenterManagement.Tests;

public class MoMoServiceTests
{
    [Fact]
    public async Task CreatePaymentUrlAsync_SandboxRejectsAndFallbackIsOff_Throws()
    {
        var momo = Service(allowFallback: null);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            momo.CreatePaymentUrlAsync("PAY-TEST-1", 650000m, "Thanh toan"));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }

    [Fact]
    public async Task CreatePaymentUrlAsync_SandboxRejectsAndFallbackIsOn_ReturnsServerSignedRedirect()
    {
        var momo = Service(allowFallback: "true");

        var created = await momo.CreatePaymentUrlAsync("PAY-TEST-2", 650000m, "Thanh toan");

        Assert.Equal(0, created.ResultCode);
        Assert.StartsWith("https://app.example.test/payment-result?", created.PayUrl);
    }

    private static MoMoService Service(string? allowFallback)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Momo:PartnerCode"] = "MOMO",
            ["Momo:AccessKey"] = "test-access",
            ["Momo:SecretKey"] = "test-secret",
            ["Momo:PaymentUrl"] = "https://test-payment.momo.vn/v2/gateway/api/create",
            ["Momo:ReturnUrl"] = "https://app.example.test/payment-result",
            ["Momo:NotifyUrl"] = "https://api.example.test/api/payments/momo-ipn",
            ["Momo:AllowSandboxFallback"] = allowFallback
        }).Build();
        return new MoMoService(config, new HttpClient(new ForbiddenHandler()));
    }

    private sealed class ForbiddenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("403") });
    }
}
