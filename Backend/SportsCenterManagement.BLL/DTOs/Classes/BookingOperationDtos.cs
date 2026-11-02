namespace SportsCenterManagement.BLL.DTOs.Classes;

public sealed record MemberWaitlistItemDto(
    long Id,
    long? SessionId,
    string ClassName,
    DateOnly SessionDate,
    TimeOnly StartTime,
    TimeOnly EndTime,
    string Status,
    DateTime JoinedAt);

public sealed record SessionRosterItemDto(
    long? BookingId,
    long MemberId,
    string MemberName,
    string? MemberCode,
    string Email,
    string? Phone,
    string? FitnessGoal,
    string? FitnessLevel,
    string? AttendanceStatus,
    string? Note);

public sealed record AttendanceRecordDto(
    long MemberId,
    string Status,
    string? Note = null);
