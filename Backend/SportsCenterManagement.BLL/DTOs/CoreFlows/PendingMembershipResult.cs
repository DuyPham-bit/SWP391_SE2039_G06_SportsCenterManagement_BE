namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

public sealed record PendingMembershipResult(
    long SubscriptionId,
    long InvoiceId,
    string InvoiceNumber,
    decimal Amount);
