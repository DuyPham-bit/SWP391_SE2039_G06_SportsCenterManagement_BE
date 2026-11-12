using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Purchase / renew / upgrade rules driven by package params (TierLevel, IsAllAccess, AllowedSports).
/// Invariant: at most one non-stacked Active package per member — an upgrade retires the lower tier.
/// </summary>
internal static class MembershipEntitlements
{
    public const string StatusUpgraded = "Upgraded";

    public sealed record CurrentPackage(MemberSubscription Subscription, MembershipPackage Package);

    /// <summary>Active subscription covering <paramref name="today"/>, highest tier first.</summary>
    public static Task<CurrentPackage?> GetCurrentAsync(SportsCenterDbContext db, long memberId, DateOnly today, CancellationToken ct) =>
        (from subscription in db.MemberSubscriptions
         join package in db.MembershipPackages on subscription.PackageId equals package.Id
         where subscription.MemberId == memberId && subscription.Status == "Active"
             && subscription.StartDate <= today && subscription.EndDate >= today
         orderby package.TierLevel descending, subscription.EndDate descending
         select new CurrentPackage(subscription, package)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Validates the purchase mode and returns the sport ids to store on the new subscription.
    /// Renewal inherits the current selection; a null request keeps the legacy implicit rule (no rows).
    /// </summary>
    public static async Task<IReadOnlyList<long>> ResolvePurchaseSportsAsync(
        SportsCenterDbContext db, long memberId, MembershipPackage package,
        IReadOnlyCollection<long>? requestedSportIds, DateOnly today, CancellationToken ct)
    {
        var current = await GetCurrentAsync(db, memberId, today, ct);
        if (current is not null)
        {
            if (current.Package.TierLevel > package.TierLevel)
                throw BusinessException.Conflict("Không thể chuyển sang gói thấp hơn khi gói cao cấp hiện tại vẫn còn hiệu lực.");
            if (current.Subscription.PackageId == package.Id)
                return await db.SubscriptionSports
                    .Where(row => row.SubscriptionId == current.Subscription.Id)
                    .Select(row => row.SportId).ToListAsync(ct);
        }

        if (package.IsAllAccess || requestedSportIds is null || requestedSportIds.Count == 0) return [];

        var ids = requestedSportIds.Distinct().ToList();
        if (ids.Count != package.AllowedSports)
            throw BusinessException.BadRequest($"Gói {package.Name} yêu cầu chọn đúng {package.AllowedSports} môn.");
        var validCount = await db.Sports.CountAsync(sport => ids.Contains(sport.Id) && sport.Status == "Active"
            && (!sport.CenterId.HasValue || sport.CenterId.Value == package.CenterId), ct);
        if (validCount != ids.Count)
            throw BusinessException.BadRequest("Có môn đã chọn không tồn tại hoặc đã ngừng hoạt động.");
        return ids;
    }

    /// <summary>What one running subscription lets the member book.</summary>
    public sealed record SubscriptionEntitlement(
        long SubscriptionId, long PackageId, string PackageName, long CenterId, int TierLevel, bool IsAllAccess,
        int AllowedSports, int CancelBeforeHours, int MaxFreezeDays, DateOnly StartDate, DateOnly EndDate,
        IReadOnlySet<long> SelectedSportIds, IReadOnlySet<long> UsedSportIds)
    {
        public bool Covers(DateOnly date, long centerId) => StartDate <= date && EndDate >= date && CenterId == centerId;

        /// <summary>Explicit selection wins; subscriptions bought before selection existed keep the implicit rule.</summary>
        public bool AllowsSport(long sportId) => IsAllAccess
            || (SelectedSportIds.Count > 0
                ? SelectedSportIds.Contains(sportId)
                : UsedSportIds.Contains(sportId) || UsedSportIds.Count < AllowedSports);

        /// <summary>
        /// Sports the member registered under this package: explicit selection, else (older subscriptions)
        /// the sports already enrolled/booked. Null = all-access or nothing registered yet (show everything).
        /// </summary>
        public IReadOnlyList<long>? LockedToSportIds => IsAllAccess ? null
            : SelectedSportIds.Count > 0 ? SelectedSportIds.ToList()
            : UsedSportIds.Count > 0 ? UsedSportIds.ToList() : null;
    }

    /// <summary>Loads every Active subscription that has not ended before <paramref name="fromDate"/>.</summary>
    public static async Task<IReadOnlyList<SubscriptionEntitlement>> LoadAsync(
        SportsCenterDbContext db, long memberId, DateOnly fromDate, CancellationToken ct)
    {
        var rows = await (
            from subscription in db.MemberSubscriptions
            join package in db.MembershipPackages on subscription.PackageId equals package.Id
            where subscription.MemberId == memberId && subscription.Status == "Active"
                && subscription.StartDate != null && subscription.EndDate != null && subscription.EndDate >= fromDate
            select new
            {
                subscription.Id, subscription.PackageId, package.Name, package.CenterId, package.TierLevel,
                package.IsAllAccess, package.AllowedSports, package.CancelBeforeHours, package.MaxFreezeDays,
                StartDate = subscription.StartDate!.Value, EndDate = subscription.EndDate!.Value
            }).ToListAsync(ct);
        if (rows.Count == 0) return [];

        var ids = rows.Select(row => row.Id).ToList();
        var selected = await db.SubscriptionSports.Where(row => ids.Contains(row.SubscriptionId))
            .Select(row => new { row.SubscriptionId, row.SportId }).ToListAsync(ct);
        // Same sources SessionBookingService uses for the implicit rule.
        var used = await (
            from booking in db.SessionBookings
            join session in db.ClassSessions on booking.SessionId equals session.Id
            join cls in db.Classes on session.ClassId equals cls.Id
            where booking.SubscriptionId != null && ids.Contains(booking.SubscriptionId.Value)
                && (booking.Status == FlowStatuses.BookingBooked || booking.Status == FlowStatuses.BookingCancelledLate)
            select new { SubscriptionId = booking.SubscriptionId!.Value, cls.SportId })
            .Concat(
                from enrollment in db.ClassEnrollments
                join cls in db.Classes on enrollment.ClassId equals cls.Id
                where enrollment.SubscriptionId != null && ids.Contains(enrollment.SubscriptionId.Value)
                    && enrollment.Status == FlowStatuses.EnrollmentConfirmed
                select new { SubscriptionId = enrollment.SubscriptionId!.Value, cls.SportId })
            .ToListAsync(ct);

        return rows.Select(row => new SubscriptionEntitlement(
                row.Id, row.PackageId, row.Name, row.CenterId, row.TierLevel, row.IsAllAccess, row.AllowedSports,
                row.CancelBeforeHours, row.MaxFreezeDays, row.StartDate, row.EndDate,
                selected.Where(s => s.SubscriptionId == row.Id).Select(s => s.SportId).ToHashSet(),
                used.Where(u => u.SubscriptionId == row.Id).Select(u => u.SportId).ToHashSet()))
            .OrderByDescending(row => row.TierLevel).ThenByDescending(row => row.EndDate)
            .ToList();
    }

    public static void AddSports(SportsCenterDbContext db, long subscriptionId, IEnumerable<long> sportIds)
    {
        foreach (var sportId in sportIds)
            db.SubscriptionSports.Add(new SubscriptionSport { SubscriptionId = subscriptionId, SportId = sportId });
    }

    /// <summary>
    /// Renewal of the same package stacks after its current end date. A new / higher package starts today
    /// and retires every other running package of the member (Status = Upgraded).
    /// </summary>
    public static async Task ActivateAsync(SportsCenterDbContext db, MemberSubscription subscription,
        MembershipPackage package, DateOnly today, DateTime now, CancellationToken ct)
    {
        var currentEnd = await db.MemberSubscriptions
            .Where(candidate => candidate.MemberId == subscription.MemberId && candidate.Id != subscription.Id
                && candidate.PackageId == subscription.PackageId
                && candidate.Status == "Active" && candidate.EndDate >= today)
            .MaxAsync(candidate => (DateOnly?)candidate.EndDate, ct);
        var durationDays = subscription.DurationDays > 0 ? subscription.DurationDays : package.DurationDays;
        subscription.StartDate = currentEnd.HasValue ? currentEnd.Value.AddDays(1) : today;
        subscription.EndDate = subscription.StartDate.Value.AddDays(durationDays - 1);
        subscription.DurationDays = durationDays;
        subscription.Status = "Active";
        subscription.UpdatedAt = now;
        if (currentEnd.HasValue) return;

        var replaced = await (
            from other in db.MemberSubscriptions
            join otherPackage in db.MembershipPackages on other.PackageId equals otherPackage.Id
            where other.MemberId == subscription.MemberId && other.Id != subscription.Id
                && other.PackageId != subscription.PackageId
                && other.Status == "Active" && other.EndDate >= today
                && otherPackage.TierLevel <= package.TierLevel
            select other).ToListAsync(ct);
        foreach (var other in replaced)
        {
            other.Status = StatusUpgraded;
            other.UpdatedAt = now;
        }
    }
}
