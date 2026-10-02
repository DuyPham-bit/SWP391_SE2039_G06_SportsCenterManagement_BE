using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class ClassService(IUnitOfWork unitOfWork) : IClassService
{
    private const double MaxWeeklyTeachingHours = 30.0;

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

    public Task<ClassCoachResponse> AssignCoachToClassAsync(
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default)
    {
        return AssignCoachToClassInternalAsync(null, classId, request, cancellationToken);
    }

    public Task<ClassCoachResponse> AssignCoachToClassAsync(
        long centerId,
        long classId,
        AssignCoachRequest request,
        CancellationToken cancellationToken = default)
    {
        return AssignCoachToClassInternalAsync(centerId, classId, request, cancellationToken);
    }

    private async Task<ClassCoachResponse> AssignCoachToClassInternalAsync(
        long? centerId,
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

        if (centerId.HasValue && classEntity.CenterId != centerId.Value)
        {
            throw new UnauthorizedAccessException("Lớp học không thuộc cơ sở của tài khoản hiện tại.");
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

        // 2. Lịch dạy không được chồng lấn trong cùng thời gian hiệu lực (Schedule Overlap Check).
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

        // 3. Mỗi lớp chỉ có một huấn luyện viên chính.
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

        // 4. Tạo hoặc cập nhật quan hệ phân công.
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

    public Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long classId,
        CancellationToken cancellationToken = default)
    {
        return GetAssignedCoachesInternalAsync(null, classId, cancellationToken);
    }

    public Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesAsync(
        long centerId,
        long classId,
        CancellationToken cancellationToken = default)
    {
        return GetAssignedCoachesInternalAsync(centerId, classId, cancellationToken);
    }

    private async Task<IReadOnlyList<ClassCoachResponse>> GetAssignedCoachesInternalAsync(
        long? centerId,
        long classId,
        CancellationToken cancellationToken = default)
    {
        var classEntity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.");

        if (centerId.HasValue && classEntity.CenterId != centerId.Value)
        {
            throw new UnauthorizedAccessException("Lớp học không thuộc cơ sở của tài khoản hiện tại.");
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

    public Task UnassignCoachFromClassAsync(
        long classId,
        long coachId,
        CancellationToken cancellationToken = default)
    {
        return UnassignCoachFromClassInternalAsync(null, classId, coachId, cancellationToken);
    }

    public Task UnassignCoachFromClassAsync(
        long centerId,
        long classId,
        long coachId,
        CancellationToken cancellationToken = default)
    {
        return UnassignCoachFromClassInternalAsync(centerId, classId, coachId, cancellationToken);
    }

    private async Task UnassignCoachFromClassInternalAsync(
        long? centerId,
        long classId,
        long coachId,
        CancellationToken cancellationToken = default)
    {
        var classEntity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.");

        if (centerId.HasValue && classEntity.CenterId != centerId.Value)
        {
            throw new UnauthorizedAccessException("Lớp học không thuộc cơ sở của tài khoản hiện tại.");
        }

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
}
