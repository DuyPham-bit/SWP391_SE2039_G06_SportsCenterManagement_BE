using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassService(IUnitOfWork unitOfWork) : IClassService
{
    private const double MaxDailyTeachingHours = 8.0;

    public async Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.Repository<ClassEntity>()
            .Find(item => item.CenterId == centerId && item.Status == "Published")
            .OrderBy(item => item.Name)
            .Select(item => new ClassCatalogResponse(
                item.Id,
                item.CenterId,
                item.SportId,
                item.RoomId,
                item.Name,
                item.Description,
                item.Level,
                item.Capacity,
                item.DurationMinutes))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClassCoachResponse> AssignCoachToClassAsync(
        long actorUserId,
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var classEntity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.");

        if (string.Equals(classEntity.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(classEntity.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Không thể phân công HLV cho lớp học đã ở trạng thái '{classEntity.Status}'.");
        }

        var coachProfile = await unitOfWork.Repository<CoachProfile>().GetByIdAsync(request.CoachId, cancellationToken)
            ?? throw new InvalidOperationException("Huấn luyện viên không tồn tại.");

        var coachUser = await unitOfWork.Repository<User>().GetByIdAsync(coachProfile.UserId, cancellationToken);
        if (!string.Equals(coachProfile.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            coachUser is null ||
            !string.Equals(coachUser.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
            (coachUser.LockedUntil.HasValue && coachUser.LockedUntil.Value > DateTime.UtcNow))
        {
            throw new InvalidOperationException(
                $"Tài khoản hoặc hồ sơ của Huấn luyện viên {coachProfile.FullName} không ở trạng thái hoạt động (Trạng thái: {coachProfile.Status}).");
        }

        if (coachProfile.CenterId != classEntity.CenterId)
        {
            throw new InvalidOperationException("Huấn luyện viên không thuộc cùng cơ sở với lớp học.");
        }

        // Chuyên môn là quan hệ Coach-Sport được Manager chọn từ catalog, không so khớp chuỗi tự do.
        var sport = await unitOfWork.Repository<Sport>().GetByIdAsync(classEntity.SportId, cancellationToken);
        var hasSpecialization = await unitOfWork.Context.CoachSports.AnyAsync(
            item => item.CoachId == coachProfile.Id && item.SportId == classEntity.SportId, cancellationToken);
        if (!hasSpecialization)
            throw new InvalidOperationException(
                $"Huấn luyện viên {coachProfile.FullName} không có chuyên môn '{sport?.Name ?? "không xác định"}' của lớp.")
            { Data = { ["StatusCode"] = 409, ["Code"] = "COACH_SPECIALIZATION_MISMATCH" } };

        // A draft class may be assigned a coach before its first schedule is created.
        // When schedules are later added/changed, ClassManagementService performs the
        // same conflict/leave checks against every coach already assigned to the class.
        var targetSchedules = await unitOfWork.Repository<ClassSchedule>()
            .Find(s => s.ClassId == classId && s.Status == "Active")
            .ToListAsync(cancellationToken);

        // Lấy danh sách các lớp khác mà HLV này đang phụ trách
        var otherClassIds = await unitOfWork.Repository<ClassCoach>()
            .Find(cc => cc.CoachId == request.CoachId && cc.ClassId != classId)
            .Select(cc => cc.ClassId)
            .ToListAsync(cancellationToken);

        var db = unitOfWork.Context;
        var existingSchedules = new List<(ClassSchedule Schedule, string ClassName)>();

        if (otherClassIds.Count > 0)
        {
            var rawSchedules = await (
                from s in db.ClassSchedules
                join c in db.Classes on s.ClassId equals c.Id
                where otherClassIds.Contains(s.ClassId)
                    && s.Status == "Active"
                    && c.Status != "Cancelled"
                    && c.Status != "Completed"
                select new { Schedule = s, ClassName = c.Name }
            ).ToListAsync(cancellationToken);

            existingSchedules = rawSchedules.Select(x => (x.Schedule, x.ClassName)).ToList();
        }

        // 1. Kiểm tra tổng giờ dạy theo từng ngày trong mọi khoảng ngày lịch lặp có hiệu lực.
        EnsureDailyTeachingLimit(targetSchedules, existingSchedules, coachProfile.FullName);

        // Schedules created before a coach was assigned may overlap each other (for example,
        // when they use different rooms). A coach still cannot teach both at the same time.
        for (var leftIndex = 0; leftIndex < targetSchedules.Count; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < targetSchedules.Count; rightIndex++)
            {
                var left = targetSchedules[leftIndex];
                var right = targetSchedules[rightIndex];
                if (left.DayOfWeek == right.DayOfWeek
                    && left.StartTime < right.EndTime && left.EndTime > right.StartTime
                    && DateRangesOverlap(left.StartDate, left.EndDate, right.StartDate, right.EndDate))
                {
                    throw new InvalidOperationException(
                        $"Không thể phân công HLV {coachProfile.FullName}: lớp đang có hai lịch học trùng nhau.")
                    {
                        Data = { ["StatusCode"] = 409, ["Code"] = "CM-04" }
                    };
                }
            }
        }

        // 2. Kiểm tra xung đột trùng lịch dạy (Schedule Overlap Check)
        if (targetSchedules.Count > 0 && existingSchedules.Count > 0)
        {
            foreach (var tSched in targetSchedules)
            {
                foreach (var eSched in existingSchedules)
                {
                    var isSameDay = tSched.DayOfWeek == eSched.Schedule.DayOfWeek;
                    var isTimeOverlap = tSched.StartTime < eSched.Schedule.EndTime && tSched.EndTime > eSched.Schedule.StartTime;
                    var isDateOverlap = DateRangesOverlap(tSched.StartDate, tSched.EndDate, eSched.Schedule.StartDate, eSched.Schedule.EndDate);

                    if (isSameDay && isTimeOverlap && isDateOverlap)
                    {
                        throw new InvalidOperationException(
                            $"Huấn luyện viên {coachProfile.FullName} bị trùng lịch dạy tại lớp '{eSched.ClassName}' " +
                            $"(Thứ {tSched.DayOfWeek}, từ {eSched.Schedule.StartTime} đến {eSched.Schedule.EndTime}).");
                    }
                }
            }
        }

        var approvedLeaves = await unitOfWork.Repository<CoachLeave>()
            .Find(leave => leave.CoachId == coachProfile.Id && leave.Status.ToLower() == "approved")
            .ToListAsync(cancellationToken);
        if (approvedLeaves.Any(leave => targetSchedules.Any(schedule => ScheduleOverlapsLeave(schedule, leave))))
        {
            throw new InvalidOperationException($"HLV {coachProfile.FullName} đã được duyệt nghỉ trong thời gian của lớp học.");
        }

        // A class may have assistant coaches, but it must keep exactly one primary coach.
        var assignments = await db.ClassCoaches.Where(cc => cc.ClassId == classId).ToListAsync(cancellationToken);
        var previousAssignments = assignments.Select(cc => new { cc.CoachId, cc.IsPrimary, cc.AssignedDate }).ToArray();
        var existingClassCoach = assignments.SingleOrDefault(cc => cc.CoachId == request.CoachId);
        if (!request.IsPrimary && !assignments.Any(cc => cc.IsPrimary && cc.CoachId != request.CoachId))
        {
            throw new InvalidOperationException("Lớp phải có một huấn luyện viên chính.");
        }
        if (request.IsPrimary)
        {
            foreach (var assignment in assignments.Where(cc => cc.CoachId != request.CoachId))
                assignment.IsPrimary = false;
        }

        if (request.IsPrimary && request.ReplacePrimary)
        {
            foreach (var formerPrimary in assignments.Where(cc => cc.CoachId != request.CoachId &&
                         previousAssignments.Any(previous => previous.CoachId == cc.CoachId && previous.IsPrimary)))
            {
                unitOfWork.Repository<ClassCoach>().Remove(formerPrimary);
            }
        }

        var today = ScheduleTime.VietnamToday;
        if (existingClassCoach is null)
        {
            existingClassCoach = new ClassCoach
            {
                ClassId = classId,
                CoachId = request.CoachId,
                IsPrimary = request.IsPrimary,
                AssignedDate = today
            };
            await unitOfWork.Repository<ClassCoach>().AddAsync(existingClassCoach, cancellationToken);
        }
        else
        {
            existingClassCoach.IsPrimary = request.IsPrimary;
            existingClassCoach.AssignedDate = today;
        }

        var primaryCoachId = request.IsPrimary
            ? coachProfile.Id
            : assignments.FirstOrDefault(assignment => assignment.IsPrimary)?.CoachId;
        var localToday = ScheduleTime.VietnamToday;
        var localNow = TimeOnly.FromDateTime(ScheduleTime.VietnamNow);
        var upcomingSessions = await db.ClassSessions
            .Where(session => session.ClassId == classId && session.SessionStatus == FlowStatuses.SessionScheduled
                && (session.SessionDate > localToday || (session.SessionDate == localToday && session.StartTime > localNow)))
            .ToListAsync(cancellationToken);
        foreach (var session in upcomingSessions)
            session.CoachId = primaryCoachId;

        AuditLogWriter.Add(unitOfWork, actorUserId, classEntity.CenterId, "CLASS_COACH.ASSIGNED", "Class", classId,
            previousAssignments, new { CoachId = coachProfile.Id, coachProfile.FullName, request.IsPrimary, request.ReplacePrimary });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ClassCoachResponse(
            classId,
            coachProfile.Id,
            coachProfile.FullName,
            coachProfile.CoachCode,
            existingClassCoach.IsPrimary,
            existingClassCoach.AssignedDate,
            coachProfile.Specialization);
    }

    public async Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long classId,
        CancellationToken cancellationToken = default)
    {
        var classExists = await unitOfWork.Repository<ClassEntity>().AnyAsync(c => c.Id == classId, cancellationToken);
        if (!classExists)
        {
            throw new InvalidOperationException("Lớp học không tồn tại.");
        }

        var db = unitOfWork.Context;
        return await (
            from cc in db.ClassCoaches
            join coach in db.CoachProfiles on cc.CoachId equals coach.Id
            where cc.ClassId == classId
            select new ClassCoachResponse(
                cc.ClassId,
                coach.Id,
                coach.FullName,
                coach.CoachCode,
                cc.IsPrimary,
                cc.AssignedDate,
                coach.Specialization)
            ).ToListAsync(cancellationToken);
    }

    public async Task UnassignCoachFromClassAsync(
        long actorUserId,
        long classId,
        long coachId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.Context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var db = unitOfWork.Context;
        var classEntity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.");
        var classCoach = await unitOfWork.Repository<ClassCoach>()
            .Find(cc => cc.ClassId == classId && cc.CoachId == coachId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Huấn luyện viên chưa được phân công vào lớp học này.");

        unitOfWork.Repository<ClassCoach>().Remove(classCoach);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = await db.ClassCoaches.Where(cc => cc.ClassId == classId)
            .OrderBy(cc => cc.AssignedDate).ThenBy(cc => cc.CoachId).ToListAsync(cancellationToken);
        if (remaining.Count == 0 && classEntity.Status == "Published")
        {
            throw new InvalidOperationException("Không thể gỡ huấn luyện viên cuối cùng khỏi lớp đang mở; hãy hủy lớp trước.");
        }
        if (classCoach.IsPrimary && remaining.Count > 0 && !remaining.Any(cc => cc.IsPrimary))
        {
            remaining[0].IsPrimary = true;
        }
        var primaryCoachId = remaining.FirstOrDefault(cc => cc.IsPrimary)?.CoachId;
        var today = ScheduleTime.VietnamToday;
        var now = TimeOnly.FromDateTime(ScheduleTime.VietnamNow);
        var upcomingSessions = await db.ClassSessions
            .Where(session => session.ClassId == classId && session.SessionStatus == FlowStatuses.SessionScheduled
                && (session.SessionDate > today || (session.SessionDate == today && session.StartTime > now)))
            .ToListAsync(cancellationToken);
        foreach (var session in upcomingSessions)
            session.CoachId = primaryCoachId;
        AuditLogWriter.Add(unitOfWork, actorUserId, classEntity.CenterId, "CLASS_COACH.UNASSIGNED", "Class", classId,
            new { classCoach.CoachId, classCoach.IsPrimary }, new { PrimaryCoachId = primaryCoachId });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static bool DateRangesOverlap(DateOnly? start1, DateOnly? end1, DateOnly? start2, DateOnly? end2)
    {
        var s1 = start1 ?? DateOnly.MinValue;
        var e1 = end1 ?? DateOnly.MaxValue;
        var s2 = start2 ?? DateOnly.MinValue;
        var e2 = end2 ?? DateOnly.MaxValue;
        return s1 <= e2 && s2 <= e1;
    }

    private static void EnsureDailyTeachingLimit(
        IReadOnlyCollection<ClassSchedule> targetSchedules,
        IReadOnlyCollection<(ClassSchedule Schedule, string ClassName)> existingSchedules,
        string coachName)
    {
        var today = ScheduleTime.VietnamToday;

        foreach (var dayOfWeek in targetSchedules.Select(schedule => schedule.DayOfWeek).Distinct())
        {
            var targetForDay = targetSchedules.Where(schedule => schedule.DayOfWeek == dayOfWeek).ToList();
            var existingForDay = existingSchedules
                .Where(item => item.Schedule.DayOfWeek == dayOfWeek)
                .Select(item => item.Schedule)
                .ToList();
            var allForDay = targetForDay.Concat(existingForDay).ToList();
            var candidateDates = new HashSet<DateOnly>
            {
                NextOccurrence(today, dayOfWeek)
            };

            foreach (var schedule in allForDay)
            {
                var firstActiveDate = schedule.StartDate.HasValue && schedule.StartDate.Value > today
                    ? schedule.StartDate.Value
                    : today;
                var occurrence = NextOccurrence(firstActiveDate, dayOfWeek);
                if (!schedule.EndDate.HasValue || occurrence <= schedule.EndDate.Value)
                {
                    candidateDates.Add(occurrence);
                }
            }

            foreach (var date in candidateDates)
            {
                var hours = allForDay
                    .Where(schedule => IsScheduledOn(schedule, date))
                    .Sum(schedule => (schedule.EndTime.ToTimeSpan() - schedule.StartTime.ToTimeSpan()).TotalHours);

                if (hours > MaxDailyTeachingHours)
                {
                    throw new InvalidOperationException(
                        $"Việc phân công sẽ làm cho HLV {coachName} dạy {hours:F1} giờ trong ngày " +
                        $"(vượt giới hạn {MaxDailyTeachingHours:F1} giờ/ngày).");
                }
            }
        }
    }

    private static DateOnly NextOccurrence(DateOnly date, int dayOfWeek)
    {
        if (dayOfWeek is < 0 or > 6)
        {
            throw new InvalidOperationException("Lịch lớp có thứ trong tuần không hợp lệ.");
        }

        var daysUntil = (dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntil);
    }

    private static bool IsScheduledOn(ClassSchedule schedule, DateOnly date) =>
        schedule.DayOfWeek == (int)date.DayOfWeek &&
        (!schedule.StartDate.HasValue || schedule.StartDate.Value <= date) &&
        (!schedule.EndDate.HasValue || schedule.EndDate.Value >= date);

    private static bool ScheduleOverlapsLeave(ClassSchedule schedule, CoachLeave leave)
    {
        var leaveStartDate = DateOnly.FromDateTime(leave.StartAt);
        var leaveEndDate = DateOnly.FromDateTime(leave.EndAt);
        var firstDate = schedule.StartDate.HasValue && schedule.StartDate.Value > leaveStartDate
            ? schedule.StartDate.Value
            : leaveStartDate;
        var lastDate = schedule.EndDate.HasValue && schedule.EndDate.Value < leaveEndDate
            ? schedule.EndDate.Value
            : leaveEndDate;

        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            if (schedule.DayOfWeek != (int)date.DayOfWeek) continue;

            var sessionStart = date.ToDateTime(schedule.StartTime);
            var sessionEnd = date.ToDateTime(schedule.EndTime);
            if (sessionStart < leave.EndAt && sessionEnd > leave.StartAt) return true;
        }

        return false;
    }
}
