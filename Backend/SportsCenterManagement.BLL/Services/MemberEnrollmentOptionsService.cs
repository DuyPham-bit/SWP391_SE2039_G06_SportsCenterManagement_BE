using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.BLL.Services.Interfaces;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Builds the sport → coach → class choices for member self-enrollment. Eligibility mirrors
/// <see cref="CoreFlowService.EnrollMemberAsync"/> (active package for today, package allowance, capacity),
/// which stays the authority when the member confirms.
/// </summary>
public sealed class MemberEnrollmentOptionsService(SportsCenterDbContext db, IBusinessClock clock)
    : IMemberEnrollmentOptionsService
{
    private static readonly string[] JoinedStatuses = ["Confirmed", "Active", "Enrolled"];

    public async Task<MemberEnrollmentOptionsDto> GetOptionsAsync(long userId, long centerId, CancellationToken ct = default)
    {
        var member = await db.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct)
            ?? throw BusinessException.NotFound("Không tìm thấy hồ sơ hội viên.");
        if (member.CenterId.HasValue && member.CenterId.Value != centerId)
            throw BusinessException.Forbidden("Bạn chỉ có thể đăng ký lớp tại trung tâm của mình.");
        if (!await db.Centers.AnyAsync(x => x.Id == centerId && x.Status == "Active", ct))
            throw BusinessException.NotFound("Trung tâm không tồn tại hoặc đã ngừng hoạt động.");

        var today = clock.Today;
        var nowUtc = clock.UtcNow;

        // Published classes of active sports that have an active primary coach.
        var classes = await (
            from cls in db.Classes.AsNoTracking()
            join sport in db.Sports on cls.SportId equals sport.Id
            join assignment in db.ClassCoaches on cls.Id equals assignment.ClassId
            join coach in db.CoachProfiles on assignment.CoachId equals coach.Id
            join coachUser in db.Users on coach.UserId equals coachUser.Id
            where cls.CenterId == centerId && cls.Status == "Published" && cls.Capacity > 0
                && sport.Status == "Active" && (sport.CenterId == null || sport.CenterId == centerId)
                && assignment.IsPrimary && coach.CenterId == centerId && coach.Status == "Active"
                && coachUser.Status == "Active" && (coachUser.LockedUntil == null || coachUser.LockedUntil <= nowUtc)
            select new ClassRow(cls.Id, cls.Name, cls.Level, cls.Description, cls.Capacity, cls.RoomId,
                sport.Id, sport.Name, sport.Description,
                coach.Id, coach.FullName, coach.Specialization, coach.Certification, coach.ExperienceYears, coach.Bio)
            ).ToListAsync(ct);
        var classIds = classes.Select(x => x.ClassId).Distinct().ToArray();
        var coachIds = classes.Select(x => x.CoachId).Distinct().ToArray();

        var rooms = await db.Rooms.AsNoTracking().Where(x => x.CenterId == centerId)
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var schedules = await db.ClassSchedules.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassId) && x.Status == "Active" && (x.EndDate == null || x.EndDate >= today))
            .OrderBy(x => x.DayOfWeek).ThenBy(x => x.StartTime)
            .ToListAsync(ct);
        var confirmedCounts = await db.ClassEnrollments.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassId) && x.Status == "Confirmed")
            .GroupBy(x => x.ClassId).Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassId, x => x.Count, ct);
        var ratings = await db.CoachReviews.AsNoTracking().Where(x => coachIds.Contains(x.CoachId))
            .GroupBy(x => x.CoachId).Select(g => new { CoachId = g.Key, Average = g.Average(r => (double)r.Rating), Count = g.Count() })
            .ToDictionaryAsync(x => x.CoachId, ct);
        var myEnrollments = await db.ClassEnrollments.AsNoTracking()
            .Where(x => x.MemberId == member.Id && JoinedStatuses.Contains(x.Status))
            .ToDictionaryAsync(x => x.ClassId, x => x.Id, ct);

        // Packages usable today at this center, ordered like the booking rules (widest sport access first).
        var subscriptions = await (
            from subscription in db.MemberSubscriptions.AsNoTracking()
            join package in db.MembershipPackages on subscription.PackageId equals package.Id
            where subscription.MemberId == member.Id && subscription.Status == "Active"
                && subscription.StartDate <= today && subscription.EndDate >= today && package.CenterId == centerId
            orderby package.AllowedSports descending, subscription.EndDate descending
            select new { subscription.Id, PackageName = package.Name, subscription.StartDate, subscription.EndDate,
                package.AllowedSports, package.MaxClasses }).ToListAsync(ct);
        var subscriptionIds = subscriptions.Select(x => x.Id).ToArray();
        var enrolledUsage = await (
            from enrollment in db.ClassEnrollments.AsNoTracking()
            join cls in db.Classes on enrollment.ClassId equals cls.Id
            where enrollment.MemberId == member.Id && enrollment.Status == "Confirmed"
                && enrollment.SubscriptionId != null && subscriptionIds.Contains(enrollment.SubscriptionId.Value)
            select new { SubscriptionId = enrollment.SubscriptionId!.Value, enrollment.ClassId, cls.SportId }).ToListAsync(ct);
        var bookedUsage = await (
            from booking in db.SessionBookings.AsNoTracking()
            join session in db.ClassSessions on booking.SessionId equals session.Id
            join cls in db.Classes on session.ClassId equals cls.Id
            where booking.MemberId == member.Id && (booking.Status == "Booked" || booking.Status == "CANCELLED_LATE_CHARGED")
                && booking.SubscriptionId != null && subscriptionIds.Contains(booking.SubscriptionId.Value)
            select new { SubscriptionId = booking.SubscriptionId!.Value, session.ClassId, cls.SportId }).ToListAsync(ct);
        var usage = enrolledUsage.Concat(bookedUsage)
            .GroupBy(x => x.SubscriptionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<(long ClassId, long SportId)>)g
                .DistinctBy(x => x.ClassId).Select(x => (x.ClassId, x.SportId)).ToArray());
        IReadOnlyCollection<(long ClassId, long SportId)> UsageOf(long subscriptionId) =>
            usage.TryGetValue(subscriptionId, out var used) ? used : [];

        EnrollmentClassOptionDto BuildClass(ClassRow row)
        {
            var classId = row.ClassId;
            var sportId = row.SportId;
            var capacity = row.Capacity;
            var roomId = row.RoomId;
            var enrolled = confirmedCounts.GetValueOrDefault(classId);
            var classSchedules = schedules.Where(s => s.ClassId == classId)
                .Select(s => new EnrollmentScheduleDto(s.DayOfWeek, s.StartTime, s.EndTime,
                    rooms.GetValueOrDefault(s.RoomId ?? roomId ?? 0)))
                .ToArray();
            var roomName = roomId.HasValue ? rooms.GetValueOrDefault(roomId.Value) : null;

            long? enrollmentId = myEnrollments.TryGetValue(classId, out var id) ? id : null;
            long? eligible = null;
            string? blockedCode = null, blockedReason = null;
            if (enrollmentId is null)
            {
                if (enrolled >= capacity)
                    (blockedCode, blockedReason) = ("CLASS_FULL", "Lớp đã đủ chỗ.");
                else if (subscriptions.Count == 0)
                    (blockedCode, blockedReason) = ("NO_PACKAGE", "Bạn cần có gói tập còn hiệu lực để đăng ký lớp.");
                else
                {
                    var verdicts = subscriptions.Select(s => (s.Id, Verdict: EnrollmentEntitlementRules.Evaluate(
                        s.MaxClasses, s.AllowedSports, UsageOf(s.Id), classId, sportId))).ToArray();
                    var allowed = verdicts.FirstOrDefault(v => v.Verdict == EnrollmentEntitlementVerdict.Allowed);
                    if (allowed.Id != 0) eligible = allowed.Id;
                    else if (verdicts.Any(v => v.Verdict == EnrollmentEntitlementVerdict.SportLimitReached))
                        (blockedCode, blockedReason) = ("SPORT_LIMIT", "Gói tập đã dùng hết số môn được phép. Nâng cấp gói để học thêm môn mới.");
                    else
                        (blockedCode, blockedReason) = ("CLASS_LIMIT", "Gói tập đã dùng hết số lớp được phép.");
                }
            }

            return new EnrollmentClassOptionDto(classId, row.Name, row.Level, row.Description, roomName, capacity,
                enrolled, classSchedules, enrollmentId, eligible, blockedCode, blockedReason);
        }

        var sports = classes
            .GroupBy(x => new { x.SportId, x.SportName, x.SportDescription })
            .OrderBy(g => g.Key.SportName)
            .Select(sportGroup => new EnrollmentSportOptionDto(
                sportGroup.Key.SportId,
                sportGroup.Key.SportName,
                sportGroup.Key.SportDescription,
                sportGroup.Any(x => myEnrollments.ContainsKey(x.ClassId)),
                sportGroup
                    .GroupBy(x => new { x.CoachId, x.FullName, x.Specialization, x.Certification, x.ExperienceYears, x.Bio })
                    .Select(coachGroup =>
                    {
                        var rating = ratings.GetValueOrDefault(coachGroup.Key.CoachId);
                        return new EnrollmentCoachOptionDto(
                            coachGroup.Key.CoachId, coachGroup.Key.FullName, coachGroup.Key.Specialization,
                            coachGroup.Key.Certification, coachGroup.Key.ExperienceYears, coachGroup.Key.Bio,
                            rating is null ? null : Math.Round(rating.Average, 1), rating?.Count ?? 0,
                            coachGroup.DistinctBy(x => x.ClassId).OrderBy(x => x.Name)
                                .Select(BuildClass).ToArray());
                    })
                    .OrderByDescending(c => c.AverageRating ?? 0).ThenBy(c => c.FullName)
                    .ToArray()))
            .ToArray();

        var subscriptionSummaries = subscriptions.Select(s =>
        {
            var used = UsageOf(s.Id);
            return new EnrollmentSubscriptionDto(s.Id, s.PackageName, s.StartDate, s.EndDate, s.AllowedSports,
                s.MaxClasses, used.Select(x => x.SportId).Distinct().OrderBy(x => x).ToArray(), used.Count);
        }).ToArray();

        return new MemberEnrollmentOptionsDto(centerId, subscriptionSummaries, sports);
    }

    private sealed record ClassRow(
        long ClassId, string Name, string? Level, string? Description, int Capacity, long? RoomId,
        long SportId, string SportName, string? SportDescription,
        long CoachId, string FullName, string? Specialization, string? Certification, int? ExperienceYears, string? Bio);
}
