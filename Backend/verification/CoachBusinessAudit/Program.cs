using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using SportsCenterManagement.BLL.Common.Helpers;
using SportsCenterManagement.BLL.DTOs.Training;
using SportsCenterManagement.BLL.Services;

var failures = new List<string>();
var checks = 0;

void Check(string name, Func<bool> assertion)
{
    checks++;
    try
    {
        if (!assertion()) throw new InvalidOperationException("Assertion failed");
        Console.WriteLine("PASS " + name);
    }
    catch (Exception exception)
    {
        failures.Add(name + ": " + exception.Message);
        Console.WriteLine("FAIL " + name + ": " + exception.Message);
    }
}

SaveTrainingPlanRequest Plan(string title = "Giáo án", PlanExercise[]? exercises = null) =>
    new(10, 20, title, "Tăng sức bền", new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 25), null,
        exercises ?? [new PlanExercise("Cable Row", "3", "12", null, 1, "Monday")]);

RecordProgressRequest Progress(
    string result = "Hoàn thành đúng kỹ thuật",
    decimal? weight = 65,
    decimal? bodyFat = 18,
    decimal? muscleMass = 30) =>
    new(10, 20, weight, bodyFat, muscleMass, result, "Tiến bộ tốt", new DateOnly(2026, 10, 25), 30);

Check("session override authorizes only the replacement coach", () =>
    CoachWorkflowRules.IsEffectiveSessionCoach(9, 9, [3]) &&
    !CoachWorkflowRules.IsEffectiveSessionCoach(3, 9, [3]));

Check("class coach is effective when a session has no override", () =>
    CoachWorkflowRules.IsEffectiveSessionCoach(3, null, [3, 4]) &&
    !CoachWorkflowRules.IsEffectiveSessionCoach(8, null, [3, 4]));

Check("active class enrollment states are explicit", () =>
    CoachWorkflowRules.IsActiveEnrollmentStatus("Active") &&
    CoachWorkflowRules.IsActiveEnrollmentStatus("Confirmed") &&
    CoachWorkflowRules.IsActiveEnrollmentStatus("Enrolled") &&
    !CoachWorkflowRules.IsActiveEnrollmentStatus("Pending") &&
    !CoachWorkflowRules.IsActiveEnrollmentStatus("Cancelled"));

Check("attendance dashboard recognizes the API's canonical status values", () =>
    CoachWorkflowRules.CountsAsPresentAttendance("PRESENT") &&
    CoachWorkflowRules.CountsAsPresentAttendance("LATE") &&
    !CoachWorkflowRules.CountsAsPresentAttendance("ABSENT") &&
    CoachWorkflowRules.CountsAsAbsentAttendance("ABSENT") &&
    !CoachWorkflowRules.CountsAsAbsentAttendance("EXCUSED"));

Check("overnight session ends on the following Vietnam business day", () =>
    CoachWorkflowRules.SessionEndVietnam(new DateOnly(2026, 10, 25), new TimeOnly(23, 30), new TimeOnly(1, 0)) ==
    new DateTime(2026, 10, 26, 1, 0, 0));

Check("valid class or personal training plan passes validation", () =>
    CoachWorkflowRules.PlanValidationError(Plan()) is null &&
    CoachWorkflowRules.PlanValidationError(Plan() with { MemberId = null }) is null);

Check("blank plan and exercise names are rejected", () =>
    CoachWorkflowRules.PlanValidationError(Plan("   ")) is not null &&
    CoachWorkflowRules.PlanValidationError(Plan(exercises: [new PlanExercise("  ", "3", "12", null)])) is not null);

Check("invalid scheduled weekday is rejected", () =>
    CoachWorkflowRules.PlanValidationError(Plan(exercises: [new PlanExercise("Cable Row", "3", "12", null, 1, "Someday")])) is not null);

Check("valid workout progress passes medical metric validation", () =>
    CoachWorkflowRules.ProgressValidationError(Progress()) is null);

Check("blank result and unsafe metric ranges are rejected", () =>
    CoachWorkflowRules.ProgressValidationError(Progress(result: "  ")) is not null &&
    CoachWorkflowRules.ProgressValidationError(Progress(weight: 301)) is not null &&
    CoachWorkflowRules.ProgressValidationError(Progress(bodyFat: 66)) is not null &&
    CoachWorkflowRules.ProgressValidationError(Progress(muscleMass: 121)) is not null);

UpdateMemberBiometricsRequest Bio(decimal? height = 170, decimal? weight = 65, string? gender = "Female",
    DateOnly? dob = null, string? level = "Beginner") =>
    new(height, weight, gender, dob ?? new DateOnly(2000, 1, 1), level, "Đau gối nhẹ");

Check("valid manual biometrics pass validation", () =>
    CoachWorkflowRules.BiometricsValidationError(Bio(), new DateOnly(2026, 11, 9)) is null);

Check("unsafe manual biometrics are rejected", () =>
    CoachWorkflowRules.BiometricsValidationError(Bio(height: 50), new DateOnly(2026, 11, 9)) is not null &&
    CoachWorkflowRules.BiometricsValidationError(Bio(weight: 301), new DateOnly(2026, 11, 9)) is not null &&
    CoachWorkflowRules.BiometricsValidationError(Bio(gender: "Robot"), new DateOnly(2026, 11, 9)) is not null &&
    CoachWorkflowRules.BiometricsValidationError(Bio(level: "Godlike"), new DateOnly(2026, 11, 9)) is not null &&
    CoachWorkflowRules.BiometricsValidationError(Bio(dob: new DateOnly(2027, 1, 1)), new DateOnly(2026, 11, 9)) is not null);

