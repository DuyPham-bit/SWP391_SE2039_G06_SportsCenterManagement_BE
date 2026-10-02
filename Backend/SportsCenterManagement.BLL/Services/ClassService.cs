using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassService(IUnitOfWork unitOfWork) : IClassService
{
    private const double MaxDailyTeachingHours = 8;

    public async Task<IReadOnlyList<ClassCatalogResponse>> GetPublishedClassesAsync(
        long centerId, CancellationToken cancellationToken = default) =>
        await unitOfWork.Repository<ClassEntity>()
            .Find(item => item.CenterId == centerId && item.Status == "Published")
            .OrderBy(item => item.Name)
            .Select(item => new ClassCatalogResponse(item.Id, item.CenterId, item.SportId, item.RoomId,
                item.Name, item.Description, item.Level, item.Capacity, item.DurationMinutes))
            .ToListAsync(cancellationToken);

    public async Task<ClassCoachResponse> AssignCoachToClassAsync(long classId, AssignCoachRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = unitOfWork.Context;
        var classEntity = await db.Classes.SingleOrDefaultAsync(x => x.Id == classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.");
        if (IsState(classEntity.Status, "Cancelled", "Completed"))
            throw new InvalidOperationException("Không thể phân công HLV cho lớp đã hủy hoặc hoàn thành.");

        var coach = await db.CoachProfiles.SingleOrDefaultAsync(x => x.Id == request.CoachId, cancellationToken)
            ?? throw new InvalidOperationException("Huấn luyện viên không tồn tại.");
        var coachUser = await db.Users.SingleOrDefaultAsync(x => x.Id == coach.UserId, cancellationToken);
        if (!IsState(coach.Status, "Active") || coachUser is null || !IsState(coachUser.Status, "Active") ||
            (coachUser.LockedUntil.HasValue && coachUser.LockedUntil > DateTime.UtcNow))
            throw new InvalidOperationException("Huấn luyện viên không ở trạng thái hoạt động.");
        if (coach.CenterId != classEntity.CenterId)
            throw new InvalidOperationException("Huấn luyện viên và lớp học phải thuộc cùng trung tâm.");

        var sport = await db.Sports.SingleOrDefaultAsync(x => x.Id == classEntity.SportId, cancellationToken);
        if (sport is not null && !string.IsNullOrWhiteSpace(coach.Specialization) &&
            !coach.Specialization.Contains(sport.Name, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chuyên môn của huấn luyện viên không phù hợp với lớp học.");

        var targetSchedules = await db.ClassSchedules
            .Where(x => x.ClassId == classId && x.Status == "Active")
            .ToListAsync(cancellationToken);
        var assignments = await db.ClassCoaches.Where(x => x.CoachId == coach.Id && x.ClassId != classId)
            .ToListAsync(cancellationToken);
        var assignedClassIds = assignments.Select(x => x.ClassId).ToArray();
        var existingSchedules = assignedClassIds.Length == 0
            ? []
            : await db.ClassSchedules.Where(x => assignedClassIds.Contains(x.ClassId) && x.Status == "Active")
                .ToListAsync(cancellationToken);

        foreach (var day in targetSchedules.Select(x => x.DayOfWeek).Distinct())
        {
            var targetsForDay = targetSchedules.Where(x => x.DayOfWeek == day).ToArray();
            var existingForDay = existingSchedules.Where(existing => existing.DayOfWeek == day &&
                targetsForDay.Any(target => DateRangesOverlap(existing, target))).ToArray();
            if (targetsForDay.Any(target => existingForDay.Any(existing =>
                    target.StartTime < existing.EndTime && target.EndTime > existing.StartTime)))
                throw new InvalidOperationException("Huấn luyện viên bị trùng lịch dạy với lớp khác.");

            var hoursForDay = targetsForDay.Sum(DurationHours) + existingForDay.Sum(DurationHours);
            if (hoursForDay > MaxDailyTeachingHours)
            {
                throw new InvalidOperationException("Phân công này làm huấn luyện viên vượt quá 8 giờ dạy trong ngày.");
            }
        }

        var classAssignments = await db.ClassCoaches.Where(x => x.ClassId == classId).ToListAsync(cancellationToken);
        var current = classAssignments.SingleOrDefault(x => x.CoachId == coach.Id);
        if (current is null && classAssignments.Count > 0)
            throw new InvalidOperationException("Lớp đã có HLV; hãy gỡ HLV hiện tại trước khi phân công người khác.");

        if (current is null)
        {
            current = new ClassCoach { ClassId = classId, CoachId = coach.Id, IsPrimary = request.IsPrimary,
                AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow) };
            db.ClassCoaches.Add(current);
        }
        else
        {
            current.IsPrimary = request.IsPrimary;
            current.AssignedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (request.IsPrimary)
            foreach (var other in classAssignments.Where(x => x.CoachId != coach.Id)) other.IsPrimary = false;

        await db.SaveChangesAsync(cancellationToken);
        return new ClassCoachResponse(classId, coach.Id, coach.FullName, coach.CoachCode, current.IsPrimary,
            current.AssignedDate, coach.Specialization);
    }

    public async Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(long classId,
        CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Context.Classes.AnyAsync(x => x.Id == classId, cancellationToken))
            throw new InvalidOperationException("Lớp học không tồn tại.");
        return await (from link in unitOfWork.Context.ClassCoaches
                      join coach in unitOfWork.Context.CoachProfiles on link.CoachId equals coach.Id
                      where link.ClassId == classId
                      select new ClassCoachResponse(link.ClassId, coach.Id, coach.FullName, coach.CoachCode,
                          link.IsPrimary, link.AssignedDate, coach.Specialization))
            .ToListAsync(cancellationToken);
    }

<<<<<<< Updated upstream
    private const double MaxWeeklyTeachingHours = 30.0;

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
        if (sport is not null && !string.IsNullOrWhiteSpace(coachProfile.Specialization))
        {
            if (!coachProfile.Specialization.Contains(sport.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Huấn luyện viên {coachProfile.FullName} (Chuyên môn: '{coachProfile.Specialization}') " +
                    $"không có chứng chỉ/chuyên môn phù hợp với môn học '{sport.Name}' của lớp.");
            }
        }

        // Lấy danh sách lịch học tươi từ DB cho lớp cần phân công
        var targetSchedules = await unitOfWork.Repository<ClassSchedule>()
            .Find(s => s.ClassId == classId)
            .ToListAsync(cancellationToken);

        // Tính tổng số giờ dạy trong tuần của lớp cần phân công
        double targetWeeklyHours = targetSchedules
            .Sum(s => (s.EndTime.ToTimeSpan() - s.StartTime.ToTimeSpan()).TotalHours);

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
                select new { Schedule = s, ClassName = c.Name }
            ).ToListAsync(cancellationToken);

            existingSchedules = rawSchedules.Select(x => (x.Schedule, x.ClassName)).ToList();
        }

        // 1. Kiểm tra giới hạn số giờ dạy tối đa trong tuần (Capacity Limit)
        double existingWeeklyHours = existingSchedules
            .Sum(x => (x.Schedule.EndTime.ToTimeSpan() - x.Schedule.StartTime.ToTimeSpan()).TotalHours);

        if (existingWeeklyHours + targetWeeklyHours > MaxWeeklyTeachingHours)
        {
            throw new InvalidOperationException(
                $"Việc phân công thêm lớp này sẽ làm cho HLV {coachProfile.FullName} vượt quá giới hạn giờ dạy tối đa " +
                $"({existingWeeklyHours + targetWeeklyHours:F1}/{MaxWeeklyTeachingHours:F1} giờ/tuần).");
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

        // 3. Nếu phân công HLV chính (IsPrimary = true), tự động gián cấp các HLV chính khác trong lớp thành HLV phụ
        if (request.IsPrimary)
        {
            var otherPrimaryCoaches = await db.ClassCoaches
                .Where(cc => cc.ClassId == classId && cc.CoachId != request.CoachId && cc.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var otherCc in otherPrimaryCoaches)
            {
                otherCc.IsPrimary = false;
            }
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
=======
    public async Task UnassignCoachFromClassAsync(long classId, long coachId,
        CancellationToken cancellationToken = default)
    {
        var link = await unitOfWork.Context.ClassCoaches.SingleOrDefaultAsync(
            x => x.ClassId == classId && x.CoachId == coachId, cancellationToken)
            ?? throw new InvalidOperationException("Huấn luyện viên chưa được phân công vào lớp này.");
        unitOfWork.Context.ClassCoaches.Remove(link);
        await unitOfWork.Context.SaveChangesAsync(cancellationToken);
    }

    private static bool IsState(string value, params string[] accepted) =>
        accepted.Any(state => string.Equals(value, state, StringComparison.OrdinalIgnoreCase));

    private static double DurationHours(ClassSchedule schedule) =>
        (schedule.EndTime.ToTimeSpan() - schedule.StartTime.ToTimeSpan()).TotalHours;

    private static bool DateRangesOverlap(ClassSchedule first, ClassSchedule second) =>
        (first.StartDate ?? DateOnly.MinValue) <= (second.EndDate ?? DateOnly.MaxValue) &&
        (second.StartDate ?? DateOnly.MinValue) <= (first.EndDate ?? DateOnly.MaxValue);
>>>>>>> Stashed changes
}
