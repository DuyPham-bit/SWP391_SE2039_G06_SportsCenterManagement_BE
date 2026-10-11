using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Training;
using SportsCenterManagement.BLL.Exceptions;
using SportsCenterManagement.DAL.Context;
using SportsCenterManagement.DAL.Entities;

namespace SportsCenterManagement.BLL.Services;

public sealed class TrainingService(SportsCenterDbContext db)
{
    private IQueryable<ClassEntity> VisibleClasses(long userId) => db.Classes.Where(c =>
        db.ClassCoaches.Any(a => a.ClassId == c.Id && db.CoachProfiles.Any(p => p.Id == a.CoachId &&
            p.UserId == userId && p.Status == "Active" && p.CenterId == c.CenterId)) ||
        (db.Users.Any(u => u.Id == userId && db.Roles.Any(r => r.Id == u.RoleId && r.Name == "Manager")) &&
         db.StaffProfiles.Any(s => s.UserId == userId && s.CenterId == c.CenterId && s.Status == "Active")));

    private IQueryable<MemberProfile> ClassMembers(long classId) => db.MemberProfiles.Where(m =>
        db.SessionBookings.Any(b => b.MemberId == m.Id && b.Status == "Booked" &&
            db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == classId)) ||
        db.ClassEnrollments.Any(e => e.MemberId == m.Id && e.ClassId == classId &&
            (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")));

    public async Task<object> ListClassesAsync(long userId, CancellationToken ct)
    {
        var classes = await (from c in VisibleClasses(userId) join s in db.Sports on c.SportId equals s.Id
            orderby c.Name select new { c.Id, c.Name, SportName = s.Name, c.Capacity, c.Status }).ToListAsync(ct);
        var ids = classes.Select(c => c.Id).ToArray();
        var memberships = await (from booking in db.SessionBookings join session in db.ClassSessions on booking.SessionId equals session.Id
            where ids.Contains(session.ClassId) && booking.Status == "Booked"
            select new { session.ClassId, booking.MemberId }).Union(
            db.ClassEnrollments.Where(e => ids.Contains(e.ClassId) &&
                (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled"))
                .Select(e => new { e.ClassId, e.MemberId })).ToListAsync(ct);
        var memberIds = memberships.Select(x => x.MemberId).Distinct().ToArray();
        var members = await (from m in db.MemberProfiles join u in db.Users on m.UserId equals u.Id
            where memberIds.Contains(m.Id) && u.Status == "Active"
            select new { m.Id, MemberId = m.Id, MemberName = m.FullName, m.FullName, m.MemberCode,
                u.Email, u.Phone, m.FitnessGoal, m.FitnessLevel }).ToListAsync(ct);
        return classes.Select(c => new { c.Id, ClassId = c.Id, c.Name, c.SportName, c.Capacity, c.Status,
            Members = members.Where(m => memberships.Any(x => x.ClassId == c.Id && x.MemberId == m.Id)).ToArray() }).ToArray();
    }

    public async Task<object> ListMembersAsync(long userId, long classId, CancellationToken ct)
    {
        if (!await VisibleClasses(userId).AnyAsync(c => c.Id == classId, ct))
            throw FlowException.Forbidden("Bạn không được phụ trách lớp này.");
        return await (from m in ClassMembers(classId) join u in db.Users on m.UserId equals u.Id
            where u.Status == "Active" orderby m.FullName
            select new { m.Id, MemberId = m.Id, MemberName = m.FullName, m.FullName, m.MemberCode,
                u.Email, u.Phone, m.FitnessGoal, m.FitnessLevel }).ToListAsync(ct);
    }

    public async Task<CoachProfile> EnsureCoachAsync(long userId, long classId, long? memberId, CancellationToken ct)
    {
        var coach = await db.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == userId && c.Status == "Active", ct)
            ?? throw FlowException.Forbidden("Tài khoản chưa có hồ sơ HLV hoạt động.");
        if (!await db.Classes.AnyAsync(c => c.Id == classId && c.CenterId == coach.CenterId &&
                db.ClassCoaches.Any(a => a.ClassId == c.Id && a.CoachId == coach.Id), ct))
            throw FlowException.Forbidden("Bạn không được phân công phụ trách lớp này.");
        if (memberId.HasValue && !await ClassMembers(classId).AnyAsync(m => m.Id == memberId &&
                db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"), ct))
            throw FlowException.Forbidden("Hội viên không thuộc lớp được phân công.");
        return coach;
    }

    public static void ValidatePlan(SaveTrainingPlanRequest request)
    {
        if (request.StartDate == default || request.EndDate < request.StartDate ||
            request.Exercises is null || request.Exercises.Length is < 1 or > 50 ||
            request.Exercises.Any(e => e is null || string.IsNullOrWhiteSpace(e.Name)))
            throw FlowException.BadRequest("Ngày hoặc danh sách bài tập của giáo án không hợp lệ.");
    }

    public async Task<IReadOnlyList<TrainingPlanResponse>> ListPlansAsync(long userId, bool member, CancellationToken ct)
    {
        var query = db.TrainingPlans.AsNoTracking().Where(p => p.Status == "Active");
        if (member)
        {
            var profileId = await db.MemberProfiles.Where(m => m.UserId == userId).Select(m => m.Id).SingleAsync(ct);
            query = query.Where(p => p.MemberId == profileId || (p.MemberId == null && p.ClassId != null &&
                (db.SessionBookings.Any(b => b.MemberId == profileId && b.Status == "Booked" &&
                    db.ClassSessions.Any(s => s.Id == b.SessionId && s.ClassId == p.ClassId)) ||
                 db.ClassEnrollments.Any(e => e.MemberId == profileId && e.ClassId == p.ClassId &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")))));
        }
        else query = query.Where(p => p.ClassId != null && VisibleClasses(userId).Any(c => c.Id == p.ClassId));
        var rows = await (from p in query join coach in db.CoachProfiles on p.CoachId equals coach.Id
            orderby p.CreatedAt descending, p.Id descending
            select new { Plan = p, CoachName = coach.FullName,
                ClassName = db.Classes.Where(c => c.Id == p.ClassId).Select(c => c.Name).FirstOrDefault(),
                SportName = (from c in db.Classes join s in db.Sports on c.SportId equals s.Id where c.Id == p.ClassId select s.Name).FirstOrDefault(),
                MemberName = db.MemberProfiles.Where(m => m.Id == p.MemberId).Select(m => m.FullName).FirstOrDefault()
            }).ToListAsync(ct);
        return rows.Select(x => new TrainingPlanResponse(x.Plan.Id, x.Plan.ClassId, x.ClassName, x.SportName,
            x.Plan.MemberId, x.MemberName, x.Plan.CoachId, x.CoachName, x.Plan.Name, x.Plan.Goal,
            x.Plan.StartDate, x.Plan.EndDate, x.Plan.FitnessCondition, x.Plan.MemberId.HasValue,
            x.Plan.Status.ToUpperInvariant(), x.Plan.CreatedAt,
            x.Plan.ExercisesJson is null ? [] : JsonSerializer.Deserialize<PlanExercise[]>(x.Plan.ExercisesJson) ?? [])).ToArray();
    }

    public async Task<long> SavePlanAsync(long userId, SaveTrainingPlanRequest request, bool personal, CancellationToken ct)
    {
        ValidatePlan(request);
        if (personal != request.MemberId.HasValue) throw FlowException.BadRequest("Đối tượng giáo án không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await EnsureCoachAsync(userId, request.ClassId, request.MemberId, ct);
        var plan = personal ? await db.TrainingPlans.SingleOrDefaultAsync(p => p.ClassId == request.ClassId &&
            p.MemberId == request.MemberId && p.CoachId == coach.Id && p.Status == "Active", ct) : null;
        if (plan is null)
        {
            plan = new TrainingPlan { ClassId = request.ClassId, MemberId = request.MemberId,
                CoachId = coach.Id, PlanType = personal ? "Personal" : "Class", Status = "Active", CreatedAt = DateTime.UtcNow };
            db.TrainingPlans.Add(plan);
        }
        plan.Name = request.Title.Trim(); plan.Goal = request.Goal?.Trim();
        plan.StartDate = request.StartDate; plan.EndDate = request.EndDate;
        plan.FitnessCondition = request.FitnessCondition?.Trim();
        plan.ExercisesJson = JsonSerializer.Serialize(request.Exercises);
        plan.UpdatedAt = DateTime.UtcNow;
        AddAudit(userId, coach.CenterId, "TRAINING_PLAN_SAVED", "TrainingPlan");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return plan.Id;
    }

    public async Task DeletePlanAsync(long userId, long planId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await db.TrainingPlans.SingleOrDefaultAsync(p => p.Id == planId && p.Status == "Active", ct)
            ?? throw FlowException.NotFound("Không tìm thấy giáo án.");
        var coach = await EnsureCoachAsync(userId, plan.ClassId ?? 0, null, ct);
        if (plan.CoachId != coach.Id) throw FlowException.Forbidden("Bạn chỉ được xóa giáo án của mình.");
        plan.Status = "Archived"; plan.UpdatedAt = DateTime.UtcNow;
        AddAudit(userId, coach.CenterId, "TRAINING_PLAN_ARCHIVED", "TrainingPlan", planId);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<ProgressResponse>> ListProgressAsync(long userId, bool member, CancellationToken ct)
    {
        var query = db.TrainingResults.AsNoTracking();
        if (member) query = query.Where(r => db.MemberProfiles.Any(m => m.Id == r.MemberId && m.UserId == userId));
        else query = query.Where(r => r.ClassId != null && VisibleClasses(userId).Any(c => c.Id == r.ClassId));
        return await (from r in query join m in db.MemberProfiles on r.MemberId equals m.Id
            join coach in db.CoachProfiles on r.CoachId equals coach.Id
            orderby r.ResultDate descending, r.CreatedAt descending, r.Id descending
            select new ProgressResponse(r.Id, r.ClassId,
                db.Classes.Where(c => c.Id == r.ClassId).Select(c => c.Name).FirstOrDefault(),
                (from c in db.Classes join s in db.Sports on c.SportId equals s.Id where c.Id == r.ClassId select s.Name).FirstOrDefault(),
                m.Id, m.FullName, coach.FullName, r.ResultDate, r.Weight, r.BodyFatPercentage,
                r.MuscleMass, r.WorkoutResult ?? r.Notes, r.CoachFeedback)).ToListAsync(ct);
    }

    public async Task<long> RecordProgressAsync(long userId, RecordProgressRequest request, CancellationToken ct)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        if (date > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))) throw FlowException.BadRequest("Không ghi tiến độ cho ngày tương lai.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await EnsureCoachAsync(userId, request.ClassId, request.MemberId, ct);
        var row = new TrainingResult { MemberId = request.MemberId, ClassId = request.ClassId, CoachId = coach.Id,
            ResultDate = date, Weight = request.Weight, BodyFatPercentage = request.BodyFat, MuscleMass = request.MuscleMass,
            WorkoutResult = request.WorkoutResult.Trim(), CoachFeedback = request.CoachFeedback?.Trim(), CreatedAt = DateTime.UtcNow };
        db.TrainingResults.Add(row);
        AddAudit(userId, coach.CenterId, "TRAINING_PROGRESS_RECORDED", "TrainingResult");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return row.Id;
    }

    public async Task SendNotificationAsync(long userId, SendNotificationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await db.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == userId && c.Status == "Active", ct)
            ?? throw FlowException.Forbidden("Không có hồ sơ HLV hoạt động.");
        var classIds = await db.Classes.Where(c => c.CenterId == coach.CenterId && db.ClassCoaches.Any(a =>
            a.ClassId == c.Id && a.CoachId == coach.Id)).Select(c => c.Id).ToListAsync(ct);
        if (request.ClassId.HasValue)
        {
            if (!classIds.Contains(request.ClassId.Value)) throw FlowException.Forbidden("Bạn không được phụ trách lớp này.");
            classIds = [request.ClassId.Value];
        }
        var recipientIds = await db.MemberProfiles.Where(m => db.SessionBookings.Any(b => b.MemberId == m.Id &&
            b.Status == "Booked" && db.ClassSessions.Any(s => s.Id == b.SessionId && classIds.Contains(s.ClassId))) ||
            db.ClassEnrollments.Any(e => e.MemberId == m.Id && classIds.Contains(e.ClassId) &&
                (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")))
            .Where(m => db.Users.Any(u => u.Id == m.UserId && u.Status == "Active")).Select(m => m.UserId).ToArrayAsync(ct);
        if (recipientIds.Length == 0) throw FlowException.Conflict("Không có học viên để nhận thông báo.");
        await FlowNotificationWriter.CreateAsync(db, userId, recipientIds, request.Title.Trim(), request.Content.Trim(), "CoachMessage", ct);
        AddAudit(userId, coach.CenterId, "COACH_NOTIFICATION_SENT", "Notification");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private void AddAudit(long userId, long centerId, string action, string entity, long? id = null) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, CenterId = centerId, Action = action,
            EntityType = entity, EntityId = id, CreatedAt = DateTime.UtcNow });
}
