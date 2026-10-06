namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed record InvoiceDetailsResponse(
    long Id,
    string InvoiceNumber,
    long MemberId,
    long CenterId,
    DateTime IssuedAtUtc,
    DateTime? PaidAtUtc,
    string Status,
    decimal Subtotal,
    decimal Discount,
    decimal Tax,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal OutstandingBalance,
    IReadOnlyList<InvoiceLineResponse> Items,
    IReadOnlyList<InvoicePaymentResponse> Payments);

public sealed record InvoiceLineResponse(
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record InvoicePaymentResponse(
    long Id,
    string PaymentMethod,
    string Status,
    decimal Amount,
    string? TransactionCode,
    string? ProviderTransactionId,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    IReadOnlyList<InvoiceRefundResponse> Refunds);

public sealed record InvoiceRefundResponse(
    long Id,
    decimal Amount,
    string Status,
    string Reason,
    string? ProviderRefundId,
    string? ExternalReference,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
