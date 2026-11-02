using System.Globalization;
using System.Data;
using Dapper;
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

    public async Task<SportRevenueReportResponse> GetSportRevenueReportAsync(
        long centerId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        ValidateRequest(from, to, "day");
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        var (startUtc, endUtc) = GetUtcRange(from, to, GetReportTimeZone());
        var payments = await (
            from payment in _unitOfWork.Context.Payments.AsNoTracking()
            join invoice in _unitOfWork.Context.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && payment.PaymentStatus == "Succeeded"
                && payment.PaidAt != null && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select new { payment.Id, payment.InvoiceId, payment.Amount }).ToListAsync(cancellationToken);
        var refundRows = await (
            from refund in _unitOfWork.Context.PaymentRefunds.AsNoTracking()
            join payment in _unitOfWork.Context.Payments.AsNoTracking() on refund.PaymentId equals payment.Id
            join invoice in _unitOfWork.Context.Invoices.AsNoTracking() on payment.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && (refund.Status == "Succeeded" || refund.Status == "Approved")
                && refund.ProcessedAt != null && refund.ProcessedAt >= startUtc && refund.ProcessedAt < endUtc
            select new { payment.InvoiceId, refund.Amount }).ToListAsync(cancellationToken);
        var invoiceIds = payments.Select(item => item.InvoiceId)
            .Concat(refundRows.Select(item => item.InvoiceId)).Distinct().ToArray();
        var invoiceItems = await _unitOfWork.Context.InvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .Select(item => new { item.InvoiceId, item.SubscriptionId, item.EnrollmentId })
            .ToListAsync(cancellationToken);
        var subscriptionIds = invoiceItems.Where(item => item.SubscriptionId.HasValue)
            .Select(item => item.SubscriptionId!.Value).Distinct().ToArray();
        var enrollmentIds = invoiceItems.Where(item => item.EnrollmentId.HasValue)
            .Select(item => item.EnrollmentId!.Value).Distinct().ToArray();
        var enrollmentSports = await (
            from enrollment in _unitOfWork.Context.ClassEnrollments.AsNoTracking()
            join cls in _unitOfWork.Context.Classes.AsNoTracking() on enrollment.ClassId equals cls.Id
            join sport in _unitOfWork.Context.Sports.AsNoTracking() on cls.SportId equals sport.Id
            where cls.CenterId == centerId &&
                (enrollmentIds.Contains(enrollment.Id)
                 || (enrollment.SubscriptionId.HasValue && subscriptionIds.Contains(enrollment.SubscriptionId.Value)))
            select new { enrollment.Id, enrollment.SubscriptionId, SportId = sport.Id, SportName = sport.Name })
            .ToListAsync(cancellationToken);
        var bookingSports = await (
            from booking in _unitOfWork.Context.SessionBookings.AsNoTracking()
            join session in _unitOfWork.Context.ClassSessions on booking.SessionId equals session.Id
            join cls in _unitOfWork.Context.Classes on session.ClassId equals cls.Id
            join sport in _unitOfWork.Context.Sports on cls.SportId equals sport.Id
            where cls.CenterId == centerId && booking.SubscriptionId.HasValue
                && subscriptionIds.Contains(booking.SubscriptionId.Value)
                && (booking.Status == "Booked" || booking.Status == "CancelledLate")
            select new { booking.SubscriptionId, SportId = sport.Id, SportName = sport.Name })
            .Distinct().ToListAsync(cancellationToken);

        var buckets = new Dictionary<(long? Id, string Name), (decimal Gross, decimal Refunds)>();
        (long? Id, string Name)[] Targets(long invoiceId)
        {
            var items = invoiceItems.Where(item => item.InvoiceId == invoiceId).ToArray();
            var directIds = items.Where(item => item.EnrollmentId.HasValue).Select(item => item.EnrollmentId!.Value).ToHashSet();
            var subs = items.Where(item => item.SubscriptionId.HasValue).Select(item => item.SubscriptionId!.Value).ToHashSet();
            var sports = enrollmentSports.Where(item => directIds.Contains(item.Id)
                    || (item.SubscriptionId.HasValue && subs.Contains(item.SubscriptionId.Value)))
                .Select(item => (item.SportId, item.SportName))
                .Concat(bookingSports.Where(item => subs.Contains(item.SubscriptionId!.Value))
                    .Select(item => (item.SportId, item.SportName))).Distinct().ToArray();
            return sports.Length > 0 ? sports.Select(item => ((long?)item.SportId, item.SportName)).ToArray()
                : new[] { ((long?)null, "Chưa phân bổ") };
        }
        foreach (var payment in payments)
        {
            var targets = Targets(payment.InvoiceId);
            foreach (var target in targets)
            {
                var current = buckets.GetValueOrDefault(target);
                buckets[target] = (current.Gross + payment.Amount / targets.Length, current.Refunds);
            }
        }
        foreach (var refund in refundRows)
        {
            var targets = Targets(refund.InvoiceId);
            foreach (var target in targets)
            {
                var current = buckets.GetValueOrDefault(target);
                buckets[target] = (current.Gross, current.Refunds + refund.Amount / targets.Length);
            }
        }

        var totalNet = payments.Sum(item => item.Amount) - refundRows.Sum(item => item.Amount);
        var rows = buckets.Select(item => new SportRevenueItem(item.Key.Id, item.Key.Name,
                decimal.Round(item.Value.Gross, 2), decimal.Round(item.Value.Refunds, 2),
                decimal.Round(item.Value.Gross - item.Value.Refunds, 2),
                totalNet > 0 ? decimal.Round((item.Value.Gross - item.Value.Refunds) / totalNet * 100, 2) : 0))
            .OrderByDescending(item => item.Net).ThenBy(item => item.SportName).ToArray();
        var unallocated = rows.Where(item => item.SportId is null).Sum(item => item.Net);
        return new SportRevenueReportResponse(centerId, from, to, totalNet,
            rows.Where(item => item.SportId is not null).Sum(item => item.Net), unallocated, rows);
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
        var db = _unitOfWork.Context;

        var centerMembers = await db.MemberProfiles.AsNoTracking()
            .Where(profile => profile.CenterId == centerId)
            .Select(profile => new { profile.Id, profile.CreatedAt })
            .ToListAsync(cancellationToken);
        var totalMembers = centerMembers.Count;

        var packages = await db.MembershipPackages.AsNoTracking()
            .Where(p => p.CenterId == centerId)
            .Select(p => new { p.Id, p.Name, p.Price, p.DurationDays })
            .ToListAsync(cancellationToken);

        var subs = await (
            from subscription in db.MemberSubscriptions.AsNoTracking()
            join package in db.MembershipPackages.AsNoTracking() on subscription.PackageId equals package.Id
            where package.CenterId == centerId
            select new {
                subscription.Id,
                subscription.MemberId,
                subscription.PackageId,
                subscription.StartDate,
                subscription.EndDate,
                subscription.Price,
                subscription.Status,
                subscription.CreatedAt
            }).ToListAsync(cancellationToken);

        var activeMemberIds = subs
            .Where(s => s.Status == "Active" && s.StartDate <= today && s.EndDate >= today)
            .Select(s => s.MemberId).ToHashSet();
        var activeMembers = activeMemberIds.Count;

        var expiredMembers = subs.Where(s => s.Status == "Expired" ||
                (s.Status == "Active" && s.EndDate < today))
            .Select(s => s.MemberId).Distinct().Count(m => !activeMemberIds.Contains(m));
        var inactiveMembers = Math.Max(0, totalMembers - activeMembers);

        // Sales are recognized on full payment; revenue follows actual collections and refunds.
        var sales = await (from item in db.InvoiceItems.AsNoTracking()
            join invoice in db.Invoices on item.InvoiceId equals invoice.Id
            where invoice.CenterId == centerId && item.PackageId.HasValue && item.SubscriptionId.HasValue
                && invoice.PaidAt >= startUtc && invoice.PaidAt < endUtc
            select new { PackageId = item.PackageId!.Value, item.SubscriptionId }).Distinct().ToListAsync(cancellationToken);
        var collected = await (from payment in db.Payments.AsNoTracking()
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            join item in db.InvoiceItems on invoice.Id equals item.InvoiceId
            where invoice.CenterId == centerId && item.PackageId.HasValue && invoice.Subtotal > 0
                && payment.PaymentStatus == "Succeeded" && payment.PaidAt >= startUtc && payment.PaidAt < endUtc
            select new { PackageId = item.PackageId!.Value, Amount = payment.Amount * item.Amount / invoice.Subtotal })
            .ToListAsync(cancellationToken);
        var refunded = await (from refund in db.PaymentRefunds.AsNoTracking()
            join payment in db.Payments on refund.PaymentId equals payment.Id
            join invoice in db.Invoices on payment.InvoiceId equals invoice.Id
            join item in db.InvoiceItems on invoice.Id equals item.InvoiceId
            where invoice.CenterId == centerId && item.PackageId.HasValue && invoice.Subtotal > 0
                && (refund.Status == "Succeeded" || refund.Status == "Approved") && refund.ProcessedAt >= startUtc && refund.ProcessedAt < endUtc
            select new { PackageId = item.PackageId!.Value, Amount = refund.Amount * item.Amount / invoice.Subtotal })
            .ToListAsync(cancellationToken);
        var packageStats = packages.Select(pkg =>
        {
            var pkgSubs = subs.Where(s => s.PackageId == pkg.Id).ToArray();
            var activeSubscribers = pkgSubs.Count(s => s.Status == "Active" && s.StartDate <= today && s.EndDate >= today);
            var soldInPeriod = sales.Count(s => s.PackageId == pkg.Id);
            var revenueInPeriod = decimal.Round(collected.Where(p => p.PackageId == pkg.Id).Sum(p => p.Amount)
                - refunded.Where(p => p.PackageId == pkg.Id).Sum(p => p.Amount), 2);
            return new MembershipPackageStatsItem(pkg.Id, pkg.Name, pkg.Price, pkg.DurationDays, activeSubscribers, soldInPeriod, revenueInPeriod);
        }).OrderByDescending(p => p.SoldInPeriod).ThenByDescending(p => p.RevenueInPeriod).ToArray();

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
            newMemberRows.Length, periods, expiredMembers, inactiveMembers, packageStats,
            activeMembers, inactiveMembers);
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
        var bookingCounts = await (
            from booking in _unitOfWork.Context.SessionBookings
            join session in _unitOfWork.Context.ClassSessions on booking.SessionId equals session.Id
            join cls in _unitOfWork.Context.Classes on session.ClassId equals cls.Id
            where cls.CenterId == centerId
            group booking by booking.Status into statusGroup
            select new { Status = statusGroup.Key, Count = statusGroup.Count() }).ToListAsync(cancellationToken);

        var currentStatusCounts = enrollmentCounts.Concat(waitlistCounts).Concat(bookingCounts)
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
        var bookingEvents = await (
            from booking in _unitOfWork.Context.SessionBookings
            join session in _unitOfWork.Context.ClassSessions on booking.SessionId equals session.Id
            join cls in _unitOfWork.Context.Classes on session.ClassId equals cls.Id
            where cls.CenterId == centerId && booking.BookedAt >= startUtc && booking.BookedAt < endUtc
            select new { booking.Status, CreatedAtUtc = booking.BookedAt }).ToListAsync(cancellationToken);
        var statusEvents = enrollmentEvents
            .Select(row => new StatusEvent(row.Status, row.CreatedAtUtc))
            .Concat(waitlistEvents.Select(row => new StatusEvent(row.Status, row.CreatedAtUtc)))
            .Concat(bookingEvents.Select(row => new StatusEvent(row.Status, row.CreatedAtUtc)))
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

    public async Task<OperationsReportResponse> GetOperationsReportAsync(
        long centerId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(from, to, "day");
        await EnsureCenterExistsAsync(centerId, cancellationToken);
        var database = _unitOfWork.Context.Database;
        var connection = database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;

        if (openedHere)
        {
            await database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            // A single batch keeps related dashboard data on one connection and eliminates N+1-style round trips.
            var parameters = new DynamicParameters();
            parameters.Add("CenterId", centerId, DbType.Int64);
            parameters.Add("FromDate", from.ToDateTime(TimeOnly.MinValue), DbType.Date);
            parameters.Add("ToDate", to.ToDateTime(TimeOnly.MinValue), DbType.Date);
            var command = new CommandDefinition(
                OperationsReportSql,
                parameters,
                cancellationToken: cancellationToken,
                flags: CommandFlags.Buffered);

            using var reader = await connection.QueryMultipleAsync(command);
            var personnel = (await reader.ReadAsync<PersonnelRow>()).AsList();
            var summary = await reader.ReadSingleAsync<OperationsSummaryRow>();
            var sessionRows = (await reader.ReadAsync<OperationalSessionRow>()).AsList();
            var coaches = (await reader.ReadAsync<OperationalCoachSourceRow>()).AsList();

            var isActive = (PersonnelRow row) =>
                row.ProfileStatus.Equals("Active", StringComparison.OrdinalIgnoreCase)
                && row.UserStatus.Equals("Active", StringComparison.OrdinalIgnoreCase);

            static decimal Rate(int booked, int capacity) => capacity > 0
                ? decimal.Round((decimal)booked / capacity * 100, 2) : 0;
            var totalBooked = sessionRows.Sum(row => row.Booked);
            var totalCapacity = sessionRows.Sum(row => row.Capacity);

            var classRows = sessionRows.GroupBy(row => new
                { row.ClassId, row.ClassName, row.SportName, row.CoachName, row.RoomName })
                .Select(group => new OperationalClassItem(
                    group.Key.ClassId, group.Key.ClassName, group.Key.SportName, group.Key.CoachName, group.Key.RoomName,
                    group.Count(), group.Sum(row => row.Booked), group.Sum(row => row.Capacity),
                    Rate(group.Sum(row => row.Booked), group.Sum(row => row.Capacity))))
                .OrderByDescending(row => row.Booked).ThenByDescending(row => row.OccupancyRate).ThenBy(row => row.Name)
                .ToArray();

            var sportRows = sessionRows.GroupBy(row => new { row.SportId, row.SportName })
                .Select(group => new OperationalSportItem(
                    group.Key.SportId, group.Key.SportName, group.Count(), group.Select(row => row.ClassId).Distinct().Count(),
                    group.Sum(row => row.Booked), group.Sum(row => row.Capacity),
                    Rate(group.Sum(row => row.Booked), group.Sum(row => row.Capacity)),
                    totalBooked > 0 ? decimal.Round((decimal)group.Sum(row => row.Booked) / totalBooked * 100, 2) : 0))
                .OrderByDescending(row => row.Booked).ThenByDescending(row => row.OccupancyRate).ThenBy(row => row.Name)
                .ToArray();

            var roomRows = sessionRows.Where(row => row.RoomId.HasValue)
                .GroupBy(row => new { Id = row.RoomId!.Value, row.RoomName, row.RoomType, row.RoomLocation, row.RoomCapacity })
                .Select(group => new OperationalRoomItem(
                    group.Key.Id, group.Key.RoomName!, group.Key.RoomType, group.Key.RoomLocation, group.Key.RoomCapacity,
                    group.Count(), group.Select(row => row.ClassId).Distinct().Count(), group.Sum(row => row.Booked),
                    group.Sum(row => row.Capacity), Rate(group.Sum(row => row.Booked), group.Sum(row => row.Capacity)),
                    group.Select(row => row.ClassName).Distinct().OrderBy(name => name).ToArray()))
                .OrderByDescending(row => row.Booked).ThenByDescending(row => row.OccupancyRate).ThenBy(row => row.Name)
                .ToArray();

            var sessionsByCoach = sessionRows
                .Where(session => session.CoachId.HasValue)
                .GroupBy(session => session.CoachId!.Value)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var coachItems = coaches.Select(coach =>
            {
                var coachSessions = sessionsByCoach.GetValueOrDefault(coach.Id) ?? [];
                var booked = coachSessions.Sum(session => session.Booked);
                var capacity = coachSessions.Sum(session => session.Capacity);
                return new OperationalCoachItem(
                    coach.Id,
                    coach.FullName,
                    coach.CoachCode,
                    coach.Specialization,
                    coach.Status,
                    coachSessions.Select(session => session.ClassId).Distinct().Count(),
                    coachSessions.Length,
                    booked,
                    capacity,
                    Rate(booked, capacity));
            }).OrderByDescending(coach => coach.Booked).ThenByDescending(coach => coach.SessionsCount).ToArray();

            return new OperationsReportResponse(
                centerId, from, to,
                personnel.Count,
                personnel.Count(isActive),
                personnel.Count(row => row.Role.Equals("Manager", StringComparison.OrdinalIgnoreCase)),
                personnel.Count(row => row.Role.Equals("Coach", StringComparison.OrdinalIgnoreCase)),
                personnel.Count(row => row.Role.Equals("Receptionist", StringComparison.OrdinalIgnoreCase)),
                summary.TotalClasses, summary.ActiveClasses,
                summary.TotalRooms, summary.ActiveRooms,
                sessionRows.Count, totalBooked, totalCapacity, Rate(totalBooked, totalCapacity),
                classRows, sportRows, roomRows, coachItems);
        }
        finally
        {
            if (openedHere)
            {
                await database.CloseConnectionAsync();
            }
        }
    }

    private const string OperationsReportSql = """
        SELECT DISTINCT personnel.UserId, personnel.Role, personnel.ProfileStatus, personnel.UserStatus
        FROM (
            SELECT u.id AS UserId, r.name AS Role, sp.status AS ProfileStatus, u.status AS UserStatus
            FROM dbo.staff_profiles AS sp
            INNER JOIN dbo.users AS u ON u.id = sp.user_id
            INNER JOIN dbo.roles AS r ON r.id = u.role_id
            WHERE sp.center_id = @CenterId

            UNION ALL

            SELECT u.id AS UserId, r.name AS Role, cp.status AS ProfileStatus, u.status AS UserStatus
            FROM dbo.coach_profiles AS cp
            INNER JOIN dbo.users AS u ON u.id = cp.user_id
            INNER JOIN dbo.roles AS r ON r.id = u.role_id
            WHERE cp.center_id = @CenterId
        ) AS personnel;

        SELECT
            (SELECT COUNT(1) FROM dbo.classes WHERE center_id = @CenterId) AS TotalClasses,
            (SELECT COUNT(1) FROM dbo.classes WHERE center_id = @CenterId
                AND status NOT IN (N'Cancelled', N'Completed')) AS ActiveClasses,
            (SELECT COUNT(1) FROM dbo.rooms WHERE center_id = @CenterId) AS TotalRooms,
            (SELECT COUNT(1) FROM dbo.rooms WHERE center_id = @CenterId AND status = N'Active') AS ActiveRooms;

        WITH booked AS (
            SELECT session_id, COUNT(1) AS Booked
            FROM dbo.session_bookings
            WHERE status = N'Booked'
            GROUP BY session_id
        )
        SELECT
            session_row.id AS SessionId,
            class_row.id AS ClassId,
            class_row.name AS ClassName,
            sport.id AS SportId,
            sport.name AS SportName,
            room.id AS RoomId,
            room.name AS RoomName,
            room.room_type AS RoomType,
            room.location_description AS RoomLocation,
            ISNULL(room.capacity, 0) AS RoomCapacity,
            COALESCE(session_row.coach_id, primary_coach.Id) AS CoachId,
            COALESCE(session_coach.full_name, primary_coach.FullName, N'Chưa có Coach') AS CoachName,
            class_row.capacity AS Capacity,
            ISNULL(booked.Booked, 0) AS Booked
        FROM dbo.class_sessions AS session_row
        INNER JOIN dbo.classes AS class_row ON class_row.id = session_row.class_id
        INNER JOIN dbo.sports AS sport ON sport.id = class_row.sport_id
        LEFT JOIN dbo.rooms AS room ON room.id = COALESCE(session_row.room_id, class_row.room_id)
        OUTER APPLY (
            SELECT TOP (1) coach.id AS Id, coach.full_name AS FullName
            FROM dbo.class_coaches AS assignment
            INNER JOIN dbo.coach_profiles AS coach ON coach.id = assignment.coach_id
            WHERE assignment.class_id = class_row.id AND assignment.is_primary = 1
            ORDER BY assignment.coach_id
        ) AS primary_coach
        LEFT JOIN dbo.coach_profiles AS session_coach ON session_coach.id = session_row.coach_id
        LEFT JOIN booked ON booked.session_id = session_row.id
        WHERE class_row.center_id = @CenterId
          AND session_row.session_date >= @FromDate
          AND session_row.session_date <= @ToDate
          AND session_row.session_status <> N'Cancelled';

        SELECT id AS Id, full_name AS FullName, coach_code AS CoachCode,
               specialization AS Specialization, status AS Status
        FROM dbo.coach_profiles
        WHERE center_id = @CenterId;
        """;

    private sealed record PersonnelRow(long UserId, string Role, string ProfileStatus, string UserStatus);

    private sealed record OperationsSummaryRow(int TotalClasses, int ActiveClasses, int TotalRooms, int ActiveRooms);

    private sealed record OperationalSessionRow(
        long SessionId,
        long ClassId,
        string ClassName,
        long SportId,
        string SportName,
        long? RoomId,
        string? RoomName,
        string? RoomType,
        string? RoomLocation,
        int RoomCapacity,
        long? CoachId,
        string CoachName,
        int Capacity,
        int Booked);

    private sealed record OperationalCoachSourceRow(
        long Id,
        string FullName,
        string? CoachCode,
        string? Specialization,
        string Status);

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
        if (from > to || to == DateOnly.MaxValue || to.DayNumber - from.DayNumber > 365)
        {
            throw BusinessException.BadRequest("Khoảng ngày phải hợp lệ và không vượt quá 366 ngày.");
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
