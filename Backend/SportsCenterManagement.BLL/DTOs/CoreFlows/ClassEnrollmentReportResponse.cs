namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

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
