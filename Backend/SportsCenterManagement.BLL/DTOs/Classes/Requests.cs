using System.ComponentModel.DataAnnotations;

namespace SportsCenterManagement.BLL.DTOs.Classes
{
    public sealed record AssignCoachRequest
    {
        [Required]
        public required long CoachId { get; init; }

        public bool IsPrimary { get; init; } = true;

        /// <summary>
        /// When true, former primary assignments are removed instead of being retained as assistants.
        /// Existing API consumers keep the multi-coach behavior by default.
        /// </summary>
        public bool ReplacePrimary { get; init; }
    }

    public sealed record CreateClassRequest(
        long CenterId,
        long SportId,
        long? RoomId,
        string Name,
        string? Description,
        string? Level,
        int Capacity,
        int DurationMinutes,
        bool AllowWaitlist = true);

    public sealed record CreateClassScheduleRequest(
        long? RoomId,
        int DayOfWeek,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly StartDate,
        DateOnly EndDate);

    public sealed record UpdateClassRequest(
        long SportId,
        long? RoomId,
        string Name,
        string? Description,
        string? Level,
        int Capacity,
        int DurationMinutes,
        bool AllowWaitlist = true);

    public sealed record UpdateClassScheduleRequest(
        long? RoomId,
        int DayOfWeek,
        TimeOnly StartTime,
        TimeOnly EndTime,
        DateOnly StartDate,
        DateOnly EndDate);

    public sealed record ClassEnrollmentRequest(long SubscriptionId);

    public sealed record CancelClassEnrollmentRequest(string? Reason);

    public sealed record SaveSportRequest(
        string Name,
        string? Description,
        string Status = "Active");

    public sealed record SessionScheduleQuery(
        long? SportId,
        long? CoachId,
        DateOnly? From,
        DateOnly? To,
        int Page = 1,
        int PageSize = 20,
        long? CenterId = null);
}
