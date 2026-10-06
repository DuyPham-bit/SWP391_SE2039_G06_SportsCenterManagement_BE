namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

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
