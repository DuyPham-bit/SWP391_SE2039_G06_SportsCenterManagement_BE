namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

public sealed record RevenueAnalyticsReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string GroupBy,
    decimal Gross,
    decimal Refunds,
    decimal Net,
    int SuccessfulPaymentCount,
    IReadOnlyList<RevenueAnalyticsPeriod> Periods,
    IReadOnlyList<RevenueBreakdownItem> Rooms,
    IReadOnlyList<RevenueBreakdownItem> Sports,
    IReadOnlyList<RevenueBreakdownItem> Packages,
    IReadOnlyList<RevenueBreakdownItem> Coaches,
    IReadOnlyList<RevenueBreakdownItem> Members);

public sealed record RevenueAnalyticsPeriod(
    DateOnly PeriodStart,
    decimal Gross,
    decimal Refunds,
    decimal Net);

public sealed record RevenueBreakdownItem(
    long? Id,
    string? Code,
    string Name,
    decimal Gross,
    decimal Refunds,
    decimal Net,
    int ActivityCount);
