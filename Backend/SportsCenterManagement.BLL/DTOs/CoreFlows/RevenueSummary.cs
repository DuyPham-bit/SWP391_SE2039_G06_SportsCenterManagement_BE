namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

public sealed record RevenueSummary(
    long CenterId,
    DateOnly From,
    DateOnly To,
    decimal Gross,
    decimal Refunds,
    decimal Net);
