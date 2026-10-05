namespace SportsCenterManagement.BLL.Services;

/// <summary>Shared time helpers. Class schedules are stored in center-local time (Vietnam, UTC+7).</summary>
internal static class ScheduleTime
{
    public static DateTime VietnamNow => DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;

    public static DateOnly VietnamToday => DateOnly.FromDateTime(VietnamNow);

    public static bool Overlaps(TimeOnly startA, TimeOnly endA, TimeOnly startB, TimeOnly endB) =>
        startA < endB && endA > startB;

    public static bool DateRangesOverlap(DateOnly? start1, DateOnly? end1, DateOnly? start2, DateOnly? end2)
    {
        var s1 = start1 ?? DateOnly.MinValue;
        var e1 = end1 ?? DateOnly.MaxValue;
        var s2 = start2 ?? DateOnly.MinValue;
        var e2 = end2 ?? DateOnly.MaxValue;
        return s1 <= e2 && s2 <= e1;
    }
}

internal static class FlowStatuses
{
    public const string ClassPublished = "Published";
    public const string ClassCancelled = "Cancelled";
    public const string ClassCompleted = "Completed";

    public const string ScheduleActive = "Active";
    public const string ScheduleCancelled = "Cancelled";

    public const string SessionScheduled = "Scheduled";
    public const string SessionCancelled = "Cancelled";

    public const string BookingBooked = "Booked";
    public const string BookingCancelled = "Cancelled";
    public const string BookingCancelledLate = "CANCELLED_LATE_CHARGED";

    public const string WaitlistWaiting = "Waiting";
    public const string WaitlistPromoted = "Promoted";
    public const string WaitlistCancelled = "Cancelled";

    public const string EnrollmentConfirmed = "Confirmed";
    public const string EnrollmentCancelled = "Cancelled";

    public static bool IsClosed(string status) =>
        string.Equals(status, ClassCancelled, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, ClassCompleted, StringComparison.OrdinalIgnoreCase);
}
