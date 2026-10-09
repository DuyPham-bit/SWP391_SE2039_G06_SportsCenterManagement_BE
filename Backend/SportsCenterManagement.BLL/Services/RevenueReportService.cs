using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Reports;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class RevenueReportService(IUnitOfWork unitOfWork, IReportService summaryReports) : IRevenueReportService
{
    private static readonly string[] ManagerRoles = ["Admin", "Manager", "CenterManager"];
    private const int MaxReturnedTransactions = 200;

    public async Task<RevenueReportResponse> GetRevenueReportAsync(long requesterUserId, long centerId,
        DateOnly from, DateOnly to, string? groupBy, bool includeTransactions, CancellationToken cancellationToken = default)
    {
        await StaffAuthorization.RequireAsync(unitOfWork, requesterUserId, centerId, ManagerRoles, cancellationToken);
        var normalizedGroupBy = string.IsNullOrWhiteSpace(groupBy) ? "day" : groupBy.Trim().ToLowerInvariant();

        // Use the existing financial report for totals and refund recognition dates.
        var summary = await summaryReports.GetRevenueReportAsync(centerId, from, to, normalizedGroupBy, cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(summary.TimeZone);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(from.ToDateTime(TimeOnly.MinValue), zone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var db = unitOfWork.Context;

        var payments = await (
            from payment in db.Payments.AsNoTracking()
            join invoice in db.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            join member in db.MemberProfiles.AsNoTracking() on payment.MemberId equals member.Id
            where invoice.CenterId == centerId && payment.PaymentStatus == "Succeeded"
                && payment.PaidAt != null && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select new RevenueTransactionDto(payment.Id, invoice.InvoiceNumber, payment.PaidAt!.Value,
                payment.PaymentStatus, payment.PaymentMethod, payment.TransactionCode, payment.Amount,
                member.Id, member.MemberCode, member.FullName, payment.ProcessedBy, null))
            .ToListAsync(cancellationToken);

        var refunds = await (
            from refund in db.PaymentRefunds.AsNoTracking()
            join payment in db.Payments.AsNoTracking() on refund.PaymentId equals payment.Id
            join invoice in db.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            join member in db.MemberProfiles.AsNoTracking() on payment.MemberId equals member.Id
            where invoice.CenterId == centerId && refund.Status == "Succeeded"
                && refund.ProcessedAt != null && refund.ProcessedAt >= startUtc && refund.ProcessedAt < endUtc
            select new RevenueTransactionDto(payment.Id, invoice.InvoiceNumber, refund.ProcessedAt!.Value,
                "Refunded", payment.PaymentMethod, refund.ProviderRefundId, refund.Amount,
                member.Id, member.MemberCode, member.FullName, refund.RequestedBy, refund.Id))
            .ToListAsync(cancellationToken);

        var voidedCount = await (
            from payment in db.Payments.AsNoTracking()
            join invoice in db.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && payment.PaymentStatus == "Voided"
                && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select payment.Id).CountAsync(cancellationToken);

        var paidInvoices = db.Invoices.AsNoTracking().Where(invoice => invoice.CenterId == centerId
            && invoice.PaidAt != null && invoice.PaidAt >= startUtc && invoice.PaidAt < endUtc
            && invoice.Status != "Cancelled" && invoice.Status != "Voided");
        var packageRows = await (
            from invoice in paidInvoices
            join item in db.InvoiceItems.AsNoTracking() on invoice.Id equals item.InvoiceId
            join package in db.MembershipPackages.AsNoTracking() on item.PackageId equals package.Id
            select new { package.Id, package.Name, item.Quantity, item.Amount }).ToListAsync(cancellationToken);

        DateOnly Period(DateTime timestamp)
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc), zone));
            return normalizedGroupBy == "month" ? new DateOnly(day.Year, day.Month, 1) : day;
        }

        var periods = summary.Periods.Select(period =>
        {
            var end = normalizedGroupBy == "month"
                ? new DateOnly(period.PeriodStart.Year, period.PeriodStart.Month,
                    DateTime.DaysInMonth(period.PeriodStart.Year, period.PeriodStart.Month))
                : period.PeriodStart;
            return new RevenuePeriodBucketDto(period.PeriodStart < from ? from : period.PeriodStart,
                end > to ? to : end, period.Gross, period.Refunds, period.Net,
                payments.Count(row => Period(row.PaidAtUtc) == period.PeriodStart),
                refunds.Where(row => Period(row.PaidAtUtc) == period.PeriodStart).Select(row => row.PaymentId).Distinct().Count());
        }).ToList();

        return new RevenueReportResponse
        {
            CenterId = centerId, From = from, To = to, GroupBy = normalizedGroupBy,
            TimeZone = summary.TimeZone, GeneratedAtUtc = DateTime.UtcNow,
            GrossRevenue = summary.Gross, RefundAmount = summary.Refunds, NetRevenue = summary.Net,
            SuccessfulPaymentCount = payments.Count,
            RefundedPaymentCount = refunds.Select(row => row.PaymentId).Distinct().Count(),
            VoidedPaymentCount = voidedCount,
            PaidInvoiceCount = await paidInvoices.CountAsync(cancellationToken),
            AverageTicket = payments.Count == 0 ? 0 : summary.Gross / payments.Count,
            Periods = periods,
            PaymentMethods = payments.GroupBy(row => row.PaymentMethod)
                .Select(group => new RevenuePaymentMethodDto(group.Key, group.Sum(row => row.Amount), group.Count()))
                .OrderByDescending(row => row.GrossRevenue).ThenBy(row => row.PaymentMethod).ToList(),
            Packages = packageRows.GroupBy(row => new { row.Id, row.Name })
                .Select(group => new RevenuePackageDto(group.Key.Id, group.Key.Name, group.Sum(row => row.Quantity), group.Sum(row => row.Amount)))
                .OrderByDescending(row => row.Revenue).ThenBy(row => row.PackageName).ToList(),
            Transactions = includeTransactions
                ? payments.Concat(refunds).OrderByDescending(row => row.PaidAtUtc).ThenByDescending(row => row.PaymentId)
                    .Take(MaxReturnedTransactions)
                    .Select(row => row with { PaidAtUtc = DateTime.SpecifyKind(row.PaidAtUtc, DateTimeKind.Utc) }).ToList()
                : []
        };
    }
}
