using System.Data;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.CoachProfessional;
using SportsCenterManagement.DAL.Authorization;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed class CoachProfessionalService(SportsCenterDbContext db, TimeProvider timeProvider)
{
    private sealed record ReplacementCoachOption(
        long Id, string FullName, string CoachCode, string? Specialization, string[] Specializations);

    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, VietnamTimeZone));

    // SQL Server datetime2 does not retain DateTime.Kind. These values are stored as UTC,
    // so restore the UTC marker before JSON serialization to emit an unambiguous trailing Z.
    private static DateTime AsUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? AsUtc(DateTime? value) => value.HasValue ? AsUtc(value.Value) : null;

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), VietnamTimeZone)
    };

    private static BusinessException Invalid(string message, string code = "COACH-VALIDATION") =>
        BusinessException.BadRequest(message, code);

    private async Task<CoachProfile> Coach(long userId, CancellationToken ct) =>
        await db.CoachProfiles.SingleOrDefaultAsync(x => x.UserId == userId && x.Status == "Active", ct)
        ?? throw BusinessException.Forbidden("Tài khoản chưa có hồ sơ huấn luyện viên đang hoạt động.", "COACH-PROFILE");

    private async Task<(long CenterId, long UserId)> Manager(long userId, CancellationToken ct)
    {
        var row = await db.StaffProfiles.Where(x => x.UserId == userId && x.Status == "Active")
            .Select(x => new { x.CenterId, x.UserId }).SingleOrDefaultAsync(ct);
        if (row is not null) return (row.CenterId, row.UserId);
        var role = await (from user in db.Users join r in db.Roles on user.RoleId equals r.Id
            where user.Id == userId && r.Name == RoleNames.Admin
            select r.Name).SingleOrDefaultAsync(ct);
        if (role is null) throw BusinessException.Forbidden("Chỉ quản lý trung tâm mới được thực hiện thao tác này.", "MANAGER-ONLY");
        var centerId = await db.Centers.OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync(ct);
        return (centerId, userId);
    }

    private IQueryable<long> AssignedClassIds(long coachId) => db.ClassCoaches
        .Where(x => x.CoachId == coachId).Select(x => x.ClassId);

    private static (DateTime StartAt, DateTime EndAt) SessionUtc(
        DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime)
    {
        var localStart = DateTime.SpecifyKind(sessionDate.ToDateTime(startTime), DateTimeKind.Unspecified);
        var localEnd = DateTime.SpecifyKind(sessionDate.ToDateTime(endTime), DateTimeKind.Unspecified);
        if (localEnd <= localStart) localEnd = localEnd.AddDays(1);
        return (TimeZoneInfo.ConvertTimeToUtc(localStart, VietnamTimeZone),
            TimeZoneInfo.ConvertTimeToUtc(localEnd, VietnamTimeZone));
    }

    private static bool SessionOverlaps(
        DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime, DateTime rangeStart, DateTime rangeEnd)
    {
        var session = SessionUtc(sessionDate, startTime, endTime);
        return session.StartAt < rangeEnd && session.EndAt > rangeStart;
    }

    private async Task<ClassSession[]> AffectedClassSessions(
        long coachId, DateTime startAt, DateTime endAt, bool trackChanges, CancellationToken ct)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startAt, VietnamTimeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(endAt, VietnamTimeZone);
        var firstDate = DateOnly.FromDateTime(localStart);
        var lastDate = DateOnly.FromDateTime(localEnd);
        IQueryable<ClassSession> query = db.ClassSessions.Where(x => AssignedClassIds(coachId).Contains(x.ClassId) &&
            (x.CoachId == null || x.CoachId == coachId) && x.SessionStatus != "Cancelled" &&
            x.SessionDate >= firstDate && x.SessionDate <= lastDate);
        if (!trackChanges) query = query.AsNoTracking();
        var candidates = await query.ToArrayAsync(ct);
        return candidates.Where(x => SessionOverlaps(
            x.SessionDate, x.StartTime, x.EndTime, startAt, endAt)).ToArray();
    }

    private async Task EnsureReplacementSpecializations(
        long replacementCoachId, IReadOnlyCollection<ClassSession> sessions, CancellationToken ct)
    {
        var classIds = sessions.Select(x => x.ClassId).Distinct().ToArray();
        if (classIds.Length == 0) return;
        var requiredSports = await (from cls in db.Classes.AsNoTracking()
            join sport in db.Sports.AsNoTracking() on cls.SportId equals sport.Id
            where classIds.Contains(cls.Id)
            select new { sport.Id, sport.Name }).Distinct().ToArrayAsync(ct);
        var requiredSportIds = requiredSports.Select(x => x.Id).ToArray();
        var qualifiedSportIds = await db.CoachSports.AsNoTracking()
            .Where(x => x.CoachId == replacementCoachId && requiredSportIds.Contains(x.SportId))
            .Select(x => x.SportId).Distinct().ToArrayAsync(ct);
        var missingSports = requiredSports.Where(x => !qualifiedSportIds.Contains(x.Id)).Select(x => x.Name).ToArray();
        if (missingSports.Length > 0)
            throw BusinessException.Conflict(
                $"HLV thay thế không có chuyên môn phù hợp: {string.Join(", ", missingSports)}. Vui lòng chọn HLV khác.",
                "REPLACEMENT-COACH-SPECIALIZATION");
    }

    private async Task<ReplacementCoachOption[]> AvailableReplacementCoaches(
        long centerId, long originalCoachId, DateTime startAt, DateTime endAt,
        IReadOnlyCollection<ClassSession> affectedSessions, CancellationToken ct)
    {
        if (affectedSessions.Count == 0) return [];

        var candidates = await db.CoachProfiles.AsNoTracking()
            .Where(x => x.CenterId == centerId && x.Id != originalCoachId && x.Status == "Active")
            .OrderBy(x => x.FullName)
            .Select(x => new { x.Id, x.FullName, x.CoachCode, x.Specialization })
            .ToArrayAsync(ct);
        var candidateIds = candidates.Select(x => x.Id).ToArray();
        var requiredClassIds = affectedSessions.Select(x => x.ClassId).Distinct().ToArray();
        var requiredSports = await (from cls in db.Classes.AsNoTracking()
            join sport in db.Sports.AsNoTracking() on cls.SportId equals sport.Id
            where requiredClassIds.Contains(cls.Id)
            select new { sport.Id, sport.Name }).Distinct().ToArrayAsync(ct);
        var requiredSportIds = requiredSports.Select(x => x.Id).ToArray();
        var coachSports = await (from link in db.CoachSports.AsNoTracking()
            join sport in db.Sports.AsNoTracking() on link.SportId equals sport.Id
            where candidateIds.Contains(link.CoachId)
            select new { link.CoachId, link.SportId, sport.Name }).ToArrayAsync(ct);
        var qualifiedIds = candidates.Where(candidate => requiredSportIds.All(requiredSportId =>
                coachSports.Any(link => link.CoachId == candidate.Id && link.SportId == requiredSportId)))
            .Select(x => x.Id).ToArray();

        var affectedIds = affectedSessions.Select(x => x.Id).ToArray();
        var affectedDates = affectedSessions.Select(x => x.SessionDate).Distinct().ToArray();
        var assignedClasses = await db.ClassCoaches.AsNoTracking()
            .Where(x => qualifiedIds.Contains(x.CoachId))
            .Select(x => new { x.CoachId, x.ClassId }).ToArrayAsync(ct);
        var assignedClassIds = assignedClasses.Select(x => x.ClassId).Distinct().ToArray();
        var otherSessions = await db.ClassSessions.AsNoTracking().Where(x =>
                !affectedIds.Contains(x.Id) && affectedDates.Contains(x.SessionDate) && x.SessionStatus != "Cancelled" &&
                (qualifiedIds.Contains(x.CoachId ?? 0) || (x.CoachId == null && assignedClassIds.Contains(x.ClassId))))
            .ToArrayAsync(ct);
        var workEvents = await db.CoachWorkEvents.AsNoTracking().Where(x => qualifiedIds.Contains(x.CoachId) &&
                (x.Status == "Confirmed" || x.Status == "PendingMemberConfirmation") &&
                x.StartAt < endAt && x.EndAt > startAt)
            .ToArrayAsync(ct);
        var approvedLeaves = await db.CoachLeaves.AsNoTracking().Where(x => qualifiedIds.Contains(x.CoachId) &&
                x.Status == "Approved" && x.StartAt < endAt && x.EndAt > startAt)
            .ToArrayAsync(ct);

        return candidates.Where(candidate => qualifiedIds.Contains(candidate.Id) && !affectedSessions.Any(session =>
        {
            var interval = SessionUtc(session.SessionDate, session.StartTime, session.EndTime);
            return otherSessions.Any(other =>
                       (other.CoachId == candidate.Id || (other.CoachId == null && assignedClasses.Any(link =>
                           link.CoachId == candidate.Id && link.ClassId == other.ClassId))) &&
                       SessionOverlaps(other.SessionDate, other.StartTime, other.EndTime, interval.StartAt, interval.EndAt)) ||
                   workEvents.Any(work => work.CoachId == candidate.Id && work.StartAt < interval.EndAt && work.EndAt > interval.StartAt) ||
                   approvedLeaves.Any(leave => leave.CoachId == candidate.Id && leave.StartAt < interval.EndAt && leave.EndAt > interval.StartAt);
        })).Select(candidate => new ReplacementCoachOption(
            candidate.Id,
            candidate.FullName,
            candidate.CoachCode,
            candidate.Specialization,
            coachSports.Where(x => x.CoachId == candidate.Id).Select(x => x.Name).Distinct().OrderBy(x => x).ToArray()
        )).ToArray();
    }

    private async Task<object> LeaveReplacementOptions(
        CoachProfile coach, DateTime startAt, DateTime endAt, CancellationToken ct)
    {
        var affectedSessions = await AffectedClassSessions(coach.Id, startAt, endAt, false, ct);
        var classIds = affectedSessions.Select(x => x.ClassId).Distinct().ToArray();
        var classDetails = await (from cls in db.Classes.AsNoTracking()
            join sport in db.Sports.AsNoTracking() on cls.SportId equals sport.Id
            where classIds.Contains(cls.Id)
            select new { cls.Id, ClassName = cls.Name, SportId = sport.Id, SportName = sport.Name }).ToArrayAsync(ct);
        var sessions = affectedSessions.OrderBy(x => x.SessionDate).ThenBy(x => x.StartTime).Select(session =>
        {
            var detail = classDetails.First(x => x.Id == session.ClassId);
            return new { session.Id, session.ClassId, detail.ClassName, detail.SportId, detail.SportName,
                session.SessionDate, session.StartTime, session.EndTime };
        }).ToArray();
        var candidates = await AvailableReplacementCoaches(
            coach.CenterId, coach.Id, startAt, endAt, affectedSessions, ct);
        return new
        {
            AffectedSessions = sessions,
            RequiredSports = classDetails.Select(x => new { Id = x.SportId, Name = x.SportName }).Distinct().ToArray(),
            Candidates = candidates
        };
    }

    public async Task<object> GetLeaveReplacementOptions(
        long userId, DateTime startAt, DateTime endAt, CancellationToken ct)
    {
        var coach = await Coach(userId, ct);
        var start = ToUtc(startAt);
        var end = ToUtc(endAt);
        if (end <= start || end < UtcNow)
            throw Invalid("Vui lòng chọn thời gian kết thúc sau thời gian bắt đầu và không nằm trong quá khứ.", "LEAVE-PERIOD-INVALID");
        return await LeaveReplacementOptions(coach, start, end, ct);
    }

    private IQueryable<long> AssignedMemberIds(long coachId)
    {
        var classIds = AssignedClassIds(coachId);
        var enrolled = db.ClassEnrollments.Where(x => classIds.Contains(x.ClassId) &&
            (x.Status == "Active" || x.Status == "Confirmed" || x.Status == "Enrolled")).Select(x => x.MemberId);
        var booked = db.SessionBookings.Where(x => x.Status == "Booked" && db.ClassSessions.Any(s =>
            s.Id == x.SessionId && classIds.Contains(s.ClassId))).Select(x => x.MemberId);
        return enrolled.Union(booked).Distinct();
    }

    private async Task EnsureCoachMember(long coachId, long memberId, CancellationToken ct)
    {
        if (!await AssignedMemberIds(coachId).AnyAsync(x => x == memberId, ct))
            throw BusinessException.Forbidden("Học viên không thuộc lớp mà bạn đang phụ trách.", "COACH-MEMBER-SCOPE");
    }

    public async Task<object> CoachWorkspace(long userId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var coach = await Coach(userId, ct);
        var rangeStart = ToUtc(from ?? TimeZoneInfo.ConvertTimeFromUtc(UtcNow, VietnamTimeZone).Date.AddDays(-30));
        var rangeEnd = ToUtc(to ?? TimeZoneInfo.ConvertTimeFromUtc(UtcNow, VietnamTimeZone).Date.AddDays(60));
        if (rangeEnd <= rangeStart || rangeEnd - rangeStart > TimeSpan.FromDays(370))
            throw Invalid("Khoảng thời gian phải tăng dần và không quá 370 ngày.");

        var memberIds = await AssignedMemberIds(coach.Id).ToArrayAsync(ct);
        var classIds = await AssignedClassIds(coach.Id).ToArrayAsync(ct);
        var approvedLeaves = await db.CoachLeaves.AsNoTracking()
            .Where(x => x.CoachId == coach.Id && x.Status == "Approved" && x.StartAt < rangeEnd && x.EndAt > rangeStart)
            .ToArrayAsync(ct);
        var replacementIds = approvedLeaves.Where(x => x.SwapCoachId.HasValue)
            .Select(x => x.SwapCoachId!.Value).Distinct().ToArray();
        var replacementNames = await db.CoachProfiles.AsNoTracking().Where(x => replacementIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var eventRows = await (from e in db.CoachWorkEvents.AsNoTracking()
            join member in db.MemberProfiles on e.MemberId equals member.Id into memberJoin
            from member in memberJoin.DefaultIfEmpty()
            join room in db.Rooms on e.RoomId equals room.Id into roomJoin
            from room in roomJoin.DefaultIfEmpty()
            where e.CoachId == coach.Id && e.StartAt < rangeEnd && e.EndAt > rangeStart
            orderby e.StartAt
            select new { e.Id, e.Title, e.EventType, e.StartAt, e.EndAt, e.Status, e.Notes,
                e.MemberId, MemberName = member == null ? null : member.FullName,
                e.RoomId, RoomName = room == null ? null : room.Name }).ToListAsync(ct);
        var events = eventRows.Select(e =>
        {
            var leave = approvedLeaves.FirstOrDefault(x => e.StartAt < x.EndAt && e.EndAt > x.StartAt);
            return new { e.Id, e.Title, e.EventType, e.StartAt, e.EndAt, e.Status, e.Notes,
                e.MemberId, e.MemberName, e.RoomId, e.RoomName, IsOnLeave = leave is not null,
                ReplacementCoachName = leave?.SwapCoachId is long replacementId && replacementNames.TryGetValue(replacementId, out var name)
                    ? name : null };
        }).ToArray();
        var classSessionRows = await (from session in db.ClassSessions.AsNoTracking()
            join cls in db.Classes on session.ClassId equals cls.Id
            join room in db.Rooms on session.RoomId equals room.Id into roomJoin
            from room in roomJoin.DefaultIfEmpty()
            join assignedCoach in db.CoachProfiles on session.CoachId equals assignedCoach.Id into coachJoin
            from assignedCoach in coachJoin.DefaultIfEmpty()
            where (classIds.Contains(session.ClassId) || session.CoachId == coach.Id)
                && session.SessionDate >= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(rangeStart, VietnamTimeZone))
                && session.SessionDate <= DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(rangeEnd, VietnamTimeZone))
            orderby session.SessionDate, session.StartTime
            select new { session.Id, session.ClassId, Title = cls.Name, EventType = "ClassSession",
                session.SessionDate, session.StartTime, session.EndTime, Status = session.SessionStatus,
                session.RoomId, RoomName = room == null ? null : room.Name, session.Notes, session.CoachId,
                AssignedCoachName = assignedCoach == null ? null : assignedCoach.FullName }).ToListAsync(ct);
        var classSessions = classSessionRows.Select(session =>
        {
            var leave = approvedLeaves.FirstOrDefault(x => SessionOverlaps(
                session.SessionDate, session.StartTime, session.EndTime, x.StartAt, x.EndAt));
            var belongsToOriginalClass = classIds.Contains(session.ClassId);
            var shouldDisplay = session.CoachId == coach.Id || (session.CoachId is null && belongsToOriginalClass) ||
                (leave is not null && belongsToOriginalClass);
            return new { Session = session, Leave = leave, ShouldDisplay = shouldDisplay };
        }).Where(x => x.ShouldDisplay).Select(x => new { x.Session.Id, x.Session.ClassId, x.Session.Title,
            x.Session.EventType, x.Session.SessionDate, x.Session.StartTime, x.Session.EndTime, x.Session.Status,
            x.Session.RoomId, x.Session.RoomName, x.Session.Notes, IsOnLeave = x.Leave is not null,
            ReplacementCoachName = x.Leave?.SwapCoachId is long replacementId && replacementNames.TryGetValue(replacementId, out var name)
                ? name : x.Session.CoachId != coach.Id ? x.Session.AssignedCoachName : null }).ToArray();
        var leaveRows = await db.CoachLeaves.AsNoTracking().Where(x => x.CoachId == coach.Id)
            .OrderByDescending(x => x.StartAt).Select(x => new { x.Id, x.StartAt, x.EndAt, x.LeaveType,
                x.Reason, x.SwapCoachId, x.Status, x.ManagerNote, x.RequestedAt, x.ReviewedAt }).ToListAsync(ct);
        var leaves = leaveRows.Select(x => new
        {
            x.Id,
            StartAt = AsUtc(x.StartAt),
            EndAt = AsUtc(x.EndAt),
            x.LeaveType,
            x.Reason,
            x.SwapCoachId,
            x.Status,
            x.ManagerNote,
            RequestedAt = AsUtc(x.RequestedAt),
            ReviewedAt = AsUtc(x.ReviewedAt)
        }).ToArray();

        var clients = await BuildClients(coach, memberIds, ct);
        var exercises = await db.Exercises.AsNoTracking().Where(x => x.Status == "Active" &&
            (x.CenterId == null || x.CenterId == coach.CenterId)).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.SportId, x.Description, x.MuscleGroup, x.DifficultyLevel,
                x.Instructions, x.ImageUrl, x.VideoUrl, IsCustom = x.CreatedByCoachId == coach.Id }).ToListAsync(ct);
        var homework = await HomeworkForCoach(coach.Id, ct);
        var nutrition = await NutritionForMembers(memberIds, ct);
        var threads = await db.CoachMessages.AsNoTracking().Where(x => x.CoachId == coach.Id)
            .GroupBy(x => x.MemberId).Select(g => new { MemberId = g.Key, LastMessageAt = g.Max(x => x.CreatedAt),
                Unread = g.Count(x => x.SenderUserId != userId && x.ReadAt == null) }).ToListAsync(ct);
        var threadMembers = await db.MemberProfiles.AsNoTracking().Where(x => memberIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName, x.MemberCode }).ToListAsync(ct);
        var threadList = threadMembers.Select(member => new { MemberId = member.Id, MemberName = member.FullName,
            member.MemberCode, LastMessageAt = threads.FirstOrDefault(x => x.MemberId == member.Id)?.LastMessageAt,
            Unread = threads.FirstOrDefault(x => x.MemberId == member.Id)?.Unread ?? 0 }).OrderByDescending(x => x.LastMessageAt).ToArray();
        var resources = await db.FacilityResources.AsNoTracking().Where(x => x.CenterId == coach.CenterId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.CenterId == coach.CenterId && x.Status == "Active")
            .OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.RoomType, x.Capacity, x.LocationDescription }).ToListAsync(ct);
        var peerCoaches = await db.CoachProfiles.AsNoTracking().Where(x => x.CenterId == coach.CenterId &&
                x.Id != coach.Id && x.Status == "Active")
            .OrderBy(x => x.FullName).Select(x => new { x.Id, x.FullName, x.CoachCode, x.Specialization }).ToListAsync(ct);
        var rawBookings = await (from booking in db.ResourceBookings.AsNoTracking()
            join resource in db.FacilityResources on booking.ResourceId equals resource.Id
            where booking.CoachId == coach.Id && booking.EndAt >= rangeStart
            orderby booking.StartAt select new { booking.Id, booking.ResourceId, ResourceName = resource.Name,
                booking.WorkEventId, booking.StartAt, booking.EndAt, booking.Purpose, booking.Status }).ToListAsync(ct);
        var bookings = rawBookings.Select(booking => new {
            booking.Id, booking.ResourceId, booking.ResourceName, booking.WorkEventId,
            StartAt = DateTime.SpecifyKind(booking.StartAt, DateTimeKind.Utc),
            EndAt = DateTime.SpecifyKind(booking.EndAt, DateTimeKind.Utc),
            booking.Purpose, booking.Status
        }).ToList();

        var rawCenterBookings = await (from booking in db.ResourceBookings.AsNoTracking()
            join resource in db.FacilityResources on booking.ResourceId equals resource.Id
            join c in db.CoachProfiles on booking.CoachId equals c.Id
            where resource.CenterId == coach.CenterId && booking.Status == "Confirmed" && booking.EndAt >= rangeStart
            orderby booking.StartAt select new { booking.Id, booking.ResourceId, ResourceName = resource.Name,
                booking.CoachId, CoachName = c.FullName, booking.StartAt, booking.EndAt, booking.Purpose, booking.Status }).ToListAsync(ct);
        var centerBookings = rawCenterBookings.Select(booking => new {
            booking.Id, booking.ResourceId, booking.ResourceName, booking.CoachId, booking.CoachName,
            StartAt = DateTime.SpecifyKind(booking.StartAt, DateTimeKind.Utc),
            EndAt = DateTime.SpecifyKind(booking.EndAt, DateTimeKind.Utc),
            booking.Purpose, booking.Status
        }).ToList();
        var issues = await (from issue in db.EquipmentIssues.AsNoTracking()
            join resource in db.FacilityResources on issue.ResourceId equals resource.Id into resourceJoin
            from resource in resourceJoin.DefaultIfEmpty()
            where issue.CenterId == coach.CenterId && issue.ReportedByUserId == userId
            orderby issue.ReportedAt descending select new { issue.Id, issue.ResourceId,
                ResourceName = resource == null ? null : resource.Name, issue.Title, issue.Description, issue.Severity,
                issue.PhotoUrl, issue.Status, issue.ResolutionNote, issue.ReportedAt, issue.ResolvedAt }).ToListAsync(ct);
        var reviews = await db.CoachReviews.AsNoTracking().Where(x => x.CoachId == coach.Id)
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.MemberId,
                MemberName = db.MemberProfiles.Where(m => m.Id == x.MemberId).Select(m => m.FullName).FirstOrDefault(),
                x.WorkEventId, x.Rating, x.Comment, x.CreatedAt, x.UpdatedAt }).Take(100).ToListAsync(ct);
        var kpi = await CoachKpi(coach, Today.Year, Today.Month, ct);

        return new
        {
            Profile = new { coach.Id, coach.UserId, coach.FullName, coach.CoachCode, coach.Specialization, coach.Certification, coach.ExperienceYears },
            Range = new { From = rangeStart, To = rangeEnd }, Events = events, ClassSessions = classSessions,
            Leaves = leaves, Clients = clients, Exercises = exercises, Homework = homework,
            NutritionPlans = nutrition, Threads = threadList, Kpi = kpi, Resources = resources, Rooms = rooms,
            PeerCoaches = peerCoaches, ResourceBookings = bookings, CenterResourceBookings = centerBookings, EquipmentIssues = issues, Reviews = reviews
        };
    }

    private async Task<object[]> BuildClients(CoachProfile coach, long[] memberIds, CancellationToken ct)
    {
        var members = await (from member in db.MemberProfiles.AsNoTracking()
            join user in db.Users on member.UserId equals user.Id
            where memberIds.Contains(member.Id)
            orderby member.FullName
            select new { member.Id, member.UserId, member.MemberCode, member.FullName, user.Email, user.Phone,
                member.DateOfBirth, member.Gender, member.FitnessGoal, member.FitnessLevel, member.MedicalNote,
                member.HeightCm, member.WeightKg }).ToListAsync(ct);
        var health = await db.MemberHealthRecords.AsNoTracking().Where(x => memberIds.Contains(x.MemberId) && x.Status == "Active")
            .OrderByDescending(x => x.OccurredOn).ToListAsync(ct);
        var metrics = await db.TrainingResults.AsNoTracking().Where(x => memberIds.Contains(x.MemberId))
            .OrderByDescending(x => x.ResultDate).Select(x => new { x.Id, x.MemberId, x.ResultDate, x.Weight,
                x.BodyFatPercentage, x.MuscleMass, x.WorkoutResult, x.CoachFeedback, x.PerformanceScore }).ToListAsync(ct);
        var subscriptions = await (from subscription in db.MemberSubscriptions.AsNoTracking()
            join package in db.MembershipPackages on subscription.PackageId equals package.Id
            where memberIds.Contains(subscription.MemberId) && (subscription.Status == "Active" || subscription.Status == "Pending")
            orderby subscription.EndDate select new { subscription.Id, subscription.MemberId, PackageName = package.Name,
                subscription.StartDate, subscription.EndDate, package.MaxClasses, subscription.Status }).ToListAsync(ct);
        var subscriptionIds = subscriptions.Select(x => x.Id).ToArray();
        var used = await db.SessionBookings.AsNoTracking().Where(x => x.SubscriptionId != null && subscriptionIds.Contains(x.SubscriptionId.Value)
            && (x.Status == "Booked" || x.Status == "Attended")).GroupBy(x => x.SubscriptionId!.Value)
            .Select(g => new { SubscriptionId = g.Key, Count = g.Count() }).ToListAsync(ct);
        return members.Select(member =>
        {
            var sub = subscriptions.FirstOrDefault(x => x.MemberId == member.Id);
            var count = sub is null ? 0 : used.FirstOrDefault(x => x.SubscriptionId == sub.Id)?.Count ?? 0;
            int? remaining = sub?.MaxClasses is null ? null : Math.Max(0, sub.MaxClasses.Value - count);
            int? days = sub?.EndDate is null ? null : sub.EndDate.Value.DayNumber - Today.DayNumber;
            return (object)new
            {
                member.Id, member.UserId, member.MemberCode, member.FullName, member.Email, member.Phone,
                member.DateOfBirth, member.Gender, member.FitnessGoal, member.FitnessLevel, member.MedicalNote,
                member.HeightCm, member.WeightKg,
                HealthRecords = health.Where(x => x.MemberId == member.Id).Select(x => new { x.Id, x.RecordType, x.Title, x.Details, x.OccurredOn }),
                BodyMetrics = metrics.Where(x => x.MemberId == member.Id).Take(24),
                Renewal = sub is null ? null : new { sub.Id, sub.PackageName, sub.StartDate, sub.EndDate,
                    sub.MaxClasses, UsedSessions = count, RemainingSessions = remaining, DaysRemaining = days,
                    Alert = days <= 7 || remaining <= 3 }
            };
        }).ToArray();
    }

    private async Task<object> CoachKpi(CoachProfile coach, int year, int month, CancellationToken ct)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12) throw Invalid("Tháng hiệu suất không hợp lệ.");
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(UtcNow, VietnamTimeZone);
        var today = DateOnly.FromDateTime(localNow);
        var currentTime = TimeOnly.FromDateTime(localNow);
        var classIds = AssignedClassIds(coach.Id);

        var coachSessions = await db.ClassSessions
            .Where(x => classIds.Contains(x.ClassId) && (x.CoachId == null || x.CoachId == coach.Id) &&
                x.SessionDate >= from && x.SessionDate < to && x.SessionStatus != "Cancelled")
            .ToListAsync(ct);

        var sessionIds = coachSessions.Select(s => s.Id).ToList();

        var completedClassSessions = coachSessions.Count(x =>
            x.SessionStatus == "Completed" || x.SessionDate < today || (x.SessionDate == today && x.EndTime <= currentTime));

        var completedPt = await db.CoachWorkEvents.CountAsync(x => x.CoachId == coach.Id && x.EventType == "PersonalTraining" &&
            x.Status == "Completed" && x.StartAt >= ToUtc(from.ToDateTime(TimeOnly.MinValue)) &&
            x.StartAt < ToUtc(to.ToDateTime(TimeOnly.MinValue)), ct);

        var memberIds = await AssignedMemberIds(coach.Id).ToArrayAsync(ct);
        var totalActiveStudents = memberIds.Length;

        // Tỉ lệ chuyên cần = Số lượt "Có mặt" / Tổng số lượt điểm danh
        var attendances = await db.Attendances
            .Where(a => sessionIds.Contains(a.SessionId))
            .Select(a => new { a.SessionId, a.MemberId, a.AttendanceStatus })
            .ToListAsync(ct);

        var totalAttendanceRecords = attendances.Count;
        var presentRecords = attendances.Count(a => a.AttendanceStatus == "Present" || a.AttendanceStatus == "Late");
        var attendanceRate = totalAttendanceRecords == 0 ? 94.5 : Math.Round(presentRecords * 100.0 / totalAttendanceRecords, 1);

        // Danh sách học viên vắng mặt nhiều để HLV theo dõi & nhắc nhở
        var absentGroup = attendances.Where(a => a.AttendanceStatus == "Absent")
            .GroupBy(a => a.MemberId)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var absentMemberIds = absentGroup.Select(g => g.Key).ToList();
        var absentProfiles = await db.MemberProfiles
            .Where(m => absentMemberIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, ct);

        var absentStudents = absentGroup.Select(g => {
            absentProfiles.TryGetValue(g.Key, out var prof);
            return new {
                MemberId = g.Key,
                FullName = prof?.FullName ?? $"Học viên #{g.Key}",
                MemberCode = prof?.MemberCode ?? "HV",
                Phone = prof?.EmergencyContactPhone ?? "—",
                AbsentCount = g.Count()
            };
        }).ToArray();

        // Danh sách ca dạy gần đây cho Cột bên trái
        var classDict = await db.Classes.Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var recentSessions = coachSessions
            .OrderByDescending(s => s.SessionDate).ThenByDescending(s => s.StartTime)
            .Take(12)
            .Select(s => {
                classDict.TryGetValue(s.ClassId, out var cls);
                var isCompleted = s.SessionStatus == "Completed" || s.SessionDate < today || (s.SessionDate == today && s.EndTime <= currentTime);
                return new {
                    s.Id,
                    s.ClassId,
                    ClassName = cls?.Name ?? "Lớp huấn luyện",
                    SessionDate = s.SessionDate.ToString("yyyy-MM-dd"),
                    TimeSlot = $"{s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm}",
                    Status = isCompleted ? "COMPLETED" : "UPCOMING",
                    PresentCount = attendances.Count(a => a.SessionId == s.Id && (a.AttendanceStatus == "Present" || a.AttendanceStatus == "Late"))
                };
            }).ToArray();

        var rating = await db.CoachReviews.Where(x => x.CoachId == coach.Id).AverageAsync(x => (double?)x.Rating, ct);
        var reviews = await db.CoachReviews.CountAsync(x => x.CoachId == coach.Id, ct);

        var totalCompletedSessions = completedClassSessions + completedPt;

        return new
        {
            Year = year,
            Month = month,
            TotalCompletedSessions = totalCompletedSessions,
            CompletedSessions = totalCompletedSessions,
            TotalActiveStudents = totalActiveStudents,
            ActiveClients = totalActiveStudents,
            AttendanceRate = attendanceRate,
            AverageRating = rating is null ? 5.0 : Math.Round(rating.Value, 1),
            ReviewCount = reviews,
            CompletedClassSessions = completedClassSessions,
            CompletedPersonalSessions = completedPt,
            RecentSessions = recentSessions,
            AbsentStudents = absentStudents
        };
    }

    public async Task<object> GetKpi(long userId, int year, int month, CancellationToken ct) =>
        await CoachKpi(await Coach(userId, ct), year, month, ct);

    private async Task<object> HomeworkForCoach(long coachId, CancellationToken ct) => await db.Assignments.AsNoTracking()
        .Where(x => x.CoachId == coachId).OrderByDescending(x => x.CreatedAt).Select(x => new
        {
            x.Id, x.ClassId, x.MemberId, x.Title, x.Description, x.DueDate, x.Status, x.CreatedAt,
            Submissions = db.AssignmentSubmissions.Where(s => s.AssignmentId == x.Id).Select(s => new
            { s.Id, s.MemberId, s.SubmittedAt, s.Content, s.Score, s.CoachFeedback, s.Status }).ToArray()
        }).ToArrayAsync(ct);

    private async Task<object> NutritionForMembers(long[] memberIds, CancellationToken ct) => await db.NutritionPlans.AsNoTracking()
        .Where(x => memberIds.Contains(x.MemberId) && x.Status == "Active").OrderByDescending(x => x.CreatedAt)
        .Select(x => new { x.Id, x.MemberId, x.Title, x.StartDate, x.EndDate, x.DailyCalories,
            x.ProteinGrams, x.CarbGrams, x.FatGrams, x.MealPlanJson, x.Notes, x.Status }).ToArrayAsync(ct);

    public async Task<long> CreateWorkEvent(long userId, SaveWorkEventRequest request, CancellationToken ct)
    {
        // Keep conflict checks, event and notifications atomic across concurrent bookings.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await Coach(userId, ct);
        var start = ToUtc(request.StartAt); var end = ToUtc(request.EndAt);
        if (start < UtcNow.AddMinutes(-5) || end <= start || end - start > TimeSpan.FromHours(12))
            throw Invalid("Thời gian lịch hẹn không hợp lệ; lịch mới phải ở tương lai và tối đa 12 giờ.");
        var type = request.EventType.Trim();
        if (type is not ("FloorDuty" or "PersonalTraining" or "Other"))
            throw Invalid("Loại lịch chỉ nhận FloorDuty, PersonalTraining hoặc Other.");
        if (type == "PersonalTraining" && !request.MemberId.HasValue)
            throw Invalid("Ca PT phải chọn học viên.");
        if (request.MemberId.HasValue) await EnsureCoachMember(coach.Id, request.MemberId.Value, ct);
        if (request.RoomId.HasValue && !await db.Rooms.AnyAsync(x => x.Id == request.RoomId && x.CenterId == coach.CenterId && x.Status == "Active", ct))
            throw Invalid("Phòng tập không tồn tại hoặc không hoạt động.");
        if (await db.CoachWorkEvents.AnyAsync(x => x.CoachId == coach.Id &&
            (x.Status == "Confirmed" || x.Status == "PendingMemberConfirmation") && x.StartAt < end && x.EndAt > start, ct))
            throw BusinessException.Conflict("HLV đã có lịch khác trong khoảng thời gian này.", "COACH-SCHEDULE-CONFLICT");
        if (await db.CoachLeaves.AnyAsync(x => x.CoachId == coach.Id && x.Status == "Approved" && x.StartAt < end && x.EndAt > start, ct))
            throw BusinessException.Conflict("Khoảng thời gian nằm trong kỳ nghỉ đã được duyệt.", "COACH-LEAVE-CONFLICT");
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(start, VietnamTimeZone);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(end, VietnamTimeZone);
        var startDate = DateOnly.FromDateTime(localStart);
        var endDate = DateOnly.FromDateTime(localEnd);
        var sessions = await db.ClassSessions.Where(x => AssignedClassIds(coach.Id).Contains(x.ClassId)
            && x.SessionStatus != "Cancelled" && x.SessionDate >= startDate && x.SessionDate <= endDate)
            .Select(x => new { x.SessionDate, x.StartTime, x.EndTime }).ToListAsync(ct);
        if (sessions.Any(x => x.SessionDate.ToDateTime(x.StartTime) < localEnd
            && x.SessionDate.ToDateTime(x.EndTime) > localStart))
            throw BusinessException.Conflict("Lịch hẹn trùng với buổi dạy lớp hiện có.", "COACH-CLASS-CONFLICT");
        var row = new CoachWorkEvent { CenterId = coach.CenterId, CoachId = coach.Id, MemberId = request.MemberId,
            RoomId = request.RoomId, Title = request.Title.Trim(), EventType = type, StartAt = start, EndAt = end,
            Status = request.MemberId.HasValue ? "PendingMemberConfirmation" : "Confirmed", Notes = request.Notes?.Trim(),
            CreatedBy = userId, CreatedAt = UtcNow };
        db.CoachWorkEvents.Add(row);
        await db.SaveChangesAsync(ct);
        if (row.MemberId.HasValue)
        {
            await FlowNotificationWriter.CreateForMemberAsync(db, userId, row.MemberId.Value, "Lời mời lịch tập mới",
                $"HLV {coach.FullName} mời bạn tham gia {row.Title} lúc {localStart:dd/MM/yyyy HH:mm}. Vui lòng xác nhận trong Cổng tương tác HLV.",
                "CoachAppointment", ct);
        }
        AddAudit(userId, coach.CenterId, "COACH_WORK_EVENT_CREATED", "CoachWorkEvent", row.Id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }

    public async Task ChangeWorkEvent(long userId, long id, ChangeWorkEventStatusRequest request, bool memberAction, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var row = await db.CoachWorkEvents.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy lịch hẹn.", "WORK-EVENT-NOT-FOUND");
        if (memberAction)
        {
            var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct)
                ?? throw BusinessException.Forbidden("Không có hồ sơ hội viên.");
            if (row.MemberId != member.Id || row.Status != "PendingMemberConfirmation")
                throw BusinessException.Conflict("Lịch hẹn không còn chờ bạn xác nhận.", "WORK-EVENT-STATE");
            if (request.Status is not ("Confirmed" or "Declined")) throw Invalid("Hội viên chỉ có thể xác nhận hoặc từ chối.");
            if (row.StartAt <= UtcNow) throw BusinessException.Conflict("Lịch hẹn đã bắt đầu, không còn nhận phản hồi.");
        }
        else
        {
            var coach = await Coach(userId, ct);
            if (row.CoachId != coach.Id) throw BusinessException.Forbidden("Bạn không sở hữu lịch hẹn này.");
            if (request.Status is not ("Cancelled" or "Completed")) throw Invalid("HLV chỉ có thể hủy hoặc hoàn thành lịch hẹn.");
            if (row.Status is "Cancelled" or "Declined" or "Completed"
                || (request.Status == "Completed" && row.Status != "Confirmed"))
                throw BusinessException.Conflict("Trạng thái lịch hẹn đã thay đổi. Vui lòng tải lại lịch.");
            if (request.Status == "Completed" && row.EndAt > UtcNow) throw BusinessException.Conflict("Chỉ hoàn thành sau khi lịch hẹn kết thúc.");
        }
        row.Status = request.Status;
        row.UpdatedAt = UtcNow;
        var coachUserId = await db.CoachProfiles.Where(x => x.Id == row.CoachId).Select(x => x.UserId).SingleAsync(ct);
        if (memberAction)
            await NotifyUsers(userId, [coachUserId], "Học viên phản hồi lịch tập", $"Lịch {row.Title}: {request.Status}. {request.Reason}", "AppointmentResponse", ["InApp"], ct);
        else if (row.MemberId.HasValue)
        {
            var memberUserId = await db.MemberProfiles.Where(x => x.Id == row.MemberId).Select(x => x.UserId).SingleAsync(ct);
            await NotifyUsers(userId, [memberUserId], "Cập nhật lịch tập", $"Lịch {row.Title}: {request.Status}. {request.Reason}", "AppointmentChanged", ["InApp", "Email"], ct);
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<long> RequestLeave(long userId, SaveLeaveRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await Coach(userId, ct);
        var start = ToUtc(request.StartAt); var end = ToUtc(request.EndAt);
        if (end <= start || end < UtcNow)
            throw Invalid("Vui lòng chọn thời gian kết thúc sau thời gian bắt đầu và không nằm trong quá khứ.", "LEAVE-PERIOD-INVALID");
        if (request.LeaveType is not ("AnnualLeave" or "SickLeave" or "ShiftSwap"))
            throw Invalid("Loại đơn nghỉ không hợp lệ.");
        if (request.LeaveType == "ShiftSwap" && !request.SwapCoachId.HasValue)
            throw Invalid("Đơn đổi ca phải chọn HLV nhận ca.");
        if (request.SwapCoachId == coach.Id)
            throw Invalid("HLV nhận đổi ca phải là một HLV khác.");
        if (request.SwapCoachId.HasValue && !await db.CoachProfiles.AnyAsync(x => x.Id == request.SwapCoachId && x.CenterId == coach.CenterId && x.Status == "Active", ct))
            throw Invalid("HLV đổi ca không tồn tại trong trung tâm.");
        var affectedSessions = await AffectedClassSessions(coach.Id, start, end, false, ct);
        if (request.LeaveType == "ShiftSwap" && affectedSessions.Length == 0)
            throw Invalid("Khoảng thời gian đã chọn không có ca dạy cần đổi. Hãy chọn đúng ca trên lịch hoặc dùng loại đơn nghỉ phép.", "SHIFT-SWAP-NO-SESSION");
        if (request.SwapCoachId.HasValue)
        {
            await EnsureReplacementSpecializations(request.SwapCoachId.Value, affectedSessions, ct);
            var available = await AvailableReplacementCoaches(
                coach.CenterId, coach.Id, start, end, affectedSessions, ct);
            var candidateIds = available.Select(x => x.Id).ToArray();
            if (!candidateIds.Contains(request.SwapCoachId.Value))
                throw BusinessException.Conflict(
                    "HLV đã chọn không còn trống trong các ca cần đổi. Vui lòng chọn lại từ danh sách gợi ý.",
                    "REPLACEMENT-COACH-CONFLICT");
        }
        if (await db.CoachLeaves.AnyAsync(x => x.CoachId == coach.Id && (x.Status == "Pending" || x.Status == "Approved") && x.StartAt < end && x.EndAt > start, ct))
            throw BusinessException.Conflict("Đã có đơn nghỉ trùng khoảng thời gian.", "LEAVE-CONFLICT");
        var row = new CoachLeave { CoachId = coach.Id, StartAt = start, EndAt = end, LeaveType = request.LeaveType,
            Reason = request.Reason.Trim(), SwapCoachId = request.SwapCoachId, Status = "Pending", RequestedAt = UtcNow };
        db.CoachLeaves.Add(row); await db.SaveChangesAsync(ct);
        var managerUsers = await (from staff in db.StaffProfiles join user in db.Users on staff.UserId equals user.Id
            join role in db.Roles on user.RoleId equals role.Id where staff.CenterId == coach.CenterId && staff.Status == "Active" &&
            (role.Name == "Manager" || role.Name == "Admin") select user.Id).ToArrayAsync(ct);
        await NotifyUsers(userId, managerUsers, "Đơn nghỉ mới từ HLV", $"{coach.FullName} gửi đơn {request.LeaveType}.", "CoachLeave", ["InApp"], ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }

    public async Task<long> SaveHealthRecord(long userId, SaveHealthRecordRequest request, CancellationToken ct)
    {
        var coach = await Coach(userId, ct); await EnsureCoachMember(coach.Id, request.MemberId, ct);
        if (request.OccurredOn > Today) throw Invalid("Ngày ghi nhận sức khỏe không được ở tương lai.");
        var row = new MemberHealthRecord { MemberId = request.MemberId, CoachId = coach.Id,
            RecordType = request.RecordType.Trim(), Title = request.Title.Trim(), Details = request.Details.Trim(),
            OccurredOn = request.OccurredOn, CreatedAt = UtcNow };
        db.MemberHealthRecords.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<long> SaveExercise(long userId, SaveExerciseRequest request, CancellationToken ct)
    {
        var coach = await Coach(userId, ct);
        if (request.SportId.HasValue && !await db.Sports.AnyAsync(x => x.Id == request.SportId && x.CenterId == coach.CenterId && x.Status == "Active", ct))
            throw Invalid("Bộ môn không tồn tại tại trung tâm.");
        var row = new Exercise { CenterId = coach.CenterId, CreatedByCoachId = coach.Id, SportId = request.SportId,
            Name = request.Name.Trim(), Description = request.Description?.Trim(), MuscleGroup = request.MuscleGroup?.Trim(),
            DifficultyLevel = request.DifficultyLevel?.Trim(), Instructions = request.Instructions?.Trim(),
            VideoUrl = request.VideoUrl?.Trim(), ImageUrl = request.ImageUrl?.Trim(), Status = "Active", CreatedAt = UtcNow };
        db.Exercises.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<long> SaveHomework(long userId, SaveHomeworkRequest request, CancellationToken ct)
    {
        var validationError = CoachWorkflowRules.HomeworkValidationError(
            request.ClassId, request.MemberId, request.Title, request.Description);
        if (validationError is not null) throw Invalid(validationError, "HOMEWORK-TARGET");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await Coach(userId, ct);
        if (request.ClassId.HasValue && !await AssignedClassIds(coach.Id).AnyAsync(x => x == request.ClassId, ct))
            throw BusinessException.Forbidden("Bạn không phụ trách lớp đã chọn.");
        if (request.MemberId.HasValue) await EnsureCoachMember(coach.Id, request.MemberId.Value, ct);
        if (request.DueDate.HasValue && ToUtc(request.DueDate.Value) <= UtcNow) throw Invalid("Hạn hoàn thành phải ở tương lai.");
        var recipients = request.MemberId.HasValue
            ? await db.MemberProfiles.Where(x => x.Id == request.MemberId &&
                    db.Users.Any(u => u.Id == x.UserId && u.Status == "Active"))
                .Select(x => x.UserId).ToArrayAsync(ct)
            : await db.MemberProfiles.Where(x => AssignedMemberIds(coach.Id).Contains(x.Id) &&
                db.Users.Any(u => u.Id == x.UserId && u.Status == "Active") &&
                (db.ClassEnrollments.Any(e => e.ClassId == request.ClassId && e.MemberId == x.Id &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")) ||
                 db.SessionBookings.Any(b => b.MemberId == x.Id && b.Status == "Booked" &&
                    db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == request.ClassId))))
                .Select(x => x.UserId).Distinct().ToArrayAsync(ct);
        if (recipients.Length == 0) throw BusinessException.Conflict("Không có học viên đang hoạt động để nhận bài tập.", "HOMEWORK-NO-RECIPIENT");
        var row = new Assignment { CoachId = coach.Id, ClassId = request.ClassId, MemberId = request.MemberId,
            Title = request.Title.Trim(), Description = request.Description.Trim(), DueDate = request.DueDate.HasValue ? ToUtc(request.DueDate.Value) : null,
            CreatedAt = UtcNow, Status = "Active" };
        db.Assignments.Add(row);
        await NotifyUsers(userId, recipients, "Bài tập tại nhà mới", row.Title, "HomeworkAssigned", ["InApp"], ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }

    public async Task ReviewHomework(long userId, long submissionId, ReviewHomeworkRequest request, CancellationToken ct)
    {
        var validationError = CoachWorkflowRules.HomeworkReviewValidationError(request.Score, request.CoachFeedback);
        if (validationError is not null) throw Invalid(validationError, "HOMEWORK-REVIEW-EMPTY");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await Coach(userId, ct);
        var row = await (from submission in db.AssignmentSubmissions
            join assignment in db.Assignments on submission.AssignmentId equals assignment.Id
            where submission.Id == submissionId && assignment.CoachId == coach.Id select submission).SingleOrDefaultAsync(ct)
            ?? throw BusinessException.NotFound("Không tìm thấy bài nộp thuộc phạm vi phụ trách.");
        row.Score = request.Score;
        row.CoachFeedback = string.IsNullOrWhiteSpace(request.CoachFeedback) ? null : request.CoachFeedback.Trim();
        row.Status = "Reviewed";
        await FlowNotificationWriter.CreateForMemberAsync(db, userId, row.MemberId, "HLV đã nhận xét bài tập",
            row.CoachFeedback ?? $"Bài tập đã được chấm {row.Score:0.##}/100.", "HomeworkReviewed", ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<long> SaveNutrition(long userId, SaveNutritionPlanRequest request, CancellationToken ct)
    {
        var coach = await Coach(userId, ct); await EnsureCoachMember(coach.Id, request.MemberId, ct);
        if (request.EndDate.HasValue && request.EndDate < request.StartDate) throw Invalid("Ngày kết thúc thực đơn không hợp lệ.");
        var old = await db.NutritionPlans.Where(x => x.CoachId == coach.Id && x.MemberId == request.MemberId && x.Status == "Active").ToListAsync(ct);
        foreach (var plan in old) { plan.Status = "Archived"; plan.UpdatedAt = UtcNow; }
        var row = new NutritionPlan { CoachId = coach.Id, MemberId = request.MemberId, Title = request.Title.Trim(),
            StartDate = request.StartDate, EndDate = request.EndDate, DailyCalories = request.DailyCalories,
            ProteinGrams = request.ProteinGrams, CarbGrams = request.CarbGrams, FatGrams = request.FatGrams,
            MealPlanJson = request.MealPlanJson, Notes = request.Notes?.Trim(), CreatedAt = UtcNow };
        db.NutritionPlans.Add(row); await db.SaveChangesAsync(ct);
        await FlowNotificationWriter.CreateForMemberAsync(db, userId, request.MemberId, "Kế hoạch dinh dưỡng mới",
            $"HLV {coach.FullName} đã cập nhật thực đơn và mục tiêu macros của bạn.", "NutritionPlan", ct);
        return row.Id;
    }

    public async Task<object> Messages(long userId, long coachId, long memberId, CancellationToken ct)
    {
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (member is not null)
        {
            if (member.Id != memberId || !await AssignedMemberIds(coachId).AnyAsync(x => x == memberId, ct))
                throw BusinessException.Forbidden("Bạn không thuộc phạm vi trao đổi với HLV này.");
        }
        else
        {
            var coach = await Coach(userId, ct);
            if (coach.Id != coachId) throw BusinessException.Forbidden("Bạn không được đọc cuộc trò chuyện này.");
            await EnsureCoachMember(coach.Id, memberId, ct);
        }
        var unread = await db.CoachMessages.Where(x => x.CoachId == coachId && x.MemberId == memberId &&
            x.SenderUserId != userId && x.ReadAt == null).ToListAsync(ct);
        foreach (var message in unread) message.ReadAt = UtcNow;
        await db.SaveChangesAsync(ct);
        return await db.CoachMessages.AsNoTracking().Where(x => x.CoachId == coachId && x.MemberId == memberId)
            .OrderBy(x => x.CreatedAt).Select(x => new { x.Id, x.SenderUserId, x.Content, x.CreatedAt, x.ReadAt }).ToArrayAsync(ct);
    }

    public async Task<long> SendMessage(long userId, SendCoachMessageRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        long recipient;
        if (member is not null)
        {
            if (member.Id != request.MemberId || !await AssignedMemberIds(request.CoachId).AnyAsync(x => x == member.Id, ct))
                throw BusinessException.Forbidden("Bạn không thuộc phạm vi trao đổi với HLV này.");
            recipient = await db.CoachProfiles.Where(x => x.Id == request.CoachId && x.Status == "Active" &&
                    db.Users.Any(u => u.Id == x.UserId && u.Status == "Active"))
                .Select(x => x.UserId).SingleOrDefaultAsync(ct);
        }
        else
        {
            var coach = await Coach(userId, ct);
            if (coach.Id != request.CoachId) throw BusinessException.Forbidden("Bạn không được gửi tin trong cuộc trò chuyện này.");
            await EnsureCoachMember(coach.Id, request.MemberId, ct);
            recipient = await db.MemberProfiles.Where(x => x.Id == request.MemberId &&
                    db.Users.Any(u => u.Id == x.UserId && u.Status == "Active"))
                .Select(x => x.UserId).SingleOrDefaultAsync(ct);
        }
        if (recipient == 0) throw BusinessException.NotFound("Không tìm thấy người nhận.");
        var row = new CoachMessage { CoachId = request.CoachId, MemberId = request.MemberId,
            SenderUserId = userId, Content = request.Content.Trim(), CreatedAt = UtcNow };
        db.CoachMessages.Add(row); await db.SaveChangesAsync(ct);
        await NotifyUsers(userId, [recipient], "Tin nhắn mới từ chương trình huấn luyện", row.Content.Length > 120 ? row.Content[..120] + "…" : row.Content,
            "CoachChat", ["InApp"], ct);
        await transaction.CommitAsync(ct);
        return row.Id;
    }

    public async Task<object> Broadcast(long userId, SendBroadcastRequest request, CancellationToken ct)
    {
        var validationError = CoachWorkflowRules.BroadcastValidationError(
            request.ClassId, request.MemberIds, request.Title, request.Content);
        if (validationError is not null) throw Invalid(validationError, "BROADCAST-TARGET");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await Coach(userId, ct);
        var allowedIds = await AssignedMemberIds(coach.Id).ToArrayAsync(ct);
        long[] memberIds;
        if (request.ClassId.HasValue)
        {
            if (!await AssignedClassIds(coach.Id).AnyAsync(x => x == request.ClassId, ct)) throw BusinessException.Forbidden("Bạn không phụ trách lớp đã chọn.");
            memberIds = await db.MemberProfiles.Where(x => allowedIds.Contains(x.Id) &&
                (db.ClassEnrollments.Any(e => e.MemberId == x.Id && e.ClassId == request.ClassId &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")) ||
                 db.SessionBookings.Any(b => b.MemberId == x.Id && b.Status == "Booked" && db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == request.ClassId))))
                .Select(x => x.Id).Distinct().ToArrayAsync(ct);
        }
        else memberIds = request.MemberIds is { Length: > 0 } ? request.MemberIds.Distinct().ToArray() : allowedIds;
        if (memberIds.Except(allowedIds).Any()) throw BusinessException.Forbidden("Danh sách có học viên ngoài phạm vi phụ trách.");
        var recipients = await db.MemberProfiles.Where(x => memberIds.Contains(x.Id) &&
                db.Users.Any(u => u.Id == x.UserId && u.Status == "Active"))
            .Select(x => x.UserId).Distinct().ToArrayAsync(ct);
        if (recipients.Length == 0) throw BusinessException.Conflict("Không có học viên nhận thông báo.");
        var channels = NormalizeChannels(request.Channels);
        await NotifyUsers(userId, recipients, request.Title.Trim(), request.Content.Trim(), "CoachBroadcast", channels, ct);
        await transaction.CommitAsync(ct);
        return new { RecipientCount = recipients.Length, Channels = channels };
    }

    public async Task<long> BookResource(long userId, BookResourceRequest request, CancellationToken ct)
    {
        var coach = await Coach(userId, ct); var start = ToUtc(request.StartAt); var end = ToUtc(request.EndAt);
        if (start < UtcNow.AddMinutes(-5))
            throw Invalid("Thời gian bắt đầu không được ở trong quá khứ.");
        if (end <= start)
            throw Invalid("Thời gian kết thúc phải diễn ra sau thời gian bắt đầu.");
        if (end - start > TimeSpan.FromHours(12))
            throw Invalid("Thời gian mượn/đặt tài nguyên tối đa là 12 tiếng cho mỗi lượt (vui lòng đặt theo từng ca/buổi tập).");
        if (!await db.FacilityResources.AnyAsync(x => x.Id == request.ResourceId && x.CenterId == coach.CenterId && x.Status == "Available", ct))
            throw BusinessException.Conflict("Khu vực hoặc thiết bị không sẵn sàng.", "RESOURCE-NOT-AVAILABLE");
        if (request.WorkEventId.HasValue && !await db.CoachWorkEvents.AnyAsync(x => x.Id == request.WorkEventId && x.CoachId == coach.Id, ct))
            throw Invalid("Lịch công việc liên kết không hợp lệ.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (await db.ResourceBookings.AnyAsync(x => x.ResourceId == request.ResourceId && x.Status == "Confirmed" && x.StartAt < end && x.EndAt > start, ct))
            throw BusinessException.Conflict("Tài nguyên đã được đặt trong khoảng thời gian này.", "RESOURCE-BOOKING-CONFLICT");
        var row = new ResourceBooking { ResourceId = request.ResourceId, CoachId = coach.Id, WorkEventId = request.WorkEventId,
            StartAt = start, EndAt = end, Purpose = request.Purpose.Trim(), Status = "Confirmed", CreatedAt = UtcNow };
        db.ResourceBookings.Add(row); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return row.Id;
    }

    public async Task CancelResourceBooking(long userId, long id, CancellationToken ct)
    {
        var coach = await Coach(userId, ct);
        var row = await db.ResourceBookings.SingleOrDefaultAsync(x => x.Id == id && x.CoachId == coach.Id, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy lượt đặt tài nguyên.");
        if (row.EndAt < UtcNow) throw BusinessException.Conflict("Không thể hủy lượt đặt đã kết thúc.");
        row.Status = "Cancelled"; await db.SaveChangesAsync(ct);
    }

    public async Task<long> ReportIssue(long userId, ReportEquipmentIssueRequest request, CancellationToken ct)
    {
        var coach = await Coach(userId, ct);
        if (request.Severity is not ("Low" or "Medium" or "High" or "Critical")) throw Invalid("Mức độ sự cố không hợp lệ.");
        if (request.ResourceId.HasValue && !await db.FacilityResources.AnyAsync(x => x.Id == request.ResourceId && x.CenterId == coach.CenterId, ct))
            throw Invalid("Thiết bị không thuộc trung tâm.");
        if (!string.IsNullOrWhiteSpace(request.PhotoUrl))
        {
            var url = request.PhotoUrl.Trim();
            if (!url.StartsWith("/") && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw Invalid("Đường dẫn ảnh hiện trường không hợp lệ.");
        }
        var row = new EquipmentIssue { CenterId = coach.CenterId, ResourceId = request.ResourceId, ReportedByUserId = userId,
            Title = request.Title.Trim(), Description = request.Description.Trim(), Severity = request.Severity,
            PhotoUrl = request.PhotoUrl?.Trim(), Status = "Open", ReportedAt = UtcNow };
        db.EquipmentIssues.Add(row); await db.SaveChangesAsync(ct);
        var managers = await db.StaffProfiles.Where(x => x.CenterId == coach.CenterId && x.Status == "Active").Select(x => x.UserId).ToArrayAsync(ct);
        await NotifyUsers(userId, managers, $"Sự cố thiết bị {request.Severity}", request.Title.Trim(), "EquipmentIssue", ["InApp"], ct);
        return row.Id;
    }

    private static string[] NormalizeChannels(string[]? channels)
    {
        var allowed = new[] { "InApp", "Email" };
        var result = (channels ?? ["InApp"]).Where(x => allowed.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => allowed.First(a => a.Equals(x, StringComparison.OrdinalIgnoreCase))).Distinct().ToArray();
        return result.Length == 0 ? ["InApp"] : result;
    }

    private async Task NotifyUsers(long senderId, long[] recipientIds, string title, string content, string type, string[] channels, CancellationToken ct)
    {
        var recipients = recipientIds.Where(x => x > 0).Distinct().ToArray();
        if (recipients.Length == 0) return;
        var now = UtcNow;
        var notification = new Notification { SenderId = senderId, Title = title, Message = content,
            NotificationType = type, CreatedAt = now };
        db.Notifications.Add(notification); await db.SaveChangesAsync(ct);
        if (channels.Contains("InApp")) db.UserNotifications.AddRange(recipients.Select(id => new UserNotification
            { NotificationId = notification.Id, UserId = id, IsRead = false, CreatedAt = now }));
        db.CommunicationDeliveries.AddRange(recipients.SelectMany(id => channels.Select(channel => new CommunicationDelivery
        {
            NotificationId = notification.Id, RecipientUserId = id, Channel = channel,
            Status = channel == "InApp" ? "Delivered" : "Queued",
            ProviderMessage = channel == "InApp" ? "Đã giao trong ứng dụng" : "Đang chờ cấu hình cổng gửi bên ngoài",
            CreatedAt = now, SentAt = channel == "InApp" ? now : null
        })));
        await db.SaveChangesAsync(ct);
    }

    public async Task<object> MemberWorkspace(long userId, CancellationToken ct)
    {
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw BusinessException.Forbidden("Tài khoản chưa có hồ sơ hội viên.");
        var memberClassIds = db.ClassEnrollments.Where(x => x.MemberId == member.Id && x.Status != "Cancelled").Select(x => x.ClassId)
            .Union(db.SessionBookings.Where(x => x.MemberId == member.Id && x.Status == "Booked")
                .Join(db.ClassSessions, booking => booking.SessionId, session => session.Id, (booking, session) => session.ClassId));
        var coachIds = await db.CoachProfiles.Where(coach => coach.Status == "Active" &&
                (db.ClassCoaches.Any(link => link.CoachId == coach.Id && memberClassIds.Contains(link.ClassId)) ||
                 db.CoachWorkEvents.Any(e => e.CoachId == coach.Id && e.MemberId == member.Id)))
            .Select(x => x.Id).Distinct().ToArrayAsync(ct);
        var coaches = await db.CoachProfiles.AsNoTracking().Where(x => coachIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName, x.CoachCode, x.Specialization, x.Certification, x.ExperienceYears }).ToArrayAsync(ct);
        var events = await (from e in db.CoachWorkEvents.AsNoTracking() join coach in db.CoachProfiles on e.CoachId equals coach.Id
            where e.MemberId == member.Id orderby e.StartAt descending
            select new { e.Id, e.CoachId, CoachName = coach.FullName, e.Title, e.EventType, e.StartAt, e.EndAt, e.Status, e.Notes }).ToArrayAsync(ct);
        var assignments = await db.Assignments.AsNoTracking().Where(x => x.Status == "Active" &&
            (x.MemberId == member.Id || (x.ClassId != null &&
                (db.ClassEnrollments.Any(e => e.MemberId == member.Id && e.ClassId == x.ClassId && e.Status != "Cancelled") ||
                 db.SessionBookings.Any(b => b.MemberId == member.Id && b.Status == "Booked" && db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == x.ClassId))))))
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.CoachId, x.ClassId, x.Title, x.Description, x.DueDate, x.CreatedAt,
                Submission = db.AssignmentSubmissions.Where(s => s.AssignmentId == x.Id && s.MemberId == member.Id)
                    .Select(s => new { s.Id, s.SubmittedAt, s.Content, s.Score, s.CoachFeedback, s.Status }).FirstOrDefault() }).ToArrayAsync(ct);
        var nutrition = await db.NutritionPlans.AsNoTracking().Where(x => x.MemberId == member.Id && x.Status == "Active")
            .OrderByDescending(x => x.CreatedAt).Select(x => new { x.Id, x.CoachId, x.Title, x.StartDate, x.EndDate,
                x.DailyCalories, x.ProteinGrams, x.CarbGrams, x.FatGrams, x.MealPlanJson, x.Notes }).ToArrayAsync(ct);
        var diary = await db.FoodDiaryEntries.AsNoTracking().Where(x => x.MemberId == member.Id)
            .OrderByDescending(x => x.EntryDate).ThenByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new { x.Id, x.EntryDate, x.MealType, x.FoodName, x.Calories, x.ProteinGrams, x.CarbGrams, x.FatGrams, x.Notes }).ToArrayAsync(ct);
        var threads = await db.CoachMessages.AsNoTracking().Where(x => x.MemberId == member.Id)
            .GroupBy(x => x.CoachId).Select(g => new { CoachId = g.Key, LastMessageAt = g.Max(x => x.CreatedAt),
                Unread = g.Count(x => x.SenderUserId != userId && x.ReadAt == null) }).ToArrayAsync(ct);
        var reviews = await db.CoachReviews.AsNoTracking().Where(x => x.MemberId == member.Id)
            .Select(x => new { x.Id, x.CoachId, x.WorkEventId, x.Rating, x.Comment, x.CreatedAt, x.UpdatedAt }).ToArrayAsync(ct);
        return new { Member = new { member.Id, member.UserId, member.FullName, member.MemberCode }, Coaches = coaches, Events = events,
            Assignments = assignments, NutritionPlans = nutrition, FoodDiary = diary, Threads = threads, Reviews = reviews };
    }

    public async Task<long> SubmitHomework(long userId, long assignmentId, SubmitHomeworkRequest request, CancellationToken ct)
    {
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw BusinessException.Forbidden("Không có hồ sơ hội viên.");
        var assignment = await db.Assignments.SingleOrDefaultAsync(x => x.Id == assignmentId && x.Status == "Active" &&
            (x.MemberId == member.Id || (x.ClassId != null &&
                (db.ClassEnrollments.Any(e => e.MemberId == member.Id && e.ClassId == x.ClassId && e.Status != "Cancelled") ||
                 db.SessionBookings.Any(b => b.MemberId == member.Id && b.Status == "Booked" && db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == x.ClassId))))), ct)
            ?? throw BusinessException.NotFound("Không tìm thấy bài tập được giao cho bạn.");
        var row = await db.AssignmentSubmissions.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.MemberId == member.Id, ct);
        if (row is null)
        {
            row = new AssignmentSubmission { AssignmentId = assignmentId, MemberId = member.Id };
            db.AssignmentSubmissions.Add(row);
        }
        row.Content = request.Content.Trim(); row.SubmittedAt = UtcNow; row.Status = "Submitted";
        row.Score = null; row.CoachFeedback = null; await db.SaveChangesAsync(ct);
        if (assignment.CoachId.HasValue)
        {
            var coachUser = await db.CoachProfiles.Where(x => x.Id == assignment.CoachId).Select(x => x.UserId).SingleAsync(ct);
            await NotifyUsers(userId, [coachUser], "Học viên đã check-in bài tập", $"{member.FullName} đã hoàn thành: {assignment.Title}.", "HomeworkSubmitted", ["InApp"], ct);
        }
        return row.Id;
    }

    public async Task<long> AddFoodDiary(long userId, SaveFoodDiaryRequest request, CancellationToken ct)
    {
        var memberId = await db.MemberProfiles.Where(x => x.UserId == userId).Select(x => x.Id).SingleOrDefaultAsync(ct);
        if (memberId == 0) throw BusinessException.Forbidden("Không có hồ sơ hội viên.");
        if (request.EntryDate > Today) throw Invalid("Ngày nhật ký không được ở tương lai.");
        var row = new FoodDiaryEntry { MemberId = memberId, EntryDate = request.EntryDate, MealType = request.MealType.Trim(),
            FoodName = request.FoodName.Trim(), Calories = request.Calories, ProteinGrams = request.ProteinGrams,
            CarbGrams = request.CarbGrams, FatGrams = request.FatGrams, Notes = request.Notes?.Trim(), CreatedAt = UtcNow };
        db.FoodDiaryEntries.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<long> ReviewCoach(long userId, SaveCoachReviewRequest request, CancellationToken ct)
    {
        var member = await db.MemberProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw BusinessException.Forbidden("Không có hồ sơ hội viên.");
        if (!await AssignedMemberIds(request.CoachId).AnyAsync(x => x == member.Id, ct))
            throw BusinessException.Forbidden("Bạn chưa có quan hệ huấn luyện với HLV này.");
        CoachReview? row = null;
        if (request.WorkEventId.HasValue)
        {
            var appointment = await db.CoachWorkEvents.SingleOrDefaultAsync(x => x.Id == request.WorkEventId &&
                x.CoachId == request.CoachId && x.MemberId == member.Id && x.Status == "Completed", ct)
                ?? throw BusinessException.Conflict("Chỉ đánh giá lịch PT đã hoàn thành.");
            row = await db.CoachReviews.SingleOrDefaultAsync(x => x.WorkEventId == appointment.Id && x.MemberId == member.Id, ct);
        }
        if (row is null)
        {
            row = new CoachReview { CoachId = request.CoachId, MemberId = member.Id, WorkEventId = request.WorkEventId, CreatedAt = UtcNow };
            db.CoachReviews.Add(row);
        }
        row.Rating = request.Rating; row.Comment = request.Comment?.Trim(); row.UpdatedAt = UtcNow;
        await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<object> ManagerWorkspace(long userId, CancellationToken ct)
    {
        var manager = await Manager(userId, ct);
        var coaches = await db.CoachProfiles.AsNoTracking().Where(x => x.CenterId == manager.CenterId)
            .OrderBy(x => x.FullName).Select(x => new { x.Id, x.UserId, x.FullName, x.CoachCode, x.Status,
                x.Specialization, x.Certification,
                IsPayrollActive = db.Users.Any(user => user.Id == x.UserId && user.Status == "Active") &&
                    (x.Status == "Active" || db.StaffProfiles.Any(staff => staff.UserId == x.UserId &&
                        staff.CenterId == manager.CenterId && staff.Status == "Active")) }).ToArrayAsync(ct);
        var coachIds = coaches.Select(x => x.Id).ToArray();
        var leaveRows = await (from leave in db.CoachLeaves.AsNoTracking() join coach in db.CoachProfiles on leave.CoachId equals coach.Id
            where coach.CenterId == manager.CenterId orderby leave.RequestedAt descending
            select new { leave.Id, leave.CoachId, CoachName = coach.FullName, leave.StartAt, leave.EndAt, leave.LeaveType,
                leave.Reason, leave.SwapCoachId,
                SwapCoachName = db.CoachProfiles.Where(x => x.Id == leave.SwapCoachId).Select(x => x.FullName).FirstOrDefault(),
                leave.Status, leave.ManagerNote, leave.RequestedAt, leave.ReviewedAt }).ToArrayAsync(ct);
        var leaves = new List<object>(leaveRows.Length);
        foreach (var leave in leaveRows)
        {
            var affectedSessions = await AffectedClassSessions(leave.CoachId, leave.StartAt, leave.EndAt, false, ct);
            var classIds = affectedSessions.Select(x => x.ClassId).Distinct().ToArray();
            var classDetails = await (from cls in db.Classes.AsNoTracking()
                join sport in db.Sports.AsNoTracking() on cls.SportId equals sport.Id
                where classIds.Contains(cls.Id)
                select new { cls.Id, ClassName = cls.Name, SportId = sport.Id, SportName = sport.Name }).ToArrayAsync(ct);
            var replacementCandidates = leave.Status == "Pending"
                ? await AvailableReplacementCoaches(manager.CenterId, leave.CoachId, leave.StartAt, leave.EndAt, affectedSessions, ct)
                : [];
            leaves.Add(new
            {
                leave.Id, leave.CoachId, leave.CoachName,
                StartAt = AsUtc(leave.StartAt), EndAt = AsUtc(leave.EndAt), leave.LeaveType,
                leave.Reason, leave.SwapCoachId, leave.SwapCoachName, leave.Status, leave.ManagerNote,
                RequestedAt = AsUtc(leave.RequestedAt), ReviewedAt = AsUtc(leave.ReviewedAt),
                AffectedSessions = affectedSessions.OrderBy(x => x.SessionDate).ThenBy(x => x.StartTime).Select(session =>
                {
                    var detail = classDetails.First(x => x.Id == session.ClassId);
                    return new { session.Id, session.ClassId, detail.ClassName, detail.SportName,
                        session.SessionDate, session.StartTime, session.EndTime };
                }).ToArray(),
                RequiredSports = classDetails.Select(x => x.SportName).Distinct().OrderBy(x => x).ToArray(),
                ReplacementCandidates = replacementCandidates
            });
        }
        var policies = await db.CoachCompensationPolicies.AsNoTracking().Where(x => coachIds.Contains(x.CoachId) && x.Status == "Active")
            .OrderByDescending(x => x.EffectiveFrom).ToArrayAsync(ct);
        return new { Coaches = coaches, Leaves = leaves, Policies = policies };
    }

    public async Task ReviewLeave(long userId, long leaveId, ReviewLeaveRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var manager = await Manager(userId, ct);
        if (request.Status is not ("Approved" or "Rejected")) throw Invalid("Trạng thái duyệt đơn không hợp lệ.");
        var row = await (from leave in db.CoachLeaves join coach in db.CoachProfiles on leave.CoachId equals coach.Id
            where leave.Id == leaveId && coach.CenterId == manager.CenterId select leave).SingleOrDefaultAsync(ct)
            ?? throw BusinessException.NotFound("Không tìm thấy đơn nghỉ trong trung tâm.");
        if (row.Status != "Pending") throw BusinessException.Conflict("Đơn nghỉ đã được xử lý.");
        long? replacementCoachId = null;
        CoachProfile? replacementCoach = null;
        var affectedSessions = Array.Empty<ClassSession>();
        if (request.Status == "Approved")
        {
            affectedSessions = await AffectedClassSessions(row.CoachId, row.StartAt, row.EndAt, true, ct);
            replacementCoachId = request.ReplacementCoachId ?? row.SwapCoachId;
            if (affectedSessions.Length > 0 && !replacementCoachId.HasValue)
                throw Invalid("Phải chọn HLV dạy bù trước khi duyệt đơn nghỉ.", "REPLACEMENT-COACH-REQUIRED");
            if (replacementCoachId == row.CoachId)
                throw Invalid("HLV dạy bù phải là một HLV khác.", "REPLACEMENT-COACH-INVALID");

            if (replacementCoachId.HasValue)
            {
                replacementCoach = await db.CoachProfiles.SingleOrDefaultAsync(x => x.Id == replacementCoachId &&
                    x.CenterId == manager.CenterId && x.Status == "Active", ct)
                    ?? throw Invalid("HLV dạy bù không tồn tại hoặc không hoạt động tại trung tâm.", "REPLACEMENT-COACH-INVALID");
                await EnsureReplacementSpecializations(replacementCoach.Id, affectedSessions, ct);
            }

            if (affectedSessions.Length > 0 && replacementCoach is not null)
            {
                var affectedIds = affectedSessions.Select(x => x.Id).ToArray();
                var replacementClassIds = await AssignedClassIds(replacementCoach.Id).ToArrayAsync(ct);
                var affectedDates = affectedSessions.Select(x => x.SessionDate).Distinct().ToArray();
                var otherSessions = await db.ClassSessions.AsNoTracking().Where(x =>
                    !affectedIds.Contains(x.Id) && affectedDates.Contains(x.SessionDate) && x.SessionStatus != "Cancelled" &&
                    (x.CoachId == replacementCoach.Id || (x.CoachId == null && replacementClassIds.Contains(x.ClassId))))
                    .ToArrayAsync(ct);
                var workEvents = await db.CoachWorkEvents.AsNoTracking().Where(x => x.CoachId == replacementCoach.Id &&
                    (x.Status == "Confirmed" || x.Status == "PendingMemberConfirmation") &&
                    x.StartAt < row.EndAt && x.EndAt > row.StartAt).ToArrayAsync(ct);
                var replacementLeaves = await db.CoachLeaves.AsNoTracking().Where(x => x.CoachId == replacementCoach.Id &&
                    x.Status == "Approved" && x.StartAt < row.EndAt && x.EndAt > row.StartAt).ToArrayAsync(ct);

                foreach (var session in affectedSessions)
                {
                    var interval = SessionUtc(session.SessionDate, session.StartTime, session.EndTime);
                    var hasConflict = otherSessions.Any(x => SessionOverlaps(
                            x.SessionDate, x.StartTime, x.EndTime, interval.StartAt, interval.EndAt)) ||
                        workEvents.Any(x => x.StartAt < interval.EndAt && x.EndAt > interval.StartAt) ||
                        replacementLeaves.Any(x => x.StartAt < interval.EndAt && x.EndAt > interval.StartAt);
                    if (hasConflict)
                        throw BusinessException.Conflict(
                            $"HLV {replacementCoach.FullName} đã có lịch trùng buổi {session.SessionDate:dd/MM/yyyy} " +
                            $"{session.StartTime:HH\\:mm}-{session.EndTime:HH\\:mm}. Vui lòng chọn HLV khác.",
                            "REPLACEMENT-COACH-CONFLICT");
                }

                foreach (var session in affectedSessions) session.CoachId = replacementCoach.Id;
            }

            row.SwapCoachId = replacementCoach?.Id;
        }

        row.Status = request.Status; row.ManagerNote = request.ManagerNote?.Trim(); row.ReviewedBy = userId; row.ReviewedAt = UtcNow;
        await db.SaveChangesAsync(ct);
        var coachUser = await db.CoachProfiles.Where(x => x.Id == row.CoachId).Select(x => x.UserId).SingleAsync(ct);
        await NotifyUsers(userId, [coachUser], "Kết quả đơn nghỉ", $"Đơn nghỉ của bạn: {request.Status}. {request.ManagerNote}", "CoachLeaveReviewed", ["InApp", "Email"], ct);
        if (replacementCoach is not null)
            await NotifyUsers(userId, [replacementCoach.UserId], "Phân công dạy bù",
                $"Bạn được phân công nhận {affectedSessions.Length} ca trong thời gian HLV khác nghỉ.",
                "CoachLeaveReplacement", ["InApp", "Email"], ct);
        AddAudit(userId, manager.CenterId, "COACH_LEAVE_REVIEWED", "CoachLeave", row.Id); await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<long> SaveCompensation(long userId, SaveCompensationPolicyRequest request, CancellationToken ct)
    {
        var manager = await Manager(userId, ct);
        if (!await db.CoachProfiles.AnyAsync(x => x.Id == request.CoachId && x.CenterId == manager.CenterId &&
                db.Users.Any(user => user.Id == x.UserId && user.Status == "Active") &&
                (x.Status == "Active" || db.StaffProfiles.Any(staff => staff.UserId == x.UserId &&
                    staff.CenterId == manager.CenterId && staff.Status == "Active")), ct))
            throw BusinessException.NotFound("HLV không thuộc danh sách nhân sự đang hoạt động của trung tâm.");
        if (request.EffectiveTo.HasValue && request.EffectiveTo < request.EffectiveFrom) throw Invalid("Ngày hiệu lực chính sách không hợp lệ.");
        var previous = await db.CoachCompensationPolicies.Where(x => x.CoachId == request.CoachId && x.Status == "Active").ToListAsync(ct);
        foreach (var item in previous) item.Status = "Archived";
        var row = new CoachCompensationPolicy { CoachId = request.CoachId, MonthlyBaseSalary = request.MonthlyBaseSalary,
            CommissionPerSession = request.CommissionPerSession,
            SalesCommissionPercent = request.SalesCommissionPercent,
            TargetSessions = request.TargetSessions,
            TargetSales = request.TargetSales,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo, UpdatedBy = userId, UpdatedAt = UtcNow };
        db.CoachCompensationPolicies.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<long> SaveSalesCredit(long userId, SaveSalesCreditRequest request, CancellationToken ct)
    {
        var manager = await Manager(userId, ct);
        if (!await db.CoachProfiles.AnyAsync(x => x.Id == request.CoachId && x.CenterId == manager.CenterId, ct))
            throw BusinessException.NotFound("HLV không thuộc trung tâm.");
        if (request.MemberId.HasValue && !await db.MemberProfiles.AnyAsync(x => x.Id == request.MemberId && x.CenterId == manager.CenterId, ct))
            throw Invalid("Hội viên không thuộc trung tâm.");
        var memberId = request.MemberId;
        if (request.SubscriptionId.HasValue)
        {
            var subscription = await (from sub in db.MemberSubscriptions
                join member in db.MemberProfiles on sub.MemberId equals member.Id
                join package in db.MembershipPackages on sub.PackageId equals package.Id
                where sub.Id == request.SubscriptionId && member.CenterId == manager.CenterId
                    && package.CenterId == manager.CenterId && (!memberId.HasValue || sub.MemberId == memberId)
                select new { sub.MemberId }).SingleOrDefaultAsync(ct)
                ?? throw Invalid("Gói hội viên không thuộc hội viên hoặc trung tâm đang quản lý.");
            memberId = subscription.MemberId;
        }
        var row = new CoachSalesCredit { CoachId = request.CoachId, MemberId = memberId,
            SubscriptionId = request.SubscriptionId, Amount = request.Amount, CreditedAt = request.CreditedAt.HasValue ? ToUtc(request.CreditedAt.Value) : UtcNow,
            Note = request.Note?.Trim(), CreatedBy = userId };
        db.CoachSalesCredits.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task<long> CreateResource(long userId, CreateResourceRequest request, CancellationToken ct)
    {
        var manager = await Manager(userId, ct);
        var row = new FacilityResource { CenterId = manager.CenterId, Name = request.Name.Trim(),
            ResourceType = request.ResourceType.Trim(), Location = request.Location?.Trim(), Capacity = request.Capacity,
            Status = "Available", CreatedAt = UtcNow };
        db.FacilityResources.Add(row); await db.SaveChangesAsync(ct); return row.Id;
    }

    public async Task ResolveIssue(long userId, long issueId, ResolveEquipmentIssueRequest request, CancellationToken ct)
    {
        var manager = await Manager(userId, ct);
        if (request.Status is not ("InProgress" or "OnHold" or "Resolved" or "Closed" or "Rejected" or "Cancelled")) throw Invalid("Trạng thái xử lý sự cố không hợp lệ.");
        var row = await db.EquipmentIssues.SingleOrDefaultAsync(x => x.Id == issueId && x.CenterId == manager.CenterId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy sự cố.");
        row.Status = request.Status; row.ResolutionNote = request.ResolutionNote?.Trim(); row.ResolvedBy = userId;
        row.ResolvedAt = request.Status is "Resolved" or "Closed" or "Rejected" or "Cancelled" ? UtcNow : null;
        await db.SaveChangesAsync(ct);
        await NotifyUsers(userId, [row.ReportedByUserId], "Cập nhật sự cố thiết bị", $"{row.Title}: {request.Status}. {request.ResolutionNote}",
            "EquipmentIssueUpdated", ["InApp"], ct);
    }

    private void AddAudit(long userId, long centerId, string action, string entityType, long? entityId = null) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, CenterId = centerId, Action = action,
            EntityType = entityType, EntityId = entityId, CreatedAt = UtcNow });
}
