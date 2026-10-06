namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

public sealed record RevenueReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string GroupBy,
    decimal Gross,
    decimal Refunds,
    decimal Net,
    IReadOnlyList<RevenuePeriod> Periods);

public sealed record RevenuePeriod(
    DateOnly PeriodStart,
    decimal Gross,
    decimal Refunds,
    decimal Net);
