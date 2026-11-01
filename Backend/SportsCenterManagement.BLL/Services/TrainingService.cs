using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.BLL.DTOs.Training;
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
                u.Email, u.Phone, m.FitnessGoal, m.FitnessLevel, m.DateOfBirth, m.Gender,
                m.HeightCm, m.WeightKg, m.MedicalNote }).ToListAsync(ct);
        return classes.Select(c => new { c.Id, ClassId = c.Id, c.Name, c.SportName, c.Capacity, c.Status,
            Members = members.Where(m => memberships.Any(x => x.ClassId == c.Id && x.MemberId == m.Id)).ToArray() }).ToArray();
    }

    public async Task<object> ListMembersAsync(long userId, long classId, CancellationToken ct)
    {
        if (!await VisibleClasses(userId).AnyAsync(c => c.Id == classId, ct))
            throw new InvalidOperationException("Bạn không được phân công phụ trách lớp này.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "CO-02",
                    ["ProposedHandling"] = "Kiểm tra quyền ở mỗi yêu cầu; từ chối truy cập và không trả về dữ liệu học viên."
                }
            };
        return await (from m in ClassMembers(classId) join u in db.Users on m.UserId equals u.Id
            where u.Status == "Active" orderby m.FullName
            select new { m.Id, MemberId = m.Id, MemberName = m.FullName, m.FullName, m.MemberCode,
                u.Email, u.Phone, m.FitnessGoal, m.FitnessLevel, m.DateOfBirth, m.Gender,
                m.HeightCm, m.WeightKg, m.MedicalNote }).ToListAsync(ct);
    }

    public async Task<CoachProfile> EnsureCoachAsync(long userId, long classId, long? memberId, CancellationToken ct)
    {
        var coach = await db.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == userId && c.Status == "Active", ct)
            ?? throw new InvalidOperationException("Tài khoản chưa có hồ sơ HLV hoạt động.") { Data = { ["StatusCode"] = 403 } };
        if (!await db.Classes.AnyAsync(c => c.Id == classId && c.CenterId == coach.CenterId &&
                db.ClassCoaches.Any(a => a.ClassId == c.Id && a.CoachId == coach.Id), ct))
            throw new InvalidOperationException("Bạn không được phân công phụ trách lớp này.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "CO-02",
                    ["ProposedHandling"] = "Kiểm tra quyền ở mỗi yêu cầu; từ chối truy cập và không trả về dữ liệu học viên."
                }
            };
        if (memberId.HasValue && !await ClassMembers(classId).AnyAsync(m => m.Id == memberId &&
                db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"), ct))
            throw new InvalidOperationException("Hội viên không thuộc danh sách lớp được phân công.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "CO-02",
                    ["ProposedHandling"] = "Kiểm tra quyền ở mỗi yêu cầu; từ chối truy cập và không trả về dữ liệu học viên."
                }
            };
        return coach;
    }

    private async Task<CoachProfile> EnsureSessionCoachAsync(
        long userId,
        ClassSession session,
        long memberId,
        CancellationToken ct)
    {
        var coach = await db.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == userId && c.Status == "Active", ct)
            ?? throw new InvalidOperationException("Tài khoản chưa có hồ sơ HLV hoạt động.") { Data = { ["StatusCode"] = 403 } };
        var sameCenter = await db.Classes.AnyAsync(c => c.Id == session.ClassId && c.CenterId == coach.CenterId, ct);
        var isEffectiveCoach = session.CoachId.HasValue
            ? session.CoachId.Value == coach.Id
            : await db.ClassCoaches.AnyAsync(a => a.ClassId == session.ClassId && a.CoachId == coach.Id, ct);
        if (!sameCenter || !isEffectiveCoach)
            throw new InvalidOperationException("Bạn không được phân công phụ trách buổi học này.")
            {
                Data =
                {
                    ["StatusCode"] = 403,
                    ["Code"] = "CO-02",
                    ["ProposedHandling"] = "Chỉ HLV hiệu lực của buổi học (HLV chính hoặc HLV dạy thay) được ghi kết quả."
                }
            };
        if (!await ClassMembers(session.ClassId).AnyAsync(m => m.Id == memberId &&
                db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"), ct))
            throw new InvalidOperationException("Hội viên không thuộc danh sách lớp của buổi học.")
            {
                Data = { ["StatusCode"] = 403, ["Code"] = "CO-02" }
            };
        return coach;
    }

    public static void ValidatePlan(SaveTrainingPlanRequest request)
    {
        var validationError = CoachWorkflowRules.PlanValidationError(request);
        if (validationError is not null)
            throw new InvalidOperationException(validationError)
            {
                Data =
                {
                    ["StatusCode"] = 400,
                    ["Code"] = "CO-03",
                    ["ProposedHandling"] = "Yêu cầu bổ sung thông tin cần thiết; hiển thị rõ đối tượng nhận trước khi giao kế hoạch. Cho lưu nháp khi chưa đủ thông tin."
                }
            };
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

        var planIds = rows.Select(x => x.Plan.Id).ToArray();
        var tableExercises = await (from tpe in db.TrainingPlanExercises
            join ex in db.Exercises on tpe.ExerciseId equals ex.Id
            where planIds.Contains(tpe.TrainingPlanId)
            orderby tpe.SortOrder, tpe.Id
            select new {
                tpe.TrainingPlanId,
                Exercise = new PlanExercise(
                    ex.Name,
                    tpe.Sets.HasValue ? tpe.Sets.Value.ToString() : null,
                    tpe.Repetitions.HasValue ? tpe.Repetitions.Value.ToString() : (tpe.DurationSeconds.HasValue ? $"{tpe.DurationSeconds}s" : null),
                    tpe.Notes ?? ex.Description,
                    null,
                    null
                )
            }).ToListAsync(ct);

        var exercisesByPlanId = tableExercises
            .GroupBy(x => x.TrainingPlanId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Exercise).ToArray());

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        return rows.Select(x => {
            PlanExercise[] exercises = [];
            if (!string.IsNullOrWhiteSpace(x.Plan.ExercisesJson))
            {
                try
                {
                    exercises = JsonSerializer.Deserialize<PlanExercise[]>(x.Plan.ExercisesJson, jsonOpts) ?? [];
                }
                catch (JsonException)
                {
                    // Older plans may hold malformed legacy JSON; use the normalized exercise rows when available.
                }
            }
            if (exercises.Length == 0 && exercisesByPlanId.TryGetValue(x.Plan.Id, out var fromTable))
            {
                exercises = fromTable;
            }
            return new TrainingPlanResponse(x.Plan.Id, x.Plan.ClassId, x.ClassName, x.SportName,
                x.Plan.MemberId, x.MemberName, x.Plan.CoachId, x.CoachName, x.Plan.Name, x.Plan.Goal,
                x.Plan.StartDate, x.Plan.EndDate, x.Plan.FitnessCondition, x.Plan.MemberId.HasValue,
                x.Plan.Status.ToUpperInvariant(), x.Plan.CreatedAt, exercises);
        }).ToArray();
    }

    public async Task<long> SavePlanAsync(long userId, SaveTrainingPlanRequest request, bool personal, CancellationToken ct)
    {
        ValidatePlan(request);
        if (personal != request.MemberId.HasValue) throw new InvalidOperationException("Đối tượng giáo án không hợp lệ.") { Data = { ["StatusCode"] = 400 } };
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
        var recipients = await ClassMembers(request.ClassId)
            .Where(m => (!personal || m.Id == request.MemberId) && db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"))
            .Select(m => m.UserId).ToArrayAsync(ct);
        await FlowNotificationWriter.CreateAsync(db, userId, recipients, "Giáo án tập luyện được cập nhật",
            $"HLV {coach.FullName} đã cập nhật giáo án: {plan.Name}. Xem Giáo án & Kết quả tập để biết chi tiết.", "TrainingPlanUpdated", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return plan.Id;
    }

    public async Task DeletePlanAsync(long userId, long planId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var plan = await db.TrainingPlans.SingleOrDefaultAsync(p => p.Id == planId && p.Status == "Active", ct)
            ?? throw new InvalidOperationException("Không tìm thấy giáo án.") { Data = { ["StatusCode"] = 404 } };
        var coach = await EnsureCoachAsync(userId, plan.ClassId ?? 0, null, ct);
        if (plan.CoachId != coach.Id) throw new InvalidOperationException("Bạn chỉ được xóa giáo án của mình.") { Data = { ["StatusCode"] = 403 } };
        plan.Status = "Archived"; plan.UpdatedAt = DateTime.UtcNow;
        AddAudit(userId, coach.CenterId, "TRAINING_PLAN_ARCHIVED", "TrainingPlan", planId);
        var recipients = await ClassMembers(plan.ClassId ?? 0)
            .Where(m => (plan.MemberId == null || m.Id == plan.MemberId) && db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"))
            .Select(m => m.UserId).ToArrayAsync(ct);
        await FlowNotificationWriter.CreateAsync(db, userId, recipients, "Giáo án đã được thu hồi",
            $"HLV {coach.FullName} đã thu hồi giáo án: {plan.Name}.", "TrainingPlanUpdated", ct);
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
                r.MuscleMass, r.WorkoutResult ?? r.Notes, r.CoachFeedback, r.SessionId,
                db.ClassSessions.Where(s => s.Id == r.SessionId).Select(s => (TimeOnly?)s.StartTime).FirstOrDefault(),
                db.ClassSessions.Where(s => s.Id == r.SessionId).Select(s => (TimeOnly?)s.EndTime).FirstOrDefault())).ToListAsync(ct);
    }

    public async Task<long> RecordProgressAsync(long userId, RecordProgressRequest request, CancellationToken ct)
    {
        var validationError = CoachWorkflowRules.ProgressValidationError(request);
        if (validationError is not null)
            throw new InvalidOperationException(validationError) { Data = { ["StatusCode"] = 400, ["Code"] = "CO-04" } };
        var date = request.Date ?? ScheduleTime.VietnamToday;
        if (date > ScheduleTime.VietnamToday) throw new InvalidOperationException("Không ghi tiến độ cho ngày tương lai.") { Data = { ["StatusCode"] = 400 } };
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        CoachProfile coach;
        if (request.SessionId.HasValue)
        {
            var session = await db.ClassSessions.SingleOrDefaultAsync(s => s.Id == request.SessionId && s.ClassId == request.ClassId, ct)
                ?? throw new InvalidOperationException("Buổi tập không thuộc lớp đã chọn.") { Data = { ["StatusCode"] = 400 } };
            coach = await EnsureSessionCoachAsync(userId, session, request.MemberId, ct);
            if (session.SessionStatus == "Cancelled" ||
                CoachWorkflowRules.SessionEndVietnam(session.SessionDate, session.StartTime, session.EndTime) > ScheduleTime.VietnamNow)
                throw new InvalidOperationException("Chỉ ghi kết quả sau khi buổi tập kết thúc và không bị hủy.") { Data = { ["StatusCode"] = 409 } };
            var eligible = await db.SessionBookings.AnyAsync(b => b.SessionId == session.Id && b.MemberId == request.MemberId && b.Status == "Booked", ct) ||
                await db.ClassEnrollments.AnyAsync(e => e.ClassId == session.ClassId && e.MemberId == request.MemberId &&
                    (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled"), ct);
            if (!eligible)
                throw new InvalidOperationException("Học viên chưa đăng ký buổi tập hoặc chưa ghi danh vào lớp này.") { Data = { ["StatusCode"] = 400 } };
            if (request.Date.HasValue && request.Date != session.SessionDate)
                throw new InvalidOperationException("Ngày ghi nhận phải trùng ngày của buổi tập.") { Data = { ["StatusCode"] = 400 } };
            date = session.SessionDate;
        }
        else coach = await EnsureCoachAsync(userId, request.ClassId, request.MemberId, ct);

        var workoutResult = request.WorkoutResult.Trim();
        var coachFeedback = string.IsNullOrWhiteSpace(request.CoachFeedback) ? null : request.CoachFeedback.Trim();
        var row = request.SessionId.HasValue
            ? await db.TrainingResults.SingleOrDefaultAsync(r => r.SessionId == request.SessionId && r.MemberId == request.MemberId, ct)
            : null;
        var isNew = row is null;
        if (row is null)
        {
            row = new TrainingResult { MemberId = request.MemberId, SessionId = request.SessionId, CreatedAt = DateTime.UtcNow };
            db.TrainingResults.Add(row);
        }
        else if (row.ClassId == request.ClassId && row.CoachId == coach.Id && row.ResultDate == date &&
                 row.Weight == request.Weight && row.BodyFatPercentage == request.BodyFat && row.MuscleMass == request.MuscleMass &&
                 row.WorkoutResult == workoutResult && row.CoachFeedback == coachFeedback)
        {
            await tx.CommitAsync(ct);
            return row.Id;
        }

        row.ClassId = request.ClassId;
        row.CoachId = coach.Id;
        row.ResultDate = date;
        row.Weight = request.Weight;
        row.BodyFatPercentage = request.BodyFat;
        row.MuscleMass = request.MuscleMass;
        row.WorkoutResult = workoutResult;
        row.CoachFeedback = coachFeedback;
        AddAudit(userId, coach.CenterId, isNew ? "TRAINING_PROGRESS_RECORDED" : "TRAINING_PROGRESS_UPDATED", "TrainingResult", row.Id > 0 ? row.Id : null);
        if (request.NotifyMember)
            await FlowNotificationWriter.CreateForMemberAsync(db, userId, request.MemberId, "Có kết quả tập luyện mới",
                $"HLV {coach.FullName} đã {(isNew ? "ghi nhận" : "cập nhật")} kết quả tập ngày {date:dd/MM/yyyy}. Xem Giáo án & Kết quả tập để biết chi tiết.", "ProgressUpdated", ct);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return row.Id;
    }

    public async Task<RecommendationContext> GetRecommendationContextAsync(long userId, long classId, long memberId, CancellationToken ct)
    {
        await EnsureCoachAsync(userId, classId, memberId, ct);
        var profile = await db.MemberProfiles.AsNoTracking().SingleAsync(m => m.Id == memberId, ct);
        var sport = await (from c in db.Classes join s in db.Sports on c.SportId equals s.Id
            where c.Id == classId select s.Name).SingleAsync(ct);
        var history = await db.TrainingResults.AsNoTracking().Where(r => r.MemberId == memberId && r.ClassId == classId)
            .OrderByDescending(r => r.ResultDate).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Take(10)
            .Select(r => new RecommendationHistory(r.ResultDate, r.Weight, r.BodyFatPercentage, r.MuscleMass,
                r.WorkoutResult ?? r.Notes, r.CoachFeedback)).ToArrayAsync(ct);

        int? age = null;
        if (profile.DateOfBirth.HasValue)
        {
            var today = ScheduleTime.VietnamToday;
            age = today.Year - profile.DateOfBirth.Value.Year;
            if (profile.DateOfBirth.Value > today.AddYears(-age.Value)) age--;
        }

        var latestWeight = history.FirstOrDefault()?.Weight ?? profile.WeightKg;
        decimal? bmi = null;
        string? bmiCategory = null;
        if (profile.HeightCm.HasValue && profile.HeightCm.Value > 0 && latestWeight.HasValue && latestWeight.Value > 0)
        {
            var hM = profile.HeightCm.Value / 100m;
            bmi = Math.Round(latestWeight.Value / (hM * hM), 1);
            bmiCategory = bmi switch
            {
                < 18.5m => "Underweight (Gầy / Thiếu cân)",
                < 25.0m => "Normal (Cân đối / Thể hình chuẩn)",
                < 30.0m => "Overweight (Thừa cân / Cần kiểm soát mỡ)",
                _ => "Obese (Béo phì / Cần giảm cân an toàn)"
            };
        }

        var healthRecords = await db.MemberHealthRecords.AsNoTracking()
            .Where(x => x.MemberId == memberId && x.Status == "Active")
            .OrderByDescending(x => x.OccurredOn)
            .Select(x => new HealthRecordSummary(x.RecordType, x.Title, x.Details, x.OccurredOn))
            .ToArrayAsync(ct);

        return new RecommendationContext(
            sport,
            profile.FitnessGoal,
            profile.FitnessLevel,
            profile.MedicalNote,
            history,
            profile.HeightCm,
            latestWeight,
            profile.Gender,
            profile.DateOfBirth,
            age,
            bmi,
            bmiCategory,
            healthRecords
        );
    }

    public async Task SendNotificationAsync(long userId, SendNotificationRequest request, CancellationToken ct)
    {
        var validationError = CoachWorkflowRules.NotificationValidationError(request);
        if (validationError is not null)
            throw new InvalidOperationException(validationError) { Data = { ["StatusCode"] = 400, ["Code"] = "CO-05" } };
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var coach = await db.CoachProfiles.SingleOrDefaultAsync(c => c.UserId == userId && c.Status == "Active", ct)
            ?? throw new InvalidOperationException("Không có hồ sơ HLV hoạt động.") { Data = { ["StatusCode"] = 403 } };
        var classIds = await db.Classes.Where(c => c.CenterId == coach.CenterId && db.ClassCoaches.Any(a =>
            a.ClassId == c.Id && a.CoachId == coach.Id)).Select(c => c.Id).ToListAsync(ct);
        var requestedClassIds = CoachWorkflowRules.NormalizeClassTargets(request.ClassIds);
        if (request.ClassId.HasValue)
        {
            if (!classIds.Contains(request.ClassId.Value)) throw new InvalidOperationException("Bạn không được phụ trách lớp này.") { Data = { ["StatusCode"] = 403 } };
            classIds = [request.ClassId.Value];
        }
        else if (requestedClassIds.Length > 0)
        {
            if (requestedClassIds.Except(classIds).Any())
                throw new InvalidOperationException("Danh sách có lớp bạn không được phụ trách.") { Data = { ["StatusCode"] = 403 } };
            classIds = requestedClassIds.ToList();
        }
        var recipientQuery = db.MemberProfiles.Where(m => db.SessionBookings.Any(b => b.MemberId == m.Id &&
            b.Status == "Booked" && db.ClassSessions.Any(s => s.Id == b.SessionId && classIds.Contains(s.ClassId))) ||
            db.ClassEnrollments.Any(e => e.MemberId == m.Id && classIds.Contains(e.ClassId) &&
                (e.Status == "Active" || e.Status == "Confirmed" || e.Status == "Enrolled")))
            .Where(m => db.Users.Any(u => u.Id == m.UserId && u.Status == "Active"));

        if (request.MemberId.HasValue)
        {
            recipientQuery = recipientQuery.Where(m => m.Id == request.MemberId.Value);
        }

        var recipientIds = await recipientQuery.Select(m => m.UserId).Distinct().ToArrayAsync(ct);
        if (recipientIds.Length == 0) throw new InvalidOperationException("Không có học viên để nhận thông báo.") { Data = { ["StatusCode"] = 409 } };
        var targetClassIds = request.MemberId.HasValue ? null : JsonSerializer.Serialize(classIds);
        await FlowNotificationWriter.CreateAsync(db, userId, recipientIds, request.Title.Trim(), request.Content.Trim(),
            "CoachMessage", ct, targetClassIds);
        AddAudit(userId, coach.CenterId, "COACH_NOTIFICATION_SENT", "Notification");
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }

    private void AddAudit(long userId, long centerId, string action, string entity, long? id = null) =>
        db.AuditLogs.Add(new AuditLog { UserId = userId, CenterId = centerId, Action = action,
            EntityType = entity, EntityId = id, CreatedAt = DateTime.UtcNow });
}
