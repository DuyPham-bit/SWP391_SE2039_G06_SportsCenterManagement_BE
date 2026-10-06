namespace SportsCenterManagement.BLL.DTOs.Payments;

public sealed record ProviderRefundResult(
    bool IsAuthenticated,
    string Status,
    string? ProviderRefundId,
    string Message);
