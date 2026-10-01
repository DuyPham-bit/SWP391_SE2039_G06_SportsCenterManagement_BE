namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed record CashPaymentResponse(
    long PaymentId,
    long InvoiceId,
    decimal Amount,
    string Status,
    DateTime PaidAt);
