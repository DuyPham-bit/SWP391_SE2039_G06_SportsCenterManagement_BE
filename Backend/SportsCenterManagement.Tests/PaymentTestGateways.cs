using SportsCenterManagement.BLL.DTOs.Payments;
using SportsCenterManagement.BLL.Interfaces;

namespace SportsCenterManagement.Tests;

internal sealed class PaymentTestVnPay : IVnPayService
{
    public string CreatePaymentUrl(string invoiceNumber, decimal amount, string orderDescription,
        string ipAddress, string? bankCode = null) => "https://gateway.test/vnpay";

    public VnPayCallbackResult ProcessCallback(IDictionary<string, string> queryParams) =>
        new() { IsValidSignature = false };

    public Task<ProviderRefundResult> RefundAsync(string paymentReference, string requestId,
        string providerTransactionId, bool isFullRefund, DateTime paidAtUtc, decimal amount,
        string reason, string requestedBy, string ipAddress, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected gateway refund in JWT integration test.");
}

internal sealed class PaymentTestMomo : IMoMoService
{
    public Task<MomoCreatePaymentResponse> CreatePaymentUrlAsync(string orderId, decimal amount,
        string orderInfo, CancellationToken cancellationToken = default) => Task.FromResult(new MomoCreatePaymentResponse
        {
            ResultCode = 0, OrderId = orderId, RequestId = orderId, Amount = (long)amount,
            PartnerCode = "test-only", PayUrl = "https://test-payment.momo.vn/payment"
        });

    public MomoCallbackResult ProcessCallback(IDictionary<string, string> queryParams) =>
        new() { IsValidSignature = false };

    public Task<ProviderRefundResult> RefundAsync(string requestId, string refundOrderId,
        string providerTransactionId, decimal amount, string reason, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Unexpected gateway refund in JWT integration test.");
}

internal sealed class PaymentTestPayOs : IPayOsService
{
    public Task<PayOsCreatePaymentResponse> CreatePaymentLinkAsync(long orderCode, decimal amount,
        string description, string itemName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PayOsCreatePaymentResponse
        {
            Code = "00", Data = new PayOsPaymentData
            { OrderCode = orderCode, Amount = (int)amount, CheckoutUrl = "https://gateway.test/payos" }
        });

    public bool VerifyWebhookSignature(PayOsWebhookRequest webhookRequest) => false;
}
