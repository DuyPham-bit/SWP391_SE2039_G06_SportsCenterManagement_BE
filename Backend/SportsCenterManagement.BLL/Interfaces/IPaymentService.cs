using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.DTOs.Payments;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IPaymentService
{
    Task<string> CreatePaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string ipAddress,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PaymentResultResponse> ProcessPaymentCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);

    Task<string> CreateMomoPaymentUrlAsync(
        long memberId,
        CreatePaymentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<PaymentResultResponse> ProcessMomoCallbackAsync(
        IDictionary<string, string> queryParams,
        CancellationToken cancellationToken = default);

    Task<PaymentResultResponse> ReconcilePendingPaymentAsync(
        long managerUserId,
        long? managerCenterId,
        string gatewayReference,
        ReconcilePendingPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<CounterPaymentResponse> ProcessCounterPaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string idempotencyKey,
        CounterPaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentResultResponse> VoidCounterPaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string invoiceNumber,
        string reason,
        CancellationToken cancellationToken = default);

    Task<InvoiceDetailsResponse?> GetInvoiceAsync(
        long actorUserId,
        string actorRole,
        long? actorCenterId,
        string invoiceNumber,
        CancellationToken cancellationToken = default);

    Task<InvoiceDetailsResponse> RecordInvoicePaymentAsync(
        long staffUserId,
        long? staffCenterId,
        string invoiceNumber,
        string idempotencyKey,
        RecordInvoicePaymentRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentRefundResponse> RefundPaymentAsync(
        long managerUserId,
        long? managerCenterId,
        long paymentId,
        string idempotencyKey,
        CreateRefundRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task<PaymentRefundResponse> ReconcileRefundAsync(
        long managerUserId,
        long? managerCenterId,
        long refundId,
        ReconcileRefundRequest request,
        CancellationToken cancellationToken = default);

    Task<RevenueReportResponse> GetRevenueReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default);
}
