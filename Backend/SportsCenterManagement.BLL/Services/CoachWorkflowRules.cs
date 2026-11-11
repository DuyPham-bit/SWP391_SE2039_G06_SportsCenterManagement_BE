using SportsCenterManagement.BLL.DTOs.Training;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Deterministic Coach workflow rules shared by API services and executable audits.
/// Database scope checks remain in their owning service/controller.
/// </summary>
public static class CoachWorkflowRules
{
    private static readonly HashSet<string> ActiveEnrollmentStatuses =
        new(["Active", "Confirmed", "Enrolled"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> WeekDays =
        new(["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
            StringComparer.OrdinalIgnoreCase);

    public static bool IsActiveEnrollmentStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && ActiveEnrollmentStatuses.Contains(status);

    // Attendance writes use the uppercase API contract; dashboard aggregation must not
    // silently drop those persisted values because of presentation casing.
    public static bool CountsAsPresentAttendance(string? status) =>
        string.Equals(status, "PRESENT", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "LATE", StringComparison.OrdinalIgnoreCase);

    public static bool CountsAsAbsentAttendance(string? status) =>
        string.Equals(status, "ABSENT", StringComparison.OrdinalIgnoreCase);

    public static bool IsEffectiveSessionCoach(
        long coachId,
        long? sessionCoachId,
        IEnumerable<long> assignedClassCoachIds) =>
        sessionCoachId.HasValue
            ? sessionCoachId.Value == coachId
            : assignedClassCoachIds.Contains(coachId);

    public static DateTime SessionEndVietnam(DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime)
    {
        var start = sessionDate.ToDateTime(startTime);
        var end = sessionDate.ToDateTime(endTime);
        return end <= start ? end.AddDays(1) : end;
    }

    public static string? PlanValidationError(SaveTrainingPlanRequest request)
    {
        if (request.StartDate == default || request.EndDate < request.StartDate)
            return "Ngày bắt đầu hoặc ngày kết thúc của giáo án không hợp lệ.";
        if (string.IsNullOrWhiteSpace(request.Title))
            return "Tên giáo án không được để trống.";
        if (request.Exercises is null || request.Exercises.Length is < 1 or > 50)
            return "Giáo án phải có từ 1 đến 50 bài tập.";
        if (request.Exercises.Any(exercise => exercise is null || string.IsNullOrWhiteSpace(exercise.Name)))
            return "Tên bài tập không được để trống.";
        if (request.Exercises.Any(exercise => exercise.WeekNumber is < 1 or > 52 ||
                (!string.IsNullOrWhiteSpace(exercise.DayOfWeek) && !WeekDays.Contains(exercise.DayOfWeek.Trim()))))
            return "Tuần hoặc ngày tập trong giáo án không hợp lệ.";
        return null;
    }

    public static string? ProgressValidationError(RecordProgressRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.WorkoutResult))
            return "Kết quả buổi tập không được để trống.";
        if (request.Weight is < 25 or > 300)
            return "Cân nặng chỉ chấp nhận từ 25 đến 300 kg.";
        if (request.BodyFat is < 2 or > 65)
            return "Tỷ lệ mỡ chỉ chấp nhận từ 2% đến 65%.";
        if (request.MuscleMass is < 10 or > 120)
            return "Khối lượng cơ chỉ chấp nhận từ 10 đến 120 kg.";
        return null;
    }

    public static readonly string[] FitnessLevels = ["Beginner", "Intermediate", "Advanced", "Athlete"];

    /// <summary>Blank means "not set" (true, null); otherwise returns the canonical level or false if unknown.</summary>
    public static bool TryCanonicalFitnessLevel(string? value, out string? canonical)
    {
        canonical = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        canonical = FitnessLevels.FirstOrDefault(l => l.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        return canonical is not null;
    }

    public static string? BiometricsValidationError(UpdateMemberBiometricsRequest request)
    {
        if (request.HeightCm is < 80 or > 250)
            return "Chiều cao chỉ chấp nhận từ 80 đến 250 cm.";
        if (request.WeightKg is < 25 or > 300)
            return "Cân nặng chỉ chấp nhận từ 25 đến 300 kg.";
        return null;
    }

    /// <summary>
    /// The newest body weight measured in training_results always takes priority; the manually entered
    /// profile weight is only a fallback when no session has recorded a weight yet.
    /// </summary>
    public static WeightPick ResolveLatestWeight(decimal? profileWeight, DateTime? profileUpdatedAtUtc,
        DateOnly? resultDate, decimal? resultWeight)
    {
        if (resultWeight is > 0 && resultDate.HasValue)
            return new(resultWeight, "training_result", resultDate);
        return profileWeight is > 0
            ? new(profileWeight, "profile", profileUpdatedAtUtc.HasValue ? DateOnly.FromDateTime(profileUpdatedAtUtc.Value.AddHours(7)) : null)
            : new(null, null, null);
    }

    public static long[] NormalizeClassTargets(long[]? classIds) =>
        (classIds ?? []).Where(id => id > 0).Distinct().ToArray();

    public static string? NotificationValidationError(SendNotificationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return "Tiêu đề và nội dung thông báo không được để trống.";
        if (request.ClassId is <= 0 || request.MemberId is <= 0 || (request.ClassIds?.Any(id => id <= 0) ?? false))
            return "Đối tượng nhận thông báo không hợp lệ.";
        if (request.ClassIds is { Length: 0 })
            return "Danh sách lớp nhận thông báo không được rỗng; dùng null nếu muốn gửi tất cả lớp phụ trách.";

        var classIds = NormalizeClassTargets(request.ClassIds);
        if (classIds.Length > 100)
            return "Mỗi lần chỉ được chọn tối đa 100 lớp.";
        if (request.ClassId.HasValue && classIds.Length > 0)
            return "Chỉ dùng ClassId hoặc ClassIds cho một lần gửi thông báo.";
        if (request.MemberId.HasValue && (request.ClassId.HasValue || classIds.Length > 0))
            return "Chỉ được chọn học viên hoặc lớp cho một lần gửi thông báo.";
        return null;
    }

    public static string? HomeworkValidationError(
        long? classId,
        long? memberId,
        string? title,
        string? description)
    {
        if (classId is <= 0 || memberId is <= 0)
            return "Đối tượng nhận bài tập không hợp lệ.";
        if (classId.HasValue == memberId.HasValue)
            return "Bài tập phải được giao cho đúng một đối tượng: một lớp hoặc một học viên.";
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return "Tiêu đề và nội dung bài tập không được để trống.";
        return null;
    }

    public static string? HomeworkReviewValidationError(decimal? score, string? coachFeedback) =>
        !score.HasValue && string.IsNullOrWhiteSpace(coachFeedback)
            ? "Cần nhập điểm hoặc nhận xét trước khi đánh dấu bài đã chấm."
            : null;

    public static string? BroadcastValidationError(
        long? classId,
        long[]? memberIds,
        string? title,
        string? content)
    {
        if (classId is <= 0 || (memberIds?.Any(id => id <= 0) ?? false))
            return "Đối tượng nhận thông báo không hợp lệ.";
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
            return "Tiêu đề và nội dung thông báo không được để trống.";
        if (classId.HasValue && memberIds is { Length: > 0 })
            return "Chỉ được chọn một lớp hoặc danh sách học viên cho mỗi thông báo.";
        if (!classId.HasValue && memberIds is { Length: 0 })
            return "Danh sách học viên không được rỗng; dùng null nếu muốn gửi tất cả học viên phụ trách.";
        return null;
    }
}
