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

Console.WriteLine($"RESULT {checks - failures.Count}/{checks} checks passed");
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.Error.WriteLine(failure);
    Environment.ExitCode = 1;
}