Check("newer training_results weight overrides older profile weight", () =>
{
    var pick = CoachWorkflowRules.ResolveLatestWeight(55, new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc),
        new DateOnly(2026, 11, 5), 57.5m, new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
    return pick.Weight == 57.5m && pick.Source == "training_result" && pick.MeasuredOn == new DateOnly(2026, 11, 5);
});

Check("manual profile weight entered after the last session wins", () =>
{
    var pick = CoachWorkflowRules.ResolveLatestWeight(60, new DateTime(2026, 11, 8, 3, 0, 0, DateTimeKind.Utc),
        new DateOnly(2026, 11, 5), 57.5m, new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc));
    return pick.Weight == 60 && pick.Source == "profile";
});

Check("profile weight is used when no session weight was measured", () =>
{
    var pick = CoachWorkflowRules.ResolveLatestWeight(55, null, null, null, null);
    var none = CoachWorkflowRules.ResolveLatestWeight(null, null, null, null, null);
    return pick.Weight == 55 && pick.Source == "profile" && none.Weight is null;
});

Check("multi-class notification target is normalized and accepted", () =>
{
    var request = new SendNotificationRequest(null, "Lịch tập", "Nội dung", null, [3, 3, 4]);
    return CoachWorkflowRules.NotificationValidationError(request) is null &&
        CoachWorkflowRules.NormalizeClassTargets(request.ClassIds).SequenceEqual([3L, 4L]);
});

Check("ambiguous or blank notification is rejected", () =>
    CoachWorkflowRules.NotificationValidationError(new SendNotificationRequest(3, " ", "Nội dung")) is not null &&
    CoachWorkflowRules.NotificationValidationError(new SendNotificationRequest(3, "Tiêu đề", "Nội dung", null, [4])) is not null &&
    CoachWorkflowRules.NotificationValidationError(new SendNotificationRequest(3, "Tiêu đề", "Nội dung", 20)) is not null &&
    CoachWorkflowRules.NotificationValidationError(new SendNotificationRequest(null, "Tiêu đề", "Nội dung", null, [])) is not null);

Check("homework must target exactly one class or one member", () =>
    CoachWorkflowRules.HomeworkValidationError(null, 20, "Bài tập", "Mô tả") is null &&
    CoachWorkflowRules.HomeworkValidationError(10, null, "Bài tập", "Mô tả") is null &&
    CoachWorkflowRules.HomeworkValidationError(10, 20, "Bài tập", "Mô tả") is not null &&
    CoachWorkflowRules.HomeworkValidationError(null, null, "Bài tập", "Mô tả") is not null);

Check("homework review requires a score or feedback", () =>
    CoachWorkflowRules.HomeworkReviewValidationError(null, "  ") is not null &&
    CoachWorkflowRules.HomeworkReviewValidationError(85, null) is null &&
    CoachWorkflowRules.HomeworkReviewValidationError(null, "Cần giữ lưng trung tính") is null);

Check("broadcast rejects ambiguous and explicitly empty targets", () =>
    CoachWorkflowRules.BroadcastValidationError(10, [20], "Thông báo", "Nội dung") is not null &&
    CoachWorkflowRules.BroadcastValidationError(null, [], "Thông báo", "Nội dung") is not null &&
    CoachWorkflowRules.BroadcastValidationError(null, null, "Thông báo", "Nội dung") is null);

Check("session booking requires an explicit member-center assignment", () =>
    BookingAccessRules.IsMemberAssignedToClassCenter(7, 7) &&
    !BookingAccessRules.IsMemberAssignedToClassCenter(7, 8) &&
    !BookingAccessRules.IsMemberAssignedToClassCenter(null, 7));

Check("VNPay callback rejects a valid signature with a mismatched currency", () =>
{
    const string secret = "audit-vnpay-secret";
    var configuration = new AuditConfiguration(new Dictionary<string, string?>
    {
        ["VnPay:HashSecret"] = secret,
        ["VnPay:TmnCode"] = "AUDITTMN",
        ["VnPay:CurrCode"] = "VND"
    });
    var signer = new VnPayLibrary();
    foreach (var (key, value) in new Dictionary<string, string>
    {
        ["vnp_Amount"] = "100000",
        ["vnp_CurrCode"] = "USD",
        ["vnp_ResponseCode"] = "00",
        ["vnp_TmnCode"] = "AUDITTMN",
        ["vnp_TransactionNo"] = "123456",
        ["vnp_TransactionStatus"] = "00",
        ["vnp_TxnRef"] = "PAY-AUDIT"
    }) signer.AddRequestData(key, value);

    var signedUri = new Uri(signer.CreateRequestUrl("https://example.test/callback", secret));
    var fields = signedUri.Query.TrimStart('?').Split('&').Select(pair => pair.Split('=', 2))
        .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]), StringComparer.Ordinal);
    var callback = new VnPayService(configuration, new HttpClient(), new BusinessClock(configuration)).ProcessCallback(fields);
    return !callback.IsValidSignature && !callback.IsSuccess;
});

Console.WriteLine($"RESULT {checks - failures.Count}/{checks} checks passed");
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine(failure);
    Environment.ExitCode = 1;
}

sealed class AuditConfiguration(Dictionary<string, string?> values) : IConfiguration
{
    public string? this[string key]
    {
        get => values.GetValueOrDefault(key);
        set => values[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];
    public IChangeToken GetReloadToken() => new AuditChangeToken();
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
}

sealed class AuditChangeToken : IChangeToken
{
    public bool HasChanged => false;
    public bool ActiveChangeCallbacks => false;
    public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new NoopDisposable();

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
