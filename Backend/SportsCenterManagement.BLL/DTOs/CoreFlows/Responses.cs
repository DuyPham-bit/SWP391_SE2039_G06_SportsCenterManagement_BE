namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

public static class Responses
{
    public sealed record PendingMembershipResult(
        long SubscriptionId,
        long InvoiceId,
        string InvoiceNumber,
        decimal Amount);

    public sealed record RevenueSummary(
        long CenterId,
        DateOnly From,
        DateOnly To,
        decimal Gross,
        decimal Refunds,
        decimal Net);
}
