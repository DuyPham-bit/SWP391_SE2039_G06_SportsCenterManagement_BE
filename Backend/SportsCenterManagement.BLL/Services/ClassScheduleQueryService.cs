using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Exceptions;
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
            throw FlowException.BadRequest("Số trang phải từ 1 đến 1.000.000.");
        if (query.PageSize is < 1 or > MaxPageSize)
            throw FlowException.BadRequest($"Kích thước trang phải từ 1 đến {MaxPageSize}.");
        if (query.SportId is <= 0 || query.CoachId is <= 0)
            throw FlowException.BadRequest("SportId và CoachId phải là số dương khi được cung cấp.");

        var fromDate = query.From ?? ScheduleTime.VietnamToday;
        if (!query.To.HasValue && fromDate == DateOnly.MaxValue)
            throw FlowException.BadRequest("Không thể tạo khoảng ngày mặc định từ ngày lớn nhất.");
        var toDate = query.To ?? fromDate.AddDays(DefaultWindowDays);
        if (toDate < fromDate) throw FlowException.BadRequest("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (toDate.DayNumber - fromDate.DayNumber > MaxWindowDays)
            throw FlowException.BadRequest($"Khoảng thời gian xem tối đa {MaxWindowDays} ngày.");

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
            sessions = sessions.Where(x => Db.ClassCoaches.Any(cc => cc.ClassId == x.cls.Id && cc.CoachId == coachId));

        var total = await sessions.CountAsync(cancellationToken);
        var page = await sessions
            .OrderBy(x => x.session.SessionDate).ThenBy(x => x.session.StartTime).ThenBy(x => x.session.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new
            {
                x.session.Id,
                ClassId = x.cls.Id,
                ClassName = x.cls.Name,
                x.cls.SportId,
                SportName = x.sport.Name,
                RoomId = x.session.RoomId ?? x.cls.RoomId,
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
                ActivePrimaryCoach = Db.ClassCoaches.Any(assignment => assignment.ClassId == x.cls.Id
                    && assignment.IsPrimary
                    && Db.CoachProfiles.Any(coach => coach.Id == assignment.CoachId
                        && coach.CenterId == x.cls.CenterId && coach.Status == "Active"
                        && Db.Users.Any(user => user.Id == coach.UserId && user.Status == "Active"
                            && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)))),
                Booked = Db.SessionBookings.Count(b => b.SessionId == x.session.Id && b.Status == FlowStatuses.BookingBooked)
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

        var items = page.Select(p =>
        {
            var capacity = p.RoomCapacity is { } rc ? Math.Min(p.Capacity, rc) : p.Capacity;
            coachByClass.TryGetValue(p.ClassId, out var coach);
            var open = p.SessionStatus == FlowStatuses.SessionScheduled && p.ClassStatus == FlowStatuses.ClassPublished;
            var available = Math.Max(0, capacity - p.Booked);
            return new SessionScheduleItemResponse(
                p.Id, p.ClassId, p.ClassName, p.SportId, p.SportName, p.RoomId,
                coach?.Id, coach?.FullName, p.SessionDate, p.StartTime, p.EndTime,
                p.SessionStatus, p.ClassStatus, capacity, p.Booked, available,
                IsBookable: open && p.RoomActive && p.ActivePrimaryCoach
                    && p.SessionDate.ToDateTime(p.StartTime) > ScheduleTime.VietnamNow
                    && (available > 0 || p.AllowWaitlist),
                AllowWaitlist: p.AllowWaitlist);
        }).ToList();

        return new PagedResult<SessionScheduleItemResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<TeachingScheduleItemResponse>> GetTeachingScheduleAsync(
        long currentUserId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
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
            ?? throw FlowException.Forbidden("Tài khoản hiện tại không phải huấn luyện viên.");

        var start = from ?? ScheduleTime.VietnamToday;
        if (!to.HasValue && start == DateOnly.MaxValue)
            throw FlowException.BadRequest("Không thể tạo khoảng ngày mặc định từ ngày lớn nhất.");
        var end = to ?? start.AddDays(DefaultWindowDays);
        if (end < start) throw FlowException.BadRequest("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.");
        if (end.DayNumber - start.DayNumber > MaxWindowDays)
            throw FlowException.BadRequest($"Khoảng thời gian xem tối đa {MaxWindowDays} ngày.");

        // UH01: no assigned class simply yields an empty list.
        var rows = await (
            from session in Db.ClassSessions
            join cls in Db.Classes on session.ClassId equals cls.Id
            join sport in Db.Sports on cls.SportId equals sport.Id
            where session.SessionDate >= start && session.SessionDate <= end
                && session.SessionStatus == FlowStatuses.SessionScheduled
                && cls.Status != FlowStatuses.ClassCancelled && cls.Status != FlowStatuses.ClassCompleted
                && cls.CenterId == coach.CenterId && sport.Status == "Active"
                && Db.Centers.Any(center => center.Id == cls.CenterId && center.Status == "Active")
                && Db.ClassCoaches.Any(cc => cc.ClassId == cls.Id && cc.CoachId == coach.Id)
            orderby session.SessionDate, session.StartTime
            select new
            {
                session.Id,
                ClassId = cls.Id,
                ClassName = cls.Name,
                SportName = sport.Name,
                RoomId = session.RoomId ?? cls.RoomId,
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
            r.SessionStatus, r.ClassStatus, r.Capacity, r.Booked)).ToList();
    }
}
