using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>UC-42 (member browses sessions) and UC-30 (coach views own teaching schedule).</summary>
public sealed class ClassScheduleQueryService(IUnitOfWork unitOfWork) : IClassScheduleQueryService
{
    private const int MaxPageSize = 100;
    private const int DefaultWindowDays = 14;
    private const int MaxWindowDays = 92;

    private SportsCenterDbContext Db => unitOfWork.Context;

    public async Task<PagedResult<SessionScheduleItemResponse>> GetSessionsAsync(
        SessionScheduleQuery query, CancellationToken cancellationToken = default)
    {
        // UH02: validate before touching the database.
        if (query.Page is < 1 or > 1_000_000)
            throw new InvalidOperationException("Số trang phải từ 1 đến 1.000.000.") { Data = { ["StatusCode"] = 400 } };
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new InvalidOperationException($"Kích thước trang phải từ 1 đến {MaxPageSize}.") { Data = { ["StatusCode"] = 400 } };
        if (query.SportId is <= 0 || query.CoachId is <= 0 || query.CenterId is <= 0)
            throw new InvalidOperationException("SportId và CoachId phải là số dương khi được cung cấp.") { Data = { ["StatusCode"] = 400 } };

        var fromDate = query.From ?? ScheduleTime.VietnamToday;
        if (!query.To.HasValue && fromDate.DayNumber > DateOnly.MaxValue.DayNumber - DefaultWindowDays)
            throw new InvalidOperationException("Không thể tạo khoảng ngày mặc định từ ngày lớn nhất.") { Data = { ["StatusCode"] = 400 } };
        var toDate = query.To ?? fromDate.AddDays(DefaultWindowDays);
        if (toDate < fromDate) throw new InvalidOperationException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.") { Data = { ["StatusCode"] = 400 } };
        if (toDate.DayNumber - fromDate.DayNumber > MaxWindowDays)
            throw new InvalidOperationException($"Khoảng thời gian xem tối đa {MaxWindowDays} ngày.") { Data = { ["StatusCode"] = 400 } };

        var sessions =
            from session in Db.ClassSessions
            join cls in Db.Classes on session.ClassId equals cls.Id
            join sport in Db.Sports on cls.SportId equals sport.Id
            where session.SessionDate >= fromDate && session.SessionDate <= toDate
                && cls.Status == FlowStatuses.ClassPublished
                && sport.Status == "Active"
                && Db.Centers.Any(center => center.Id == cls.CenterId && center.Status == "Active")
            select new { session, cls, sport };

        if (query.CenterId is { } centerId) sessions = sessions.Where(x => x.cls.CenterId == centerId);
        if (query.SportId is { } sportId) sessions = sessions.Where(x => x.cls.SportId == sportId);
        if (query.CoachId is { } coachId)
            sessions = sessions.Where(x => x.session.CoachId == coachId ||
                (x.session.CoachId == null && Db.ClassCoaches.Any(cc => cc.ClassId == x.cls.Id && cc.CoachId == coachId)));

        var total = await sessions.CountAsync(cancellationToken);
        var page = await sessions
            .OrderBy(x => x.session.SessionDate).ThenBy(x => x.session.StartTime).ThenBy(x => x.session.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new
            {
                x.session.Id,
                x.session.CoachId,
                ClassId = x.cls.Id,
                ClassName = x.cls.Name,
                x.cls.SportId,
                x.cls.CenterId,
                SportName = x.sport.Name,
                RoomId = x.session.RoomId ?? x.cls.RoomId,
                RoomName = Db.Rooms.Where(r => r.Id == (x.session.RoomId ?? x.cls.RoomId))
                    .Select(r => r.Name).FirstOrDefault(),
                x.session.SessionDate,
                x.session.StartTime,
                x.session.EndTime,
                x.session.SessionStatus,
                ClassStatus = x.cls.Status,
                x.cls.Capacity,
                x.cls.AllowWaitlist,
                RoomCapacity = (x.session.RoomId ?? x.cls.RoomId) == null
                    ? (int?)null
                    : Db.Rooms.Where(r => r.Id == (x.session.RoomId ?? x.cls.RoomId))
                        .Select(r => (int?)r.Capacity).FirstOrDefault(),
                RoomActive = (x.session.RoomId ?? x.cls.RoomId) == null
                    || Db.Rooms.Any(r => r.Id == (x.session.RoomId ?? x.cls.RoomId)
                        && r.CenterId == x.cls.CenterId && r.Status == "Active"),
                ActivePrimaryCoach = x.session.CoachId.HasValue
                    ? Db.CoachProfiles.Any(coach => coach.Id == x.session.CoachId
                        && coach.CenterId == x.cls.CenterId && coach.Status == "Active"
                        && Db.Users.Any(user => user.Id == coach.UserId && user.Status == "Active"
                            && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)))
                    : Db.ClassCoaches.Any(assignment => assignment.ClassId == x.cls.Id
                        && assignment.IsPrimary
                        && Db.CoachProfiles.Any(coach => coach.Id == assignment.CoachId
                            && coach.CenterId == x.cls.CenterId && coach.Status == "Active"
                            && Db.Users.Any(user => user.Id == coach.UserId && user.Status == "Active"
                                && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)))),
                Booked = Db.SessionBookings.Count(b => b.SessionId == x.session.Id && b.Status == FlowStatuses.BookingBooked),
                Waiting = Db.ClassWaitlists.Count(w => w.SessionId == x.session.Id && w.Status == FlowStatuses.WaitlistWaiting)
            })
            .ToListAsync(cancellationToken);

        var classIds = page.Select(p => p.ClassId).Distinct().ToList();
        var coaches = await (
            from cc in Db.ClassCoaches
            join coach in Db.CoachProfiles on cc.CoachId equals coach.Id
            join user in Db.Users on coach.UserId equals user.Id
            where classIds.Contains(cc.ClassId) && cc.IsPrimary
                && coach.Status == "Active" && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
            orderby cc.IsPrimary descending
            select new { cc.ClassId, coach.Id, coach.FullName }).ToListAsync(cancellationToken);
        var coachByClass = coaches.GroupBy(c => c.ClassId).ToDictionary(g => g.Key, g => g.First());
        var overrideCoachIds = page.Where(x => x.CoachId.HasValue).Select(x => x.CoachId!.Value).Distinct().ToArray();
        var overrideCoaches = await Db.CoachProfiles.AsNoTracking().Where(x => overrideCoachIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName }).ToDictionaryAsync(x => x.Id, cancellationToken);

        var now = ScheduleTime.VietnamNow;
        var member = await LoadMemberContextAsync(query.MemberUserId, page.Select(p => p.Id).ToList(), cancellationToken);
        var items = page.Select(p =>
        {
            var capacity = p.RoomCapacity is { } rc ? Math.Min(p.Capacity, rc) : p.Capacity;
            coachByClass.TryGetValue(p.ClassId, out var primaryCoach);
            overrideCoaches.TryGetValue(p.CoachId ?? 0, out var overrideCoach);
            var hasOverride = p.CoachId.HasValue && overrideCoach is not null;
            var effectiveCoachId = hasOverride ? overrideCoach!.Id : primaryCoach?.Id;
            var effectiveCoachName = hasOverride ? overrideCoach!.FullName : primaryCoach?.FullName;
            var open = p.SessionStatus == FlowStatuses.SessionScheduled && p.ClassStatus == FlowStatuses.ClassPublished;
            var available = Math.Max(0, capacity - p.Booked);
            var start = p.SessionDate.ToDateTime(p.StartTime);
            var openWindow = open && p.RoomActive && p.ActivePrimaryCoach
                && start - now >= TimeSpan.FromMinutes(30)
                && start - now <= TimeSpan.FromDays(7);
            var isBookable = openWindow && (available > 0 || p.AllowWaitlist);
            long? bookingId = member is not null && member.BookingBySession.TryGetValue(p.Id, out var bid) ? bid : null;
            WaitlistSpot? spot = member is not null && member.WaitlistBySession.TryGetValue(p.Id, out var ws) ? ws : null;
            var buttonState = member is null ? null
                : ResolveButtonState(member.Entitlements, bookingId.HasValue, start <= now, p.SessionDate,
                    p.CenterId, p.SportId, available, openWindow, waitlisted: spot is not null);
            return new SessionScheduleItemResponse(
                p.Id, p.ClassId, p.ClassName, p.SportId, p.SportName, p.RoomId,
                effectiveCoachId, effectiveCoachName, p.SessionDate, p.StartTime, p.EndTime,
                p.SessionStatus, p.ClassStatus, capacity, p.Booked, available,
                IsBookable: isBookable,
                AllowWaitlist: p.AllowWaitlist,
                RoomName: p.RoomName,
                ButtonState: buttonState,
                BookingId: bookingId,
                WaitlistId: spot?.Id,
                WaitlistPosition: spot?.Position,
                WaitlistCount: p.Waiting);
        }).ToList();

        return new PagedResult<SessionScheduleItemResponse>(items, query.Page, query.PageSize, total);
    }

    private sealed record WaitlistSpot(long Id, int Position);

    private sealed record MemberContext(
        IReadOnlyList<MembershipEntitlements.SubscriptionEntitlement> Entitlements,
        IReadOnlyDictionary<long, long> BookingBySession,
        IReadOnlyDictionary<long, WaitlistSpot> WaitlistBySession);

    /// <summary>Null for staff callers or users without a member profile: no ButtonState is computed.</summary>
    private async Task<MemberContext?> LoadMemberContextAsync(long? userId, IReadOnlyList<long> sessionIds, CancellationToken ct)
    {
        if (userId is not { } uid) return null;
        var memberId = await Db.MemberProfiles.Where(m => m.UserId == uid).Select(m => (long?)m.Id).FirstOrDefaultAsync(ct);
        if (memberId is not { } mid) return null;
        var entitlements = await MembershipEntitlements.LoadAsync(Db, mid, ScheduleTime.VietnamToday, ct);
        var bookings = await Db.SessionBookings
            .Where(b => b.MemberId == mid && b.Status == FlowStatuses.BookingBooked && sessionIds.Contains(b.SessionId))
            .Select(b => new { b.SessionId, b.Id }).ToListAsync(ct);
        var waiting = await Db.ClassWaitlists
            .Where(w => w.MemberId == mid && w.Status == FlowStatuses.WaitlistWaiting
                && w.SessionId != null && sessionIds.Contains(w.SessionId.Value))
            .Select(w => new
            {
                SessionId = w.SessionId!.Value,
                w.Id,
                // Same FIFO order as the promotion (JoinedAt, then Id).
                Position = Db.ClassWaitlists.Count(other => other.SessionId == w.SessionId
                    && other.Status == FlowStatuses.WaitlistWaiting
                    && (other.JoinedAt < w.JoinedAt || (other.JoinedAt == w.JoinedAt && other.Id < w.Id))) + 1
            }).ToListAsync(ct);
        return new MemberContext(entitlements, bookings.ToDictionary(b => b.SessionId, b => b.Id),
            waiting.GroupBy(w => w.SessionId)
                .ToDictionary(g => g.Key, g => new WaitlistSpot(g.First().Id, g.First().Position)));
    }

    /// <summary>Priority order: booked > past > waitlisted > no package > sport not included > full > not open > can book.</summary>
    internal static string ResolveButtonState(
        IReadOnlyList<MembershipEntitlements.SubscriptionEntitlement> entitlements, bool alreadyBooked, bool started,
        DateOnly sessionDate, long centerId, long sportId, int availableSeats, bool openForBooking, bool waitlisted = false)
    {
        if (alreadyBooked) return "ALREADY_BOOKED";
        if (started) return "EXPIRED";
        if (waitlisted) return "WAITLISTED";
        var covering = entitlements.Where(e => e.Covers(sessionDate, centerId)).ToList();
        if (covering.Count == 0) return "NO_VALID_PACKAGE";
        if (!covering.Any(e => e.AllowsSport(sportId))) return "SPORT_NOT_INCLUDED";
        if (availableSeats <= 0) return "FULL";
        return openForBooking ? "CAN_BOOK" : "NOT_OPEN";
    }

    public async Task<MemberEntitlementResponse> GetMyEntitlementsAsync(long currentUserId, CancellationToken cancellationToken = default)
    {
        var activeSportCount = await Db.Sports.CountAsync(s => s.Status == "Active", cancellationToken);
        var memberId = await Db.MemberProfiles.Where(m => m.UserId == currentUserId)
            .Select(m => (long?)m.Id).FirstOrDefaultAsync(cancellationToken);
        var today = ScheduleTime.VietnamToday;
        var entitlements = memberId is { } mid
            ? await MembershipEntitlements.LoadAsync(Db, mid, today, cancellationToken)
            : [];
        var current = entitlements.FirstOrDefault(e => e.StartDate <= today && e.EndDate >= today);
        if (current is null)
            return new MemberEntitlementResponse(false, null, null, null, 0, false, 0, 0, 0, null, null, activeSportCount);
        // Stacked renewals of the same package extend the visible end date.
        var endDate = entitlements.Where(e => e.PackageId == current.PackageId).Max(e => e.EndDate);
        return new MemberEntitlementResponse(true, current.SubscriptionId, current.PackageId, current.PackageName,
            current.TierLevel, current.IsAllAccess, current.AllowedSports, current.CancelBeforeHours,
            current.MaxFreezeDays, endDate, current.LockedToSportIds, activeSportCount);
    }

    public async Task<IReadOnlyList<TeachingScheduleItemResponse>> GetTeachingScheduleAsync(
        long currentUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default, bool includeHistory = false)
    {
        // UH02: coach identity comes only from the authenticated user.
        var coach = await (
                from profile in Db.CoachProfiles
                join user in Db.Users on profile.UserId equals user.Id
                where profile.UserId == currentUserId && profile.Status == "Active"
                    && user.Status == "Active"
                    && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
                select new { profile.Id, profile.CenterId })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Tài khoản hiện tại không phải huấn luyện viên.") { Data = { ["StatusCode"] = 403 } };

        var start = from ?? ScheduleTime.VietnamToday;
        if (!to.HasValue && start.DayNumber > DateOnly.MaxValue.DayNumber - DefaultWindowDays)
            throw new InvalidOperationException("Không thể tạo khoảng ngày mặc định từ ngày lớn nhất.") { Data = { ["StatusCode"] = 400 } };
        var end = to ?? start.AddDays(DefaultWindowDays);
        if (end < start) throw new InvalidOperationException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.") { Data = { ["StatusCode"] = 400 } };
        if (end.DayNumber - start.DayNumber > MaxWindowDays)
            throw new InvalidOperationException($"Khoảng thời gian xem tối đa {MaxWindowDays} ngày.") { Data = { ["StatusCode"] = 400 } };

        // UH01: no assigned class simply yields an empty list.
        var rows = await (
            from session in Db.ClassSessions
            join cls in Db.Classes on session.ClassId equals cls.Id
            join sport in Db.Sports on cls.SportId equals sport.Id
            where session.SessionDate >= start && session.SessionDate <= end
                && (includeHistory || session.SessionStatus == FlowStatuses.SessionScheduled)
                && (includeHistory || (cls.Status != FlowStatuses.ClassCancelled && cls.Status != FlowStatuses.ClassCompleted))
                && cls.CenterId == coach.CenterId && sport.Status == "Active"
                && Db.Centers.Any(center => center.Id == cls.CenterId && center.Status == "Active")
                && (session.CoachId == coach.Id || (session.CoachId == null &&
                    Db.ClassCoaches.Any(cc => cc.ClassId == cls.Id && cc.CoachId == coach.Id)))
            orderby session.SessionDate, session.StartTime
            select new
            {
                session.Id,
                ClassId = cls.Id,
                ClassName = cls.Name,
                SportName = sport.Name,
                RoomId = session.RoomId ?? cls.RoomId,
                RoomName = Db.Rooms.Where(r => r.Id == (session.RoomId ?? cls.RoomId)).Select(r => r.Name).FirstOrDefault(),
                session.SessionDate,
                session.StartTime,
                session.EndTime,
                session.SessionStatus,
                ClassStatus = cls.Status,
                cls.Capacity,
                Booked = Db.SessionBookings.Count(b => b.SessionId == session.Id && b.Status == FlowStatuses.BookingBooked)
            }).ToListAsync(cancellationToken);

        return rows.Select(r => new TeachingScheduleItemResponse(
            r.Id, r.ClassId, r.ClassName, r.SportName, r.RoomId, r.SessionDate, r.StartTime, r.EndTime,
            r.SessionStatus, r.ClassStatus, r.Capacity, r.Booked, r.RoomName)).ToList();
    }
}
