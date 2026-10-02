namespace SportsCenterManagement.BLL.DTOs.CoreFlows;

/// <summary>
/// Báo cáo tổng hợp doanh thu của trung tâm thể thao trong một khoảng thời gian.
/// </summary>
/// <param name="CenterId">Mã trung tâm thể thao.</param>
/// <param name="From">Ngày bắt đầu kỳ báo cáo.</param>
/// <param name="To">Ngày kết thúc kỳ báo cáo.</param>
/// <param name="Gross">Tổng doanh thu phát sinh (tổng thu chưa trừ hoàn tiền).</param>
/// <param name="Refunds">Tổng số tiền hoàn lại.</param>
/// <param name="Net">Doanh thu thuần thực tế (Gross - Refunds).</param>
public sealed record RevenueSummary(
    long CenterId,
    DateOnly From,
    DateOnly To,
    decimal Gross,
    decimal Refunds,
    decimal Net);
