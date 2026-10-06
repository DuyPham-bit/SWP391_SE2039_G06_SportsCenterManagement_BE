using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
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

        // Kiểm tra chuyên môn của HLV có phù hợp với bộ môn của lớp học
        var sport = await unitOfWork.Repository<Sport>().GetByIdAsync(classEntity.SportId, cancellationToken);
        if (sport is not null)
        {
            if (string.IsNullOrWhiteSpace(coachProfile.Specialization) ||
                !coachProfile.Specialization.Contains(sport.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Huấn luyện viên {coachProfile.FullName} (Chuyên môn: '{coachProfile.Specialization}') " +
                    $"không có chứng chỉ/chuyên môn phù hợp với môn học '{sport.Name}' của lớp.");
            }
        }

        // Lấy danh sách lịch học tươi từ DB cho lớp cần phân công
        var targetSchedules = await unitOfWork.Repository<ClassSchedule>()
            .Find(s => s.ClassId == classId && s.Status == "Active")
            .ToListAsync(cancellationToken);
        if (targetSchedules.Count == 0)
        {
            throw new InvalidOperationException("Lớp học cần có lịch dạy đang hoạt động trước khi phân công HLV.");
        }

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

        // 3. Nếu phân công HLV chính (IsPrimary = true), tự động gián cấp các HLV chính khác trong lớp thành HLV phụ
        var anotherCoachAssigned = await db.ClassCoaches
            .AnyAsync(cc => cc.ClassId == classId && cc.CoachId != request.CoachId, cancellationToken);
        if (anotherCoachAssigned)
        {
            throw new InvalidOperationException("Lớp học đã có HLV được phân công. Hãy gỡ HLV hiện tại trước khi thay thế.");
        }

        // 4. Thực hiện gán HLV vào lớp
        var existingClassCoach = await unitOfWork.Repository<ClassCoach>()
            .Find(cc => cc.ClassId == classId && cc.CoachId == request.CoachId)
            .SingleOrDefaultAsync(cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
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
        long classId,
        long coachId,
        CancellationToken cancellationToken = default)
    {
        var classCoach = await unitOfWork.Repository<ClassCoach>()
            .Find(cc => cc.ClassId == classId && cc.CoachId == coachId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Huấn luyện viên chưa được phân công vào lớp học này.");

        unitOfWork.Repository<ClassCoach>().Remove(classCoach);
        await unitOfWork.SaveChangesAsync(cancellationToken);
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

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
