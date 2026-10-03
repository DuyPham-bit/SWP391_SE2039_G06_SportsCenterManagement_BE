namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record CreateClassRequest(
    long CenterId,
    long SportId,
    long? RoomId,
    string Name,
    string? Description,
    string? Level,
    int Capacity,
    int DurationMinutes);

public sealed record CreateClassScheduleRequest(
    long? RoomId,
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record ClassSessionResponse(
    long Id,
    long ClassId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    long? RoomId,
    long? CoachId,
    string Status);

public sealed record CenterScheduleResponse(
    long ClassId,
    string ClassName,
    long? RoomId,
    long? CoachId,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity,
    int ConfirmedEnrollments);

public sealed record ClassEnrollmentRequest(long SubscriptionId);

public sealed record CancelClassEnrollmentRequest(string? Reason);
