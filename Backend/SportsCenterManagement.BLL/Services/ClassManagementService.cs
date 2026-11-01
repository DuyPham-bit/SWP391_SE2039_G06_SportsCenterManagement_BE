using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Classes;
using SportsCenterManagement.BLL.Interfaces;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;
using SportsCenterManagement.DAL.Repositories.Interfaces;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// UC-12. Enforces BR-CLASS-01 (capacity vs room), BR-CLASS-03 (room not double-booked)
/// and, when a schedule is added to a class that already has coaches, BR-CLASS-02.
/// </summary>
public sealed class ClassManagementService(IUnitOfWork unitOfWork) : IClassManagementService
{
    private const int MaxScheduleSpanDays = 366;
    private const double MaxDailyTeachingHours = 8.0;
    private static readonly TimeOnly CenterOpeningTime = new(5, 0);
    private static readonly TimeOnly CenterClosingTime = new(22, 0);

    private SportsCenterDbContext Db => unitOfWork.Context;

    // ------------------------------------------------------------------ classes

    public async Task<ClassDetailResponse> CreateClassAsync(long actorUserId, CreateClassRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClassInput(request.Name, request.Capacity, request.DurationMinutes);
        await using var transaction = await Db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var centerExists = await unitOfWork.Repository<Center>().AnyAsync(
            c => c.Id == request.CenterId && c.Status == "Active", cancellationToken);
        if (!centerExists) throw new InvalidOperationException("Cơ sở không tồn tại hoặc đã ngừng hoạt động.") { Data = { ["StatusCode"] = 404 } };

        await EnsureSportAndRoomAsync(request.CenterId, request.SportId, request.RoomId, request.Capacity, cancellationToken);

        var entity = new ClassEntity
        {
            CenterId = request.CenterId,
            SportId = request.SportId,
            RoomId = request.RoomId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Level = request.Level?.Trim(),
            Capacity = request.Capacity,
            AllowWaitlist = request.AllowWaitlist,
            DurationMinutes = request.DurationMinutes,
            Status = FlowStatuses.ClassDraft,
            CreatedAt = DateTime.UtcNow
        };
        await unitOfWork.Repository<ClassEntity>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class.created", "Class", entity.Id,
            newValues: new { entity.Name, entity.SportId, entity.RoomId, entity.Capacity, entity.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<ClassDetailResponse> PublishClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (entity.Status == FlowStatuses.ClassPublished)
        {
            await transaction.CommitAsync(cancellationToken);
            return ToResponse(entity);
        }
        if (FlowStatuses.IsClosed(entity.Status))
            throw new InvalidOperationException($"Không thể mở lớp ở trạng thái '{entity.Status}'.") { Data = { ["StatusCode"] = 409 } };
        var previousStatus = entity.Status;

        var hasSchedule = await Db.ClassSchedules.AnyAsync(
            schedule => schedule.ClassId == classId && schedule.Status == FlowStatuses.ScheduleActive,
            cancellationToken);
        if (!hasSchedule)
            throw new InvalidOperationException("Cần tạo ít nhất một lịch hoạt động trước khi mở lớp.") { Data = { ["StatusCode"] = 409 } };

        var hasActiveCoach = await (
            from assignment in Db.ClassCoaches
            join coach in Db.CoachProfiles on assignment.CoachId equals coach.Id
            join user in Db.Users on coach.UserId equals user.Id
            where assignment.ClassId == classId && assignment.IsPrimary && coach.CenterId == entity.CenterId
                && coach.Status == "Active" && user.Status == "Active"
                && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
            select assignment.CoachId).AnyAsync(cancellationToken);
        if (!hasActiveCoach)
            throw new InvalidOperationException("Cần phân công ít nhất một huấn luyện viên đang hoạt động trước khi mở lớp.") { Data = { ["StatusCode"] = 409 } };

        var hasUpcomingSession = await Db.ClassSessions.AnyAsync(session =>
            session.ClassId == classId && session.SessionStatus == FlowStatuses.SessionScheduled
            && (session.SessionDate > ScheduleTime.VietnamToday
                || (session.SessionDate == ScheduleTime.VietnamToday && session.StartTime > TimeOnly.FromDateTime(ScheduleTime.VietnamNow))),
            cancellationToken);
        if (!hasUpcomingSession)
            throw new InvalidOperationException("Lịch lớp chưa tạo buổi học sắp tới trong khoảng hiệu lực.") { Data = { ["StatusCode"] = 409 } };

        entity.Status = FlowStatuses.ClassPublished;
        entity.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class.published", "Class", entity.Id,
            new { Status = previousStatus }, new { Status = entity.Status });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<ClassDetailResponse> GetClassAsync(long classId, CancellationToken cancellationToken = default)
    {
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        return ToResponse(entity);
    }

    public async Task<ClassDetailResponse> UpdateClassAsync(long actorUserId, long classId, UpdateClassRequest request, CancellationToken cancellationToken = default)
    {
        ValidateClassInput(request.Name, request.Capacity, request.DurationMinutes);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (FlowStatuses.IsClosed(entity.Status))
            throw new InvalidOperationException($"Không thể cập nhật lớp học ở trạng thái '{entity.Status}'.") { Data = { ["StatusCode"] = 409 } };

        var oldValues = new
        {
            entity.Name, entity.SportId, entity.RoomId, entity.Capacity,
            entity.AllowWaitlist, entity.DurationMinutes, entity.Status
        };

        await EnsureSportAndRoomAsync(entity.CenterId, request.SportId, request.RoomId, request.Capacity, cancellationToken);
        if (request.SportId != entity.SportId)
        {
            var newSportName = await Db.Sports.Where(sport => sport.Id == request.SportId)
                .Select(sport => sport.Name).SingleAsync(cancellationToken);
            var assignedCoaches = await (
                from assignment in Db.ClassCoaches
                join coach in Db.CoachProfiles on assignment.CoachId equals coach.Id
                join user in Db.Users on coach.UserId equals user.Id
                where assignment.ClassId == classId
                select new
                {
                    assignment.IsPrimary,
                    CoachId = coach.Id,
                    coach.FullName,
                    CoachActive = coach.Status == "Active" && user.Status == "Active"
                        && (!user.LockedUntil.HasValue || user.LockedUntil.Value <= DateTime.UtcNow)
                }).ToListAsync(cancellationToken);

            if (entity.Status == FlowStatuses.ClassPublished
                && !assignedCoaches.Any(coach => coach.IsPrimary && coach.CoachActive))
            {
                throw new InvalidOperationException("Không thể đổi bộ môn của lớp đang mở khi chưa có HLV chính đang hoạt động.") { Data = { ["StatusCode"] = 409 } };
            }
            var compatibleCoachIds = await Db.CoachSports.Where(item => item.SportId == request.SportId)
                .Select(item => item.CoachId).ToListAsync(cancellationToken);
            var incompatibleCoach = assignedCoaches.FirstOrDefault(coach => coach.CoachActive
                && !compatibleCoachIds.Contains(coach.CoachId));
            if (incompatibleCoach is not null)
            {
                throw new InvalidOperationException($"HLV {incompatibleCoach.FullName} chưa có chuyên môn phù hợp với bộ môn '{newSportName}'.") { Data = { ["StatusCode"] = 409 } };
            }
        }

        var today = ScheduleTime.VietnamToday;

        // BR-CLASS-01: never shrink below seats already booked on an upcoming session.
        if (request.Capacity < entity.Capacity)
        {
            var peak = await (
                from booking in Db.SessionBookings
                join session in Db.ClassSessions on booking.SessionId equals session.Id
                where session.ClassId == classId
                    && session.SessionStatus == FlowStatuses.SessionScheduled
                    && session.SessionDate >= today
                    && booking.Status == FlowStatuses.BookingBooked
                group booking by session.Id into g
                select g.Count()).ToListAsync(cancellationToken);
            var maxBooked = peak.Count == 0 ? 0 : peak.Max();
            if (request.Capacity < maxBooked)
                throw new InvalidOperationException($"Sức chứa mới ({request.Capacity}) nhỏ hơn số chỗ đã đặt ({maxBooked}) của một buổi sắp tới.") { Data = { ["StatusCode"] = 409 } };
        }

        // BR-CLASS-03: a room change must not collide with other classes for schedules
        // that follow the class default room. The manager UI historically persisted the
        // current default room as an explicit schedule room, so those schedules must also
        // follow a later default-room change.
        var roomChanged = request.RoomId != entity.RoomId;
        List<ClassSchedule> schedulesFollowingDefaultRoom = [];
        if (roomChanged)
        {
            schedulesFollowingDefaultRoom = await Db.ClassSchedules
                .Where(s => s.ClassId == classId && s.Status == FlowStatuses.ScheduleActive
                    && (s.RoomId == null || s.RoomId == entity.RoomId))
                .ToListAsync(cancellationToken);
            foreach (var schedule in schedulesFollowingDefaultRoom)
            {
                await EnsureRoomFreeAsync(request.RoomId, schedule.DayOfWeek, schedule.StartTime, schedule.EndTime,
                    schedule.StartDate, schedule.EndDate, schedule.Id, cancellationToken);
            }
        }

        // A class-level capacity change must be effective for every active schedule.
        // Without this check the manager can save (for example) 3 seats while a schedule
        // still runs in a 2-seat override room, and members continue to see the session as full.
        await EnsureActiveScheduleRoomsCanHostAsync(
            entity, request.RoomId, request.Capacity, cancellationToken);

        entity.SportId = request.SportId;
        entity.RoomId = request.RoomId;
        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        entity.Level = request.Level?.Trim();
        entity.Capacity = request.Capacity;
        entity.AllowWaitlist = request.AllowWaitlist;
        entity.DurationMinutes = request.DurationMinutes;
        entity.UpdatedAt = DateTime.UtcNow;

        if (roomChanged && schedulesFollowingDefaultRoom.Count > 0)
        {
            // Preserve true inheritance (null); update rows that only duplicated the old
            // default room so future generated sessions use the new default as well.
            foreach (var schedule in schedulesFollowingDefaultRoom.Where(schedule => schedule.RoomId.HasValue))
                schedule.RoomId = request.RoomId;

            var scheduleIds = schedulesFollowingDefaultRoom.Select(s => (long?)s.Id).ToList();
            var localToday = ScheduleTime.VietnamToday;
            var localNow = TimeOnly.FromDateTime(ScheduleTime.VietnamNow);
            var upcoming = await Db.ClassSessions
                .Where(s => s.ScheduleId != null && scheduleIds.Contains(s.ScheduleId)
                    && s.SessionStatus == FlowStatuses.SessionScheduled
                    && (s.SessionDate > localToday || (s.SessionDate == localToday && s.StartTime > localNow)))
                .ToListAsync(cancellationToken);
            foreach (var session in upcoming) session.RoomId = request.RoomId;
            var roomName = await Db.Rooms.Where(room => room.Id == request.RoomId)
                .Select(room => room.Name).FirstOrDefaultAsync(cancellationToken) ?? "Chưa phân công phòng";
            await NotifyBookedMembersAsync(actorUserId, upcoming.Select(session => session.Id).ToList(),
                $"Thay đổi địa điểm: {entity.Name}",
                $"Địa điểm các buổi học sắp tới của lớp {entity.Name} đã đổi thành {roomName}. Vui lòng kiểm tra lịch cá nhân trước khi đến tập.", cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class.updated", "Class", entity.Id,
            oldValues, new
            {
                entity.Name, entity.SportId, entity.RoomId, entity.Capacity,
                entity.AllowWaitlist, entity.DurationMinutes, entity.Status
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    public async Task<CancelClassResult> CancelClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (FlowStatuses.IsClosed(entity.Status))
            throw new InvalidOperationException($"Không thể hủy lớp ở trạng thái '{entity.Status}'.") { Data = { ["StatusCode"] = 409 } };
        var previousStatus = entity.Status;

        var enrollments = await Db.ClassEnrollments
            .Where(e => e.ClassId == classId && e.Status == FlowStatuses.EnrollmentConfirmed)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var enrollment in enrollments)
        {
            enrollment.Status = FlowStatuses.EnrollmentCancelled;
            enrollment.CancelledAt = now;
            enrollment.CancellationReason = "Lớp học đã bị hủy.";
        }

        var waiting = await Db.ClassWaitlists
            .Where(w => w.ClassId == classId && w.Status == FlowStatuses.WaitlistWaiting)
            .ToListAsync(cancellationToken);
        foreach (var entry in waiting) entry.Status = FlowStatuses.WaitlistCancelled;

        var schedules = await Db.ClassSchedules
            .Where(s => s.ClassId == classId && s.Status == FlowStatuses.ScheduleActive)
            .ToListAsync(cancellationToken);
        foreach (var schedule in schedules) schedule.Status = FlowStatuses.ScheduleCancelled;

        var (sessions, bookings) = await CancelUpcomingSessionsAsync(
            Db.ClassSessions.Where(s => s.ClassId == classId), actorUserId, cancellationToken);

        entity.Status = FlowStatuses.ClassCancelled;
        entity.UpdatedAt = now;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class.cancelled", "Class", entity.Id,
            new { Status = previousStatus },
            new
            {
                Status = entity.Status,
                CancelledEnrollments = enrollments.Count,
                CancelledWaitlistEntries = waiting.Count,
                CancelledSessions = sessions,
                CancelledBookings = bookings
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CancelClassResult(classId, entity.Status, enrollments.Count, bookings, sessions);
    }

    public async Task<ClassDetailResponse> RestoreClassAsync(long actorUserId, long classId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, cancellationToken)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };

        if (!string.Equals(entity.Status, FlowStatuses.ClassCancelled, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ lớp đã hủy mới có thể mở lại.") { Data = { ["StatusCode"] = 409 } };

        entity.Status = FlowStatuses.ClassDraft;
        entity.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class.restored", "Class", entity.Id,
            new { Status = FlowStatuses.ClassCancelled },
            new { Status = entity.Status, HistoricalDataRestored = false });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(entity);
    }

    // ---------------------------------------------------------------- schedules

    public async Task<IReadOnlyList<ClassScheduleResponse>> GetSchedulesAsync(long classId, CancellationToken cancellationToken = default)
    {
        if (!await unitOfWork.Repository<ClassEntity>().AnyAsync(c => c.Id == classId, cancellationToken))
            throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };

        var schedules = await Db.ClassSchedules.Where(s => s.ClassId == classId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime).ToListAsync(cancellationToken);
        return schedules.Select(s => ToResponse(s, 0)).ToList();
    }

    public async Task<ClassScheduleResponse> CreateScheduleAsync(long actorUserId, long classId, CreateClassScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScheduleInput(request.DayOfWeek, request.StartTime, request.EndTime, request.StartDate, request.EndDate);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await LoadOpenClassAsync(classId, cancellationToken);
        ValidateScheduleDuration(request.StartTime, request.EndTime, entity.DurationMinutes);
        var roomId = request.RoomId ?? entity.RoomId;
        await EnsureScheduleRoomAsync(entity, request.RoomId, cancellationToken);

        await EnsureRoomFreeAsync(roomId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.StartDate, request.EndDate, null, cancellationToken);
        await EnsureCoachesFreeAsync(classId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.StartDate, request.EndDate, cancellationToken);

        var schedule = new ClassSchedule
        {
            ClassId = classId,
            RoomId = request.RoomId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = FlowStatuses.ScheduleActive
        };
        await unitOfWork.Repository<ClassSchedule>().AddAsync(schedule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken); // need schedule.Id for sessions

        var generated = await GenerateSessionsAsync(schedule, entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class_schedule.created", "ClassSchedule", schedule.Id,
            newValues: new
            {
                schedule.ClassId, schedule.RoomId, schedule.DayOfWeek, schedule.StartTime,
                schedule.EndTime, schedule.StartDate, schedule.EndDate, schedule.Status,
                GeneratedSessions = generated
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(schedule, generated);
    }

    public async Task<ClassScheduleResponse> UpdateScheduleAsync(long actorUserId, long classId, long scheduleId, UpdateClassScheduleRequest request, CancellationToken cancellationToken = default)
    {
        ValidateScheduleInput(request.DayOfWeek, request.StartTime, request.EndTime, request.StartDate, request.EndDate);

        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var entity = await LoadOpenClassAsync(classId, cancellationToken);
        ValidateScheduleDuration(request.StartTime, request.EndTime, entity.DurationMinutes);
        var schedule = await LoadActiveScheduleAsync(classId, scheduleId, cancellationToken);
        var oldValues = new
        {
            schedule.RoomId, schedule.DayOfWeek, schedule.StartTime, schedule.EndTime,
            schedule.StartDate, schedule.EndDate, schedule.Status
        };
        await EnsureScheduleRoomAsync(entity, request.RoomId, cancellationToken);

        var roomId = request.RoomId ?? entity.RoomId;
        await EnsureRoomFreeAsync(roomId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.StartDate, request.EndDate, scheduleId, cancellationToken);
        await EnsureCoachesFreeAsync(classId, request.DayOfWeek, request.StartTime, request.EndTime,
            request.StartDate, request.EndDate, cancellationToken, scheduleId);

        var today = ScheduleTime.VietnamToday;
        var now = TimeOnly.FromDateTime(ScheduleTime.VietnamNow);
        var upcoming = await Db.ClassSessions
            .Where(s => s.ScheduleId == scheduleId && s.SessionStatus == FlowStatuses.SessionScheduled
                && (s.SessionDate > today || (s.SessionDate == today && s.StartTime > now)))
            .ToListAsync(cancellationToken);
        var upcomingIds = upcoming.Select(s => s.Id).ToList();
        var activeBookings = await Db.SessionBookings.CountAsync(
            b => upcomingIds.Contains(b.SessionId) && b.Status == FlowStatuses.BookingBooked, cancellationToken);
        if (activeBookings > 0)
            throw new InvalidOperationException($"Lịch này đang có {activeBookings} chỗ đặt cho các buổi sắp tới. Hãy hủy lịch thay vì sửa.") { Data = { ["StatusCode"] = 409 } };

        var waitingEntries = await Db.ClassWaitlists
            .Where(item => upcomingIds.Contains(item.SessionId ?? 0)
                && item.Status == FlowStatuses.WaitlistWaiting)
            .ToListAsync(cancellationToken);
        foreach (var entry in waitingEntries) entry.Status = FlowStatuses.WaitlistCancelled;

        foreach (var session in upcoming) session.SessionStatus = FlowStatuses.SessionCancelled;

        schedule.RoomId = request.RoomId;
        schedule.DayOfWeek = request.DayOfWeek;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.StartDate = request.StartDate;
        schedule.EndDate = request.EndDate;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var generated = await GenerateSessionsAsync(schedule, entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, entity.CenterId, "class_schedule.updated", "ClassSchedule", schedule.Id,
            oldValues, new
            {
                schedule.RoomId, schedule.DayOfWeek, schedule.StartTime, schedule.EndTime,
                schedule.StartDate, schedule.EndDate, schedule.Status, GeneratedSessions = generated
            });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(schedule, generated);
    }

    public async Task<ClassScheduleResponse> CancelScheduleAsync(long actorUserId, long classId, long scheduleId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await Db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var schedule = await LoadActiveScheduleAsync(classId, scheduleId, cancellationToken);
        var classCenterId = await Db.Classes.Where(item => item.Id == classId)
            .Select(item => item.CenterId).SingleAsync(cancellationToken);
        var previousStatus = schedule.Status;
        schedule.Status = FlowStatuses.ScheduleCancelled;
        var (cancelledSessions, cancelledBookings) = await CancelUpcomingSessionsAsync(
            Db.ClassSessions.Where(s => s.ScheduleId == scheduleId), actorUserId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        AuditLogWriter.Add(unitOfWork, actorUserId, classCenterId, "class_schedule.cancelled",
            "ClassSchedule", schedule.Id,
            new { Status = previousStatus },
            new { Status = schedule.Status, CancelledSessions = cancelledSessions, CancelledBookings = cancelledBookings });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(schedule, 0);
    }

    // ---------------------------------------------------------------- validation

    private static void ValidateClassInput(string? name, int capacity, int durationMinutes)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Tên lớp không được để trống.") { Data = { ["StatusCode"] = 400 } };
        if (name.Trim().Length > 150) throw new InvalidOperationException("Tên lớp tối đa 150 ký tự.") { Data = { ["StatusCode"] = 400 } };
        if (capacity <= 0) throw new InvalidOperationException("Sức chứa phải lớn hơn 0.") { Data = { ["StatusCode"] = 400 } };
        if (durationMinutes <= 0) throw new InvalidOperationException("Thời lượng phải lớn hơn 0 phút.") { Data = { ["StatusCode"] = 400 } };
    }

    private static void ValidateScheduleInput(int dayOfWeek, TimeOnly start, TimeOnly end, DateOnly startDate, DateOnly endDate)
    {
        if (dayOfWeek is < 0 or > 6) throw new InvalidOperationException("Thứ trong tuần phải từ 0 (Chủ nhật) đến 6 (Thứ bảy).") { Data = { ["StatusCode"] = 400 } };
        if (end <= start) throw new InvalidOperationException("Giờ kết thúc phải sau giờ bắt đầu.") { Data = { ["StatusCode"] = 400 } };
        if (start < CenterOpeningTime || end > CenterClosingTime)
            throw new InvalidOperationException("Lịch học phải nằm trong giờ hoạt động của trung tâm, từ 05:00 đến 22:00.") { Data = { ["StatusCode"] = 400 } };
        if (endDate < startDate) throw new InvalidOperationException("Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.") { Data = { ["StatusCode"] = 400 } };
        if (endDate.DayNumber - startDate.DayNumber > MaxScheduleSpanDays)
            throw new InvalidOperationException($"Một lịch lặp tối đa {MaxScheduleSpanDays} ngày.") { Data = { ["StatusCode"] = 400 } };
    }

    private static void ValidateScheduleDuration(TimeOnly start, TimeOnly end, int maximumMinutes)
    {
        var scheduledMinutes = (end - start).TotalMinutes;
        if (scheduledMinutes > maximumMinutes)
            throw new InvalidOperationException($"Một buổi học không được vượt quá thời lượng {maximumMinutes} phút đã thiết lập cho lớp.") { Data = { ["StatusCode"] = 400 } };
    }

    private async Task EnsureSportAndRoomAsync(long centerId, long sportId, long? roomId, int capacity, CancellationToken ct)
    {
        var sport = await unitOfWork.Repository<Sport>().GetByIdAsync(sportId, ct);
        if (sport is null || sport.Status != "Active" || (sport.CenterId.HasValue && sport.CenterId.Value != centerId))
            throw new InvalidOperationException("Bộ môn không tồn tại.") { Data = { ["StatusCode"] = 404 } };

        if (roomId is null) return;
        var room = await unitOfWork.Repository<Room>().GetByIdAsync(roomId.Value, ct)
            ?? throw new InvalidOperationException("Phòng tập không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (room.CenterId != centerId)
            throw new InvalidOperationException("Phòng tập không thuộc cơ sở của lớp học.") { Data = { ["StatusCode"] = 400 } };
        if (!string.Equals(room.Status, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Phòng tập đang bảo trì hoặc đã ngừng hoạt động.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-03",
                    ["ProposedHandling"] = "Chặn lưu lịch bị xung đột; chỉ rõ lớp/phòng/thời gian liên quan để đổi lịch hoặc phòng."
                }
            };
        if (capacity > room.Capacity) // BR-CLASS-01
            throw new InvalidOperationException($"Sức chứa lớp ({capacity}) vượt quá sức chứa phòng '{room.Name}' ({room.Capacity}).") { Data = { ["StatusCode"] = 400 } };
    }

    private async Task EnsureScheduleRoomAsync(ClassEntity entity, long? scheduleRoomId, CancellationToken ct)
    {
        if (scheduleRoomId is null) return;
        await EnsureSportAndRoomAsync(entity.CenterId, entity.SportId, scheduleRoomId, entity.Capacity, ct);
    }

    private async Task EnsureActiveScheduleRoomsCanHostAsync(
        ClassEntity entity, long? requestedDefaultRoomId, int requestedCapacity, CancellationToken ct)
    {
        var defaultRoomChanged = requestedDefaultRoomId != entity.RoomId;
        var schedules = await Db.ClassSchedules
            .Where(schedule => schedule.ClassId == entity.Id
                && schedule.Status == FlowStatuses.ScheduleActive)
            .Select(schedule => new
            {
                schedule.RoomId,
                schedule.DayOfWeek,
                schedule.StartTime,
                schedule.EndTime
            })
            .ToListAsync(ct);

        var effectiveRooms = schedules.Select(schedule => new
        {
            Schedule = schedule,
            RoomId = schedule.RoomId is null
                || (defaultRoomChanged && schedule.RoomId == entity.RoomId)
                    ? requestedDefaultRoomId
                    : schedule.RoomId
        }).ToList();
        var roomIds = effectiveRooms.Where(item => item.RoomId.HasValue)
            .Select(item => item.RoomId!.Value).Distinct().ToArray();
        if (roomIds.Length == 0) return;

        var rooms = await Db.Rooms.Where(room => roomIds.Contains(room.Id))
            .ToDictionaryAsync(room => room.Id, ct);
        foreach (var item in effectiveRooms.Where(item => item.RoomId.HasValue))
        {
            if (!rooms.TryGetValue(item.RoomId!.Value, out var room)
                || room.CenterId != entity.CenterId
                || !string.Equals(room.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Lịch đang sử dụng phòng không còn hoạt động tại trung tâm.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-03",
                        ["ProposedHandling"] = "Đổi phòng của lịch sang một phòng đang hoạt động trước khi cập nhật lớp."
                    }
                };
            }

            if (requestedCapacity <= room.Capacity) continue;
            throw new InvalidOperationException(
                $"Không thể đặt sức chứa lớp là {requestedCapacity}: lịch Thứ {item.Schedule.DayOfWeek} " +
                $"{item.Schedule.StartTime:HH\\:mm}-{item.Schedule.EndTime:HH\\:mm} đang dùng phòng " +
                $"'{room.Name}' chỉ có {room.Capacity} chỗ.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-04",
                    ["ProposedHandling"] = "Đổi lịch sang phòng có sức chứa phù hợp rồi cập nhật lại sĩ số lớp."
                }
            };
        }
    }

    /// <summary>BR-CLASS-03: no two classes may use the same room at overlapping day/time/date ranges.</summary>
    private async Task EnsureRoomFreeAsync(
        long? roomId, int dayOfWeek, TimeOnly start, TimeOnly end, DateOnly? startDate, DateOnly? endDate,
        long? excludeScheduleId, CancellationToken ct)
    {
        if (roomId is null) return;

        var candidates = await (
            from s in Db.ClassSchedules
            join c in Db.Classes on s.ClassId equals c.Id
            where s.Status == FlowStatuses.ScheduleActive
                && c.Status != FlowStatuses.ClassCancelled && c.Status != FlowStatuses.ClassCompleted
                && s.DayOfWeek == dayOfWeek
                && (s.RoomId ?? c.RoomId) == roomId
                && (excludeScheduleId == null || s.Id != excludeScheduleId)
            select new { s.StartTime, s.EndTime, s.StartDate, s.EndDate, ClassName = c.Name })
            .ToListAsync(ct);

        var clash = candidates.FirstOrDefault(x =>
            ScheduleTime.Overlaps(start, end, x.StartTime, x.EndTime) &&
            ScheduleTime.DateRangesOverlap(startDate, endDate, x.StartDate, x.EndDate));
        if (clash is not null)
            throw new InvalidOperationException($"Phòng đã được lớp '{clash.ClassName}' sử dụng vào Thứ {dayOfWeek}, {clash.StartTime:HH\\:mm}-{clash.EndTime:HH\\:mm}.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-03",
                    ["ProposedHandling"] = "Chặn lưu lịch bị xung đột; chỉ rõ lớp/phòng/thời gian liên quan để đổi lịch hoặc phòng."
                }
            };
    }

    /// <summary>BR-CLASS-02 when a schedule is added/changed on a class that already has coaches.</summary>
    private async Task EnsureCoachesFreeAsync(
        long classId, int dayOfWeek, TimeOnly start, TimeOnly end, DateOnly? startDate, DateOnly? endDate,
        CancellationToken ct, long? excludeScheduleId = null)
    {
        var coaches = await (
            from cc in Db.ClassCoaches
            join coach in Db.CoachProfiles on cc.CoachId equals coach.Id
            where cc.ClassId == classId
            select new { coach.Id, coach.FullName }).ToListAsync(ct);
        if (coaches.Count == 0) return;

        var coachIds = coaches.Select(c => c.Id).ToList();
        var candidates = await (
            from cc in Db.ClassCoaches
            join s in Db.ClassSchedules on cc.ClassId equals s.ClassId
            join c in Db.Classes on s.ClassId equals c.Id
            where coachIds.Contains(cc.CoachId)
                && s.Status == FlowStatuses.ScheduleActive
                && c.Status != FlowStatuses.ClassCancelled && c.Status != FlowStatuses.ClassCompleted
                && s.DayOfWeek == dayOfWeek
                && (!excludeScheduleId.HasValue || s.Id != excludeScheduleId.Value)
            select new { cc.CoachId, Schedule = s, ClassName = c.Name })
            .ToListAsync(ct);

        var clash = candidates.FirstOrDefault(x =>
            ScheduleTime.Overlaps(start, end, x.Schedule.StartTime, x.Schedule.EndTime) &&
            ScheduleTime.DateRangesOverlap(startDate, endDate, x.Schedule.StartDate, x.Schedule.EndDate));
        if (clash is not null)
        {
            var name = coaches.First(c => c.Id == clash.CoachId).FullName;
            throw new InvalidOperationException($"HLV {name} đã có lịch dạy lớp '{clash.ClassName}' trùng khung giờ này.")
            {
                Data =
                {
                    ["StatusCode"] = 409,
                    ["Code"] = "CM-04",
                    ["ProposedHandling"] = "Kiểm tra lại khi lưu; từ chối phân công và hiển thị nguyên nhân."
                }
            };
        }

        var approvedLeaves = await Db.CoachLeaves
            .Where(leave => coachIds.Contains(leave.CoachId) && leave.Status.ToLower() == "approved")
            .ToListAsync(ct);
        foreach (var coach in coaches)
        {
            if (approvedLeaves.Any(leave => leave.CoachId == coach.Id && ProposedScheduleOverlapsLeave(
                    dayOfWeek, start, end, startDate, endDate, leave)))
            {
                throw new InvalidOperationException($"HLV {coach.FullName} đã được duyệt nghỉ trong thời gian của lịch học này.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-05",
                        ["ProposedHandling"] = "Chọn HLV khác hoặc điều chỉnh khoảng ngày/giờ học ngoài thời gian nghỉ đã duyệt."
                    }
                };
            }

            var coachSchedules = candidates.Where(item => item.CoachId == coach.Id)
                .Select(item => item.Schedule).ToList();
            EnsureDailyTeachingLimitForProposedSchedule(
                coach.FullName, dayOfWeek, start, end, startDate, endDate, coachSchedules);
        }
    }

    private static void EnsureDailyTeachingLimitForProposedSchedule(
        string coachName, int dayOfWeek, TimeOnly start, TimeOnly end,
        DateOnly? startDate, DateOnly? endDate, IReadOnlyCollection<ClassSchedule> existingSchedules)
    {
        var today = ScheduleTime.VietnamToday;
        var candidateDates = new HashSet<DateOnly> { NextOccurrence(today, dayOfWeek) };
        var boundaries = existingSchedules.Select(schedule => schedule.StartDate)
            .Append(startDate).Where(date => date.HasValue).Select(date => date!.Value);
        foreach (var boundary in boundaries)
            candidateDates.Add(NextOccurrence(boundary > today ? boundary : today, dayOfWeek));

        foreach (var date in candidateDates)
        {
            var proposedActive = dayOfWeek == (int)date.DayOfWeek
                && (!startDate.HasValue || startDate.Value <= date)
                && (!endDate.HasValue || endDate.Value >= date);
            var hours = proposedActive ? (end.ToTimeSpan() - start.ToTimeSpan()).TotalHours : 0;
            hours += existingSchedules.Where(schedule => schedule.DayOfWeek == (int)date.DayOfWeek
                    && (!schedule.StartDate.HasValue || schedule.StartDate.Value <= date)
                    && (!schedule.EndDate.HasValue || schedule.EndDate.Value >= date))
                .Sum(schedule => (schedule.EndTime.ToTimeSpan() - schedule.StartTime.ToTimeSpan()).TotalHours);
            if (hours > MaxDailyTeachingHours)
            {
                throw new InvalidOperationException(
                    $"Lịch mới làm cho HLV {coachName} dạy {hours:F1} giờ trong ngày, vượt giới hạn {MaxDailyTeachingHours:F1} giờ/ngày.")
                {
                    Data =
                    {
                        ["StatusCode"] = 409,
                        ["Code"] = "CM-06",
                        ["ProposedHandling"] = "Giảm thời lượng hoặc chuyển lịch sang ngày khác."
                    }
                };
            }
        }
    }

    private static bool ProposedScheduleOverlapsLeave(
        int dayOfWeek, TimeOnly start, TimeOnly end, DateOnly? startDate, DateOnly? endDate, CoachLeave leave)
    {
        var firstDate = DateOnly.FromDateTime(leave.StartAt);
        var lastDate = DateOnly.FromDateTime(leave.EndAt);
        if (startDate.HasValue && startDate.Value > firstDate) firstDate = startDate.Value;
        if (endDate.HasValue && endDate.Value < lastDate) lastDate = endDate.Value;
        if (lastDate < firstDate) return false;

        var occurrence = NextOccurrence(firstDate, dayOfWeek);
        while (occurrence <= lastDate)
        {
            var sessionStart = occurrence.ToDateTime(start);
            var sessionEnd = occurrence.ToDateTime(end);
            if (sessionStart < leave.EndAt && sessionEnd > leave.StartAt) return true;
            occurrence = occurrence.AddDays(7);
        }
        return false;
    }

    private static DateOnly NextOccurrence(DateOnly date, int dayOfWeek)
    {
        var daysUntil = (dayOfWeek - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntil);
    }

    // ------------------------------------------------------------------ helpers

    private async Task<ClassEntity> LoadOpenClassAsync(long classId, CancellationToken ct)
    {
        var entity = await unitOfWork.Repository<ClassEntity>().GetByIdAsync(classId, ct)
            ?? throw new InvalidOperationException("Lớp học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (FlowStatuses.IsClosed(entity.Status))
            throw new InvalidOperationException($"Lớp học đang ở trạng thái '{entity.Status}'.") { Data = { ["StatusCode"] = 409 } };
        return entity;
    }

    private async Task<ClassSchedule> LoadActiveScheduleAsync(long classId, long scheduleId, CancellationToken ct)
    {
        var schedule = await Db.ClassSchedules.SingleOrDefaultAsync(s => s.Id == scheduleId && s.ClassId == classId, ct)
            ?? throw new InvalidOperationException("Lịch học không tồn tại.") { Data = { ["StatusCode"] = 404 } };
        if (schedule.Status != FlowStatuses.ScheduleActive)
            throw new InvalidOperationException("Lịch học đã bị hủy.") { Data = { ["StatusCode"] = 409 } };
        return schedule;
    }

    /// <summary>Generates upcoming <c>class_sessions</c> for one weekly schedule (skips past dates and duplicates).</summary>
    private async Task<int> GenerateSessionsAsync(ClassSchedule schedule, ClassEntity entity, CancellationToken ct)
    {
        if (schedule.StartDate is null || schedule.EndDate is null) return 0;

        var primaryCoachId = await Db.ClassCoaches
            .Where(cc => cc.ClassId == entity.Id)
            .OrderByDescending(cc => cc.IsPrimary)
            .Select(cc => (long?)cc.CoachId)
            .FirstOrDefaultAsync(ct);

        var existingDates = (await Db.ClassSessions
            .Where(s => s.ScheduleId == schedule.Id && s.SessionStatus != FlowStatuses.SessionCancelled)
            .Select(s => s.SessionDate).ToListAsync(ct)).ToHashSet();

        var today = ScheduleTime.VietnamToday;
        var first = schedule.StartDate.Value > today ? schedule.StartDate.Value : today;
        var now = ScheduleTime.VietnamNow;
        var created = 0;
        for (var date = first; date <= schedule.EndDate.Value;)
        {
            if ((int)date.DayOfWeek == schedule.DayOfWeek && !existingDates.Contains(date)
                && (date != today || schedule.StartTime > TimeOnly.FromDateTime(now)))
            {
                await Db.ClassSessions.AddAsync(new ClassSession
                {
                    ClassId = entity.Id,
                    ScheduleId = schedule.Id,
                    RoomId = schedule.RoomId ?? entity.RoomId,
                    CoachId = primaryCoachId,
                    SessionDate = date,
                    StartTime = schedule.StartTime,
                    EndTime = schedule.EndTime,
                    SessionStatus = FlowStatuses.SessionScheduled,
                    CreatedAt = DateTime.UtcNow
                }, ct);
                created++;
            }

            // Avoid overflowing DateOnly.MaxValue when the schedule ends on its maximum value.
            if (date == schedule.EndDate.Value) break;
            date = date.AddDays(1);
        }
        return created;
    }

    /// <summary>Cancels not-yet-held sessions from the given set together with their active bookings.</summary>
    private async Task<(int Sessions, int Bookings)> CancelUpcomingSessionsAsync(IQueryable<ClassSession> source, long actorUserId, CancellationToken ct)
    {
        var today = ScheduleTime.VietnamToday;
        var now = TimeOnly.FromDateTime(ScheduleTime.VietnamNow);
        var sessions = await source
            .Where(s => s.SessionStatus == FlowStatuses.SessionScheduled
                && (s.SessionDate > today || (s.SessionDate == today && s.StartTime > now)))
            .ToListAsync(ct);
        var ids = sessions.Select(s => s.Id).ToList();
        var bookings = await Db.SessionBookings
            .Where(b => ids.Contains(b.SessionId) && b.Status == FlowStatuses.BookingBooked)
            .ToListAsync(ct);

        // Persist recipient links in the same transaction as the schedule change.
        await NotifyBookedMembersAsync(actorUserId, ids, "Lịch học đã bị hủy",
            "Trung tâm đã hủy lịch học bạn đã đăng ký. Vui lòng xem lịch cá nhân để kiểm tra buổi bị hủy và chọn buổi học khác.", ct);
        foreach (var session in sessions) session.SessionStatus = FlowStatuses.SessionCancelled;
        foreach (var booking in bookings) booking.Status = FlowStatuses.BookingCancelled;
        var waitlists = await Db.ClassWaitlists
            .Where(item => ids.Contains(item.SessionId ?? 0) && item.Status == FlowStatuses.WaitlistWaiting)
            .ToListAsync(ct);
        foreach (var waitlist in waitlists) waitlist.Status = FlowStatuses.WaitlistCancelled;
        return (sessions.Count, bookings.Count);
    }

    private async Task NotifyBookedMembersAsync(long actorUserId, List<long> sessionIds, string title, string message, CancellationToken ct)
    {
        if (sessionIds.Count == 0) return;
        var recipients = await (from booking in Db.SessionBookings
            join member in Db.MemberProfiles on booking.MemberId equals member.Id
            where sessionIds.Contains(booking.SessionId) && booking.Status == FlowStatuses.BookingBooked
            select member.UserId).Distinct().ToListAsync(ct);
        await FlowNotificationWriter.CreateAsync(Db, actorUserId, recipients, title, message, "ScheduleChanged", ct);
    }

    private static ClassDetailResponse ToResponse(ClassEntity c) => new(
        c.Id, c.CenterId, c.SportId, c.RoomId, c.Name, c.Description, c.Level,
        c.Capacity, c.DurationMinutes, c.Status, c.AllowWaitlist);

    private static ClassScheduleResponse ToResponse(ClassSchedule s, int generated) => new(
        s.Id, s.ClassId, s.RoomId, s.DayOfWeek, s.StartTime, s.EndTime, s.StartDate, s.EndDate, s.Status, generated);
}
