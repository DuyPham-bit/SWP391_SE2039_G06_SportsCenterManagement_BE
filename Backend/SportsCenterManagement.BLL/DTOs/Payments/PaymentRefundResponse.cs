namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed record PaymentRefundResponse(
    long RefundId,
    long PaymentId,
    string InvoiceNumber,
    decimal Amount,
    string Status,
    string? ProviderRefundId,
    string? ExternalReference,
    string Message,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc);
