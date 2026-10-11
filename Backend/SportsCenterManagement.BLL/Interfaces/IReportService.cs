using SportsCenterManagement.BLL.DTOs.CoreFlows;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IReportService
{
    Task<int> GetCheckinCountAsync(long centerId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<RevenueReportResponse> GetRevenueReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default);

    Task<MembershipReportResponse> GetMembershipReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default);

    Task<ClassEnrollmentReportResponse> GetClassEnrollmentReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default);
}
