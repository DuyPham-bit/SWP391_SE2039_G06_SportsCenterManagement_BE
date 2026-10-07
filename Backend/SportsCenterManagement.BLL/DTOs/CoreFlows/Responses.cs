namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
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
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record RevenueSummary(
    long CenterId,
    DateOnly From,
    DateOnly To,
    decimal Gross,
    decimal Refunds,
    decimal Net);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
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
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record PendingMembershipResult(
    long SubscriptionId,
    long InvoiceId,
    string InvoiceNumber,
    decimal Amount);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record MembershipReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    int TotalMembers,
    int ActiveMembers,
    int NewMembers,
    IReadOnlyList<MembershipReportPeriod> Periods);

public sealed record MembershipReportPeriod(DateOnly PeriodStart, int NewMembers);
}

namespace SportsCenterManagement.BLL.DTOs.CoreFlows
{
public sealed record ClassEnrollmentReportResponse(
    long CenterId,
    DateOnly From,
    DateOnly To,
    string TimeZone,
    string GroupBy,
    IReadOnlyDictionary<string, int> CurrentStatusCounts,
    IReadOnlyList<ClassEnrollmentReportPeriod> Periods);

public sealed record ClassEnrollmentReportPeriod(
    DateOnly PeriodStart,
    IReadOnlyDictionary<string, int> StatusCounts);
}
