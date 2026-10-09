using SportsCenterManagement.BLL.DTOs.Reports;

namespace SportsCenterManagement.BLL.Interfaces;

public interface IRevenueReportService
{
    Task<DetailedRevenueReportResponse> GetRevenueReportAsync(long requesterUserId, long centerId,
        DateOnly from, DateOnly to, string? groupBy, bool includeTransactions,
        CancellationToken cancellationToken = default);
}
