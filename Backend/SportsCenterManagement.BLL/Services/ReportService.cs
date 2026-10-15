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

    public async Task<int> GetCheckinCountAsync(long centerId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(from, to, "day");
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        var (startUtc, endUtc) = GetUtcRange(from, to, GetReportTimeZone());
        return await _unitOfWork.Context.CenterCheckins.CountAsync(c => c.CenterId == centerId &&
            c.CheckInTime >= startUtc && c.CheckInTime < endUtc, cancellationToken);
    }

    public Task<RevenueReportResponse> GetRevenueReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default) =>
        _paymentService.GetRevenueReportAsync(centerId, from, to, groupBy, cancellationToken);

    public async Task<RevenueAnalyticsReportResponse> GetRevenueAnalyticsAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        string groupBy,
        CancellationToken cancellationToken = default)
    {
        groupBy = (groupBy ?? string.Empty).Trim().ToLowerInvariant();
        ValidateAnalyticsRequest(from, to, groupBy);
        await EnsureCenterExistsAsync(centerId, cancellationToken);

        var db = _unitOfWork.Context;
        var timeZone = GetReportTimeZone();
        var (startUtc, endUtc) = GetUtcRange(from, to, timeZone);
        var paidRows = await (
            from payment in db.Payments.AsNoTracking()
            join invoice in db.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && payment.PaymentStatus == "Succeeded" &&
                  payment.PaidAt != null && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select new
            {
                payment.Id,
                payment.InvoiceId,
                payment.MemberId,
                payment.Amount,
                OccurredAt = payment.PaidAt!.Value
            }).ToListAsync(cancellationToken);

        var refundRows = await (
            from refund in db.PaymentRefunds.AsNoTracking()
            join payment in db.Payments.AsNoTracking() on refund.PaymentId equals payment.Id
            join invoice in db.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && refund.Status == "Succeeded" &&
                  refund.ProcessedAt != null && refund.ProcessedAt >= startUtc && refund.ProcessedAt < endUtc
            select new
            {
                payment.Id,
                payment.InvoiceId,
                payment.MemberId,
                refund.Amount,
                OccurredAt = refund.ProcessedAt!.Value
            }).ToListAsync(cancellationToken);

        var financialEvents = new List<AnalyticsFinancialEvent>(paidRows.Count + refundRows.Count);
        financialEvents.AddRange(paidRows.Select(row => new AnalyticsFinancialEvent(
            row.Id, row.InvoiceId, row.MemberId, row.Amount, row.OccurredAt, IsRefund: false)));
        financialEvents.AddRange(refundRows.Select(row => new AnalyticsFinancialEvent(
            row.Id, row.InvoiceId, row.MemberId, row.Amount, row.OccurredAt, IsRefund: true)));

        var invoiceIds = financialEvents.Select(row => row.InvoiceId).Distinct().ToArray();
        var invoiceLines = await db.InvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .Select(item => new
            {
                item.InvoiceId,
                item.PackageId,
                item.SubscriptionId,
                item.Description,
                item.Amount
            }).ToListAsync(cancellationToken);
        var linesByInvoice = invoiceLines
            .GroupBy(row => row.InvoiceId)
            .ToDictionary(group => group.Key, group => group.Select(row => new AnalyticsInvoiceLine(
                row.PackageId, row.SubscriptionId, row.Description, row.Amount)).ToArray());

        var subscriptionIds = invoiceLines.Where(row => row.SubscriptionId.HasValue)
            .Select(row => row.SubscriptionId!.Value).Distinct().ToArray();
        var bookingRows = await (
            from booking in db.SessionBookings.AsNoTracking()
            join session in db.ClassSessions.AsNoTracking() on booking.SessionId equals session.Id
            join classEntity in db.Classes.AsNoTracking() on session.ClassId equals classEntity.Id
            where booking.SubscriptionId.HasValue && subscriptionIds.Contains(booking.SubscriptionId.Value) &&
                  (booking.Status == "Booked" || booking.Status == "CANCELLED_LATE_CHARGED") &&
                  classEntity.CenterId == centerId
            select new
            {
                SubscriptionId = booking.SubscriptionId!.Value,
                ClassId = classEntity.Id,
                session.ScheduleId,
                RoomId = session.RoomId ?? classEntity.RoomId,
                SportId = classEntity.SportId,
                session.CoachId
            }).ToListAsync(cancellationToken);

        var classIds = bookingRows.Select(row => row.ClassId).Distinct().ToArray();
        var assignedCoachRows = await db.ClassCoaches.AsNoTracking()
            .Where(row => classIds.Contains(row.ClassId))
            .OrderByDescending(row => row.IsPrimary)
            .ThenBy(row => row.CoachId)
            .Select(row => new { row.ClassId, row.CoachId })
            .ToListAsync(cancellationToken);
        var assignedCoachByClass = assignedCoachRows
            .GroupBy(row => row.ClassId)
            .ToDictionary(group => group.Key, group => (long?)group.First().CoachId);

        var scheduleIds = bookingRows.Where(row => row.ScheduleId.HasValue)
            .Select(row => row.ScheduleId!.Value).Distinct().ToArray();
        var scheduleRoomRows = await db.ClassSchedules.AsNoTracking()
            .Where(row => scheduleIds.Contains(row.Id) && row.RoomId.HasValue)
            .Select(row => new { row.Id, RoomId = row.RoomId!.Value })
            .ToListAsync(cancellationToken);
        var scheduleRoomById = scheduleRoomRows.ToDictionary(row => row.Id, row => (long?)row.RoomId);

        var rooms = await db.Rooms.AsNoTracking().Where(row => row.CenterId == centerId).ToListAsync(cancellationToken);
        var sports = await db.Sports.AsNoTracking()
            .Where(row => row.CenterId == centerId || row.CenterId == null)
            .ToListAsync(cancellationToken);
        var packages = await db.MembershipPackages.AsNoTracking()
            .Where(row => row.CenterId == centerId).ToListAsync(cancellationToken);
        var coaches = await db.CoachProfiles.AsNoTracking()
            .Where(row => row.CenterId == centerId).ToListAsync(cancellationToken);
        var members = await db.MemberProfiles.AsNoTracking()
            .Where(row => row.CenterId == centerId).ToListAsync(cancellationToken);

        var roomNames = rooms.ToDictionary(row => row.Id, row => row.Name);
        var sportNames = sports.ToDictionary(row => row.Id, row => row.Name);
        var packageNames = packages.ToDictionary(row => row.Id, row => row.Name);
        var coachNames = coaches.ToDictionary(row => row.Id, row => row.FullName);
        var memberProfiles = members.ToDictionary(row => row.Id);

        var roomUsageBySubscription = new Dictionary<long, Dictionary<long, int>>();
        var sportUsageBySubscription = new Dictionary<long, Dictionary<long, int>>();
        var coachUsageBySubscription = new Dictionary<long, Dictionary<long, int>>();
        foreach (var booking in bookingRows)
        {
            var roomId = booking.RoomId;
            if (!roomId.HasValue && booking.ScheduleId.HasValue)
                scheduleRoomById.TryGetValue(booking.ScheduleId.Value, out roomId);
            var coachId = booking.CoachId;
            if (!coachId.HasValue)
                assignedCoachByClass.TryGetValue(booking.ClassId, out coachId);

            AddUsage(roomUsageBySubscription, booking.SubscriptionId, roomId ?? 0);
            AddUsage(sportUsageBySubscription, booking.SubscriptionId, booking.SportId);
            AddUsage(coachUsageBySubscription, booking.SubscriptionId, coachId ?? 0);
        }

        var roomBreakdown = rooms.ToDictionary(row => row.Id,
            row => new MutableRevenueBreakdown(row.Id, null, row.Name));
        var sportBreakdown = sports.ToDictionary(row => row.Id,
            row => new MutableRevenueBreakdown(row.Id, null, row.Name));
        var packageBreakdown = packages.ToDictionary(row => row.Id,
            row => new MutableRevenueBreakdown(row.Id, null, row.Name));
        var coachBreakdown = coaches.ToDictionary(row => row.Id,
            row => new MutableRevenueBreakdown(row.Id, null, row.FullName));
        var memberBreakdown = members.ToDictionary(row => row.Id,
            row => new MutableRevenueBreakdown(row.Id, row.MemberCode, row.FullName));
        var periodBreakdown = CreateAnalyticsPeriodStarts(from, to, groupBy)
            .ToDictionary(period => period, _ => new MutableRevenuePeriod());

        foreach (var financialEvent in financialEvents)
        {
            if (financialEvent.Amount <= 0) continue;

            var eventPeriod = GetPeriodStart(DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(financialEvent.OccurredAt, DateTimeKind.Utc), timeZone)), groupBy);
            if (!periodBreakdown.TryGetValue(eventPeriod, out var period))
            {
                period = new MutableRevenuePeriod();
                periodBreakdown[eventPeriod] = period;
            }
            period.Add(financialEvent.Amount, financialEvent.IsRefund);

            if (!memberProfiles.TryGetValue(financialEvent.MemberId, out var member))
                memberBreakdown.TryAdd(financialEvent.MemberId, new MutableRevenueBreakdown(
                    financialEvent.MemberId, null, $"Hội viên #{financialEvent.MemberId}"));
            var memberRow = memberBreakdown[financialEvent.MemberId];
            memberRow.Add(financialEvent.Amount, financialEvent.IsRefund);
            if (!financialEvent.IsRefund) memberRow.ActivityCount++;

            var hasLines = linesByInvoice.TryGetValue(financialEvent.InvoiceId, out var lines) && lines.Length > 0;
            IReadOnlyList<(AnalyticsInvoiceLine Item, decimal Amount)> lineAllocations = hasLines
                ? SplitAmount(financialEvent.Amount, lines!, row => Math.Max(0, row.Amount))
                : Array.Empty<(AnalyticsInvoiceLine Item, decimal Amount)>();
            if (lineAllocations.Count == 0)
            {
                AddRevenue(packageBreakdown, 0, "Chưa phân loại", financialEvent.Amount, financialEvent.IsRefund);
                AddRevenue(roomBreakdown, 0, "Chưa phân bổ theo lượt đặt", financialEvent.Amount, financialEvent.IsRefund);
                AddRevenue(sportBreakdown, 0, "Chưa phân bổ theo lượt đặt", financialEvent.Amount, financialEvent.IsRefund);
                AddRevenue(coachBreakdown, 0, "Chưa phân bổ theo lượt đặt", financialEvent.Amount, financialEvent.IsRefund);
                if (!financialEvent.IsRefund) packageBreakdown[0].ActivityCount++;
                continue;
            }

            var packageIdsForPayment = new HashSet<long>();
            foreach (var (line, lineAmount) in lineAllocations)
            {
                var packageId = line.PackageId ?? 0;
                var packageName = packageId > 0 && packageNames.TryGetValue(packageId, out var knownPackageName)
                    ? knownPackageName
                    : "Chưa phân loại";
                AddRevenue(packageBreakdown, packageId, packageName, lineAmount, financialEvent.IsRefund);
                packageIdsForPayment.Add(packageId);

                AddRevenueByUsage(roomBreakdown, roomNames, roomUsageBySubscription,
                    line.SubscriptionId, lineAmount, financialEvent.IsRefund, "Chưa phân bổ theo lượt đặt");
                AddRevenueByUsage(sportBreakdown, sportNames, sportUsageBySubscription,
                    line.SubscriptionId, lineAmount, financialEvent.IsRefund, "Chưa phân bổ theo lượt đặt");
                AddRevenueByUsage(coachBreakdown, coachNames, coachUsageBySubscription,
                    line.SubscriptionId, lineAmount, financialEvent.IsRefund, "Chưa phân bổ theo lượt đặt");
            }
            if (!financialEvent.IsRefund)
            {
                foreach (var packageId in packageIdsForPayment)
                {
                    var fallbackName = packageId > 0 && packageNames.TryGetValue(packageId, out var name)
                        ? name
                        : "Chưa phân loại";
                    EnsureBreakdown(packageBreakdown, packageId, fallbackName).ActivityCount++;
                }
            }
        }

        SetUsageCounts(roomBreakdown, SumUsage(roomUsageBySubscription), roomNames, "Sân/phòng chưa xác định");
        SetUsageCounts(sportBreakdown, SumUsage(sportUsageBySubscription), sportNames, "Môn chưa xác định");
        SetUsageCounts(coachBreakdown, SumUsage(coachUsageBySubscription), coachNames, "HLV chưa xác định");

        var gross = paidRows.Sum(row => row.Amount);
        var refunds = refundRows.Sum(row => row.Amount);
        return new RevenueAnalyticsReportResponse(
            centerId,
            from,
            to,
            timeZone.Id,
            groupBy,
            gross,
            refunds,
            gross - refunds,
            paidRows.Count,
            periodBreakdown.OrderBy(row => row.Key).Select(row => new RevenueAnalyticsPeriod(
                row.Key, row.Value.Gross, row.Value.Refunds, row.Value.Gross - row.Value.Refunds)).ToArray(),
            ToBreakdownItems(roomBreakdown),
            ToBreakdownItems(sportBreakdown),
            ToBreakdownItems(packageBreakdown),
            ToBreakdownItems(coachBreakdown),
            ToBreakdownItems(memberBreakdown));
    }

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
        var centerMembers = await _unitOfWork.Context.MemberProfiles.AsNoTracking()
            .Where(profile => profile.CenterId == centerId)
            .Select(profile => new { profile.Id, profile.CreatedAt })
            .ToListAsync(cancellationToken);
        var totalMembers = centerMembers.Count;
        var activeMembers = await (
            from subscription in _unitOfWork.Context.MemberSubscriptions
            join package in _unitOfWork.Context.MembershipPackages on subscription.PackageId equals package.Id
            where package.CenterId == centerId && subscription.Status == "Active" &&
                  subscription.StartDate <= today && subscription.EndDate >= today
            select subscription.MemberId).Distinct().CountAsync(cancellationToken);
        var newMemberRows = centerMembers
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

    private static void ValidateAnalyticsRequest(DateOnly from, DateOnly to, string groupBy)
    {
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 366)
            throw BusinessException.BadRequest("Khoảng ngày phải hợp lệ và không vượt quá 367 ngày.");
        if (groupBy is not ("day" or "week" or "month" or "quarter" or "year"))
            throw BusinessException.BadRequest("groupBy chỉ nhận day, week, month, quarter hoặc year.");
    }

    private static IReadOnlyList<DateOnly> CreateAnalyticsPeriodStarts(DateOnly from, DateOnly to, string groupBy)
    {
        var starts = new SortedSet<DateOnly>();
        for (var day = from; day <= to; day = day.AddDays(1))
            starts.Add(GetPeriodStart(day, groupBy));
        return starts.ToArray();
    }

    private static DateOnly GetPeriodStart(DateOnly date, string groupBy) => groupBy switch
    {
        "week" => date.AddDays(-(((int)date.DayOfWeek + 6) % 7)),
        "month" => new DateOnly(date.Year, date.Month, 1),
        "quarter" => new DateOnly(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
        "year" => new DateOnly(date.Year, 1, 1),
        _ => date
    };

    private static void AddUsage(Dictionary<long, Dictionary<long, int>> usageBySubscription,
        long subscriptionId, long dimensionId)
    {
        if (!usageBySubscription.TryGetValue(subscriptionId, out var dimensionUsage))
        {
            dimensionUsage = [];
            usageBySubscription[subscriptionId] = dimensionUsage;
        }
        dimensionUsage[dimensionId] = dimensionUsage.GetValueOrDefault(dimensionId) + 1;
    }

    private static IReadOnlyDictionary<long, int> SumUsage(
        Dictionary<long, Dictionary<long, int>> usageBySubscription)
    {
        var result = new Dictionary<long, int>();
        foreach (var dimensionUsage in usageBySubscription.Values)
        foreach (var (dimensionId, count) in dimensionUsage)
            result[dimensionId] = result.GetValueOrDefault(dimensionId) + count;
        return result;
    }

    private static void SetUsageCounts(Dictionary<long, MutableRevenueBreakdown> breakdown,
        IReadOnlyDictionary<long, int> activityByDimension, IReadOnlyDictionary<long, string> names, string unassignedName)
    {
        foreach (var (id, count) in activityByDimension)
        {
            var name = id > 0 && names.TryGetValue(id, out var knownName) ? knownName : unassignedName;
            EnsureBreakdown(breakdown, id, name).ActivityCount = count;
        }
    }

    private static void AddRevenueByUsage(Dictionary<long, MutableRevenueBreakdown> breakdown,
        IReadOnlyDictionary<long, string> names,
        Dictionary<long, Dictionary<long, int>> usageBySubscription,
        long? subscriptionId,
        decimal amount,
        bool isRefund,
        string unassignedName)
    {
        if (!subscriptionId.HasValue || !usageBySubscription.TryGetValue(subscriptionId.Value, out var usage) || usage.Count == 0)
        {
            AddRevenue(breakdown, 0, unassignedName, amount, isRefund);
            return;
        }

        var weights = usage.OrderBy(row => row.Key).ToArray();
        foreach (var (dimensionId, allocatedAmount) in SplitAmount(amount, weights, row => row.Value))
        {
            var name = dimensionId.Key > 0 && names.TryGetValue(dimensionId.Key, out var knownName)
                ? knownName
                : unassignedName;
            AddRevenue(breakdown, dimensionId.Key, name, allocatedAmount, isRefund);
        }
    }

    private static IReadOnlyList<(T Item, decimal Amount)> SplitAmount<T>(
        decimal amount, IReadOnlyList<T> items, Func<T, decimal> weight)
    {
        if (items.Count == 0) return [];
        var totalWeight = items.Sum(item => Math.Max(0, weight(item)));
        if (totalWeight <= 0) return [];

        var result = new List<(T Item, decimal Amount)>(items.Count);
        var allocated = 0m;
        for (var index = 0; index < items.Count; index++)
        {
            var share = index == items.Count - 1
                ? amount - allocated
                : decimal.Floor(amount * Math.Max(0, weight(items[index])) / totalWeight * 100m) / 100m;
            allocated += share;
            result.Add((items[index], share));
        }
        return result;
    }

    private static MutableRevenueBreakdown EnsureBreakdown(
        Dictionary<long, MutableRevenueBreakdown> breakdown, long id, string name, string? code = null)
    {
        if (!breakdown.TryGetValue(id, out var row))
        {
            row = new MutableRevenueBreakdown(id, code, name);
            breakdown[id] = row;
        }
        return row;
    }

    private static void AddRevenue(Dictionary<long, MutableRevenueBreakdown> breakdown,
        long id, string name, decimal amount, bool isRefund) =>
        EnsureBreakdown(breakdown, id, name).Add(amount, isRefund);

    private static IReadOnlyList<RevenueBreakdownItem> ToBreakdownItems(
        Dictionary<long, MutableRevenueBreakdown> breakdown) => breakdown.Values
        .OrderByDescending(row => row.Gross - row.Refunds)
        .ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)
        .Select(row => new RevenueBreakdownItem(
            row.Id > 0 ? row.Id : null,
            row.Code,
            row.Name,
            row.Gross,
            row.Refunds,
            row.Gross - row.Refunds,
            row.ActivityCount))
        .ToArray();

    private sealed record AnalyticsFinancialEvent(
        long PaymentId, long InvoiceId, long MemberId, decimal Amount, DateTime OccurredAt, bool IsRefund);

    private sealed record AnalyticsInvoiceLine(
        long? PackageId, long? SubscriptionId, string Description, decimal Amount);

    private sealed class MutableRevenueBreakdown(long id, string? code, string name)
    {
        public long Id { get; } = id;
        public string? Code { get; } = code;
        public string Name { get; } = name;
        public decimal Gross { get; private set; }
        public decimal Refunds { get; private set; }
        public int ActivityCount { get; set; }

        public void Add(decimal amount, bool isRefund)
        {
            if (isRefund) Refunds += amount;
            else Gross += amount;
        }
    }

    private sealed class MutableRevenuePeriod
    {
        public decimal Gross { get; private set; }
        public decimal Refunds { get; private set; }

        public void Add(decimal amount, bool isRefund)
        {
            if (isRefund) Refunds += amount;
            else Gross += amount;
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
