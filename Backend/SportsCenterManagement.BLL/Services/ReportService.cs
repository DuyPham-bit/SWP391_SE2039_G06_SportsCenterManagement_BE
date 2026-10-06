using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.CoreFlows;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ReportService(
    IUnitOfWork unitOfWork,
    IPaymentService paymentService,
    IConfiguration configuration) : IReportService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IPaymentService _paymentService = paymentService;
    private readonly IConfiguration _configuration = configuration;

    public Task<RevenueReportResponse> GetRevenueReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default) =>
        _paymentService.GetRevenueReportAsync(centerId, from, to, groupBy, cancellationToken);

    public async Task<MembershipReportResponse> GetMembershipReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(from, to, groupBy);
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        var timeZone = GetReportTimeZone();
        var (startUtc, endUtc) = GetUtcRange(from, to, timeZone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var memberRows = from profile in _unitOfWork.Context.MemberProfiles
                         join subscription in _unitOfWork.Context.MemberSubscriptions on profile.Id equals subscription.MemberId
                         join package in _unitOfWork.Context.MembershipPackages on subscription.PackageId equals package.Id
                         where package.CenterId == centerId
                         select new { profile.Id, subscription.CreatedAt };
        var firstCenterSubscriptions = await memberRows
            .GroupBy(row => row.Id)
            .Select(group => new { Id = group.Key, CreatedAt = group.Min(row => row.CreatedAt) })
            .ToListAsync(cancellationToken);
        var totalMembers = firstCenterSubscriptions.Count;
        var activeMembers = await (
            from subscription in _unitOfWork.Context.MemberSubscriptions
            join package in _unitOfWork.Context.MembershipPackages on subscription.PackageId equals package.Id
            where package.CenterId == centerId && subscription.Status == "Active" &&
                  subscription.StartDate <= today && subscription.EndDate >= today
            select subscription.MemberId).Distinct().CountAsync(cancellationToken);
        var newMemberRows = firstCenterSubscriptions
            .Where(row => row.CreatedAt >= startUtc && row.CreatedAt < endUtc)
            .ToArray();
        var newMembersByPeriod = newMemberRows
            .GroupBy(row => PeriodFor(row.CreatedAt, groupBy, timeZone))
            .ToDictionary(group => group.Key, group => group.Count());
        var periods = CreatePeriodStarts(from, to, groupBy)
            .Select(period => new MembershipReportPeriod(
                period, newMembersByPeriod.GetValueOrDefault(period)))
            .ToArray();

        return new MembershipReportResponse(
            centerId, from, to, timeZone.Id, totalMembers, activeMembers,
            newMemberRows.Length, periods);
    }

    public async Task<ClassEnrollmentReportResponse> GetClassEnrollmentReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(from, to, groupBy);
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        var timeZone = GetReportTimeZone();
        var (startUtc, endUtc) = GetUtcRange(from, to, timeZone);
        var enrollmentCounts = await (
            from enrollment in _unitOfWork.Context.ClassEnrollments
            join classEntity in _unitOfWork.Context.Classes on enrollment.ClassId equals classEntity.Id
            where classEntity.CenterId == centerId
            group enrollment by enrollment.Status into statusGroup
            select new { Status = statusGroup.Key, Count = statusGroup.Count() })
            .ToListAsync(cancellationToken);
        var waitlistCounts = await (
            from item in _unitOfWork.Context.ClassWaitlists
            join classEntity in _unitOfWork.Context.Classes on item.ClassId equals classEntity.Id
            where classEntity.CenterId == centerId
            group item by item.Status into statusGroup
            select new { Status = statusGroup.Key, Count = statusGroup.Count() })
            .ToListAsync(cancellationToken);

        var currentStatusCounts = enrollmentCounts.Concat(waitlistCounts)
            .GroupBy(row => NormalizeStatus(row.Status), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Count), StringComparer.OrdinalIgnoreCase);

        var enrollmentEvents = await (
            from enrollment in _unitOfWork.Context.ClassEnrollments
            join classEntity in _unitOfWork.Context.Classes on enrollment.ClassId equals classEntity.Id
            let occurredAt = enrollment.Status == "Cancelled"
                ? enrollment.CancelledAt ?? enrollment.RegisteredAt
                : enrollment.RegisteredAt
            where classEntity.CenterId == centerId && occurredAt >= startUtc && occurredAt < endUtc
            select new { enrollment.Status, CreatedAtUtc = occurredAt })
            .ToListAsync(cancellationToken);
        var waitlistEvents = await (
            from item in _unitOfWork.Context.ClassWaitlists
            join classEntity in _unitOfWork.Context.Classes on item.ClassId equals classEntity.Id
            where classEntity.CenterId == centerId && item.JoinedAt >= startUtc && item.JoinedAt < endUtc
            select new { item.Status, CreatedAtUtc = item.JoinedAt })
            .ToListAsync(cancellationToken);
        var statusEvents = enrollmentEvents
            .Select(row => new StatusEvent(row.Status, row.CreatedAtUtc))
            .Concat(waitlistEvents.Select(row => new StatusEvent(row.Status, row.CreatedAtUtc)))
            .ToArray();
        var statusCountsByPeriod = statusEvents
            .GroupBy(row => PeriodFor(row.CreatedAtUtc, groupBy, timeZone))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<string, int>)group
                    .GroupBy(row => NormalizeStatus(row.Status), StringComparer.OrdinalIgnoreCase)
                    .OrderBy(statusGroup => statusGroup.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(statusGroup => statusGroup.Key, statusGroup => statusGroup.Count(), StringComparer.OrdinalIgnoreCase));
        var periods = CreatePeriodStarts(from, to, groupBy)
            .Select(period => new ClassEnrollmentReportPeriod(
                period, statusCountsByPeriod.GetValueOrDefault(period) ?? new Dictionary<string, int>()))
            .ToArray();

        return new ClassEnrollmentReportResponse(
            centerId, from, to, timeZone.Id, groupBy.Trim().ToLowerInvariant(), currentStatusCounts, periods);
    }

    private async Task EnsureCenterExistsAsync(long centerId, CancellationToken cancellationToken)
    {
        if (centerId <= 0 || !await _unitOfWork.Context.Centers.AnyAsync(
                center => center.Id == centerId, cancellationToken))
        {
            throw BusinessException.NotFound("Không tìm thấy trung tâm.");
        }
    }

    private static void ValidateRequest(DateOnly from, DateOnly to, string groupBy)
    {
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
        {
            throw BusinessException.BadRequest("Khoảng ngày phải hợp lệ và không vượt quá 367 ngày.");
        }
        if (!string.Equals(groupBy?.Trim(), "day", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(groupBy?.Trim(), "month", StringComparison.OrdinalIgnoreCase))
        {
            throw BusinessException.BadRequest("groupBy chỉ nhận day hoặc month.");
        }
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetUtcRange(
        DateOnly from, DateOnly to, TimeZoneInfo timeZone)
    {
        var startLocal = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var endLocal = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone), TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone));
    }

    private static IReadOnlyList<DateOnly> CreatePeriodStarts(DateOnly from, DateOnly to, string groupBy)
    {
        var result = new SortedSet<DateOnly>();
        var day = from;
        while (day <= to)
        {
            var period = groupBy.Trim().Equals("month", StringComparison.OrdinalIgnoreCase)
                ? new DateOnly(day.Year, day.Month, 1)
                : day;
            result.Add(period);
            day = groupBy.Trim().Equals("month", StringComparison.OrdinalIgnoreCase)
                ? period.AddMonths(1)
                : day.AddDays(1);
        }
        return result.ToArray();
    }

    private static DateOnly PeriodFor(DateTime utc, string groupBy, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
        var date = DateOnly.FromDateTime(local);
        return groupBy.Trim().Equals("month", StringComparison.OrdinalIgnoreCase)
            ? new DateOnly(date.Year, date.Month, 1)
            : date;
    }

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "Unknown" : status.Trim();

    private TimeZoneInfo GetReportTimeZone()
    {
        var configured = _configuration["Reports:TimeZoneId"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(configured);
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }

    private sealed record StatusEvent(string Status, DateTime CreatedAtUtc);
}
