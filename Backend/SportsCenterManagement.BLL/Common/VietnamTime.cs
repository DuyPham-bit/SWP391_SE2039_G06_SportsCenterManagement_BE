namespace SportsCenterManagement.BLL.Common;

public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public static DateOnly GetDate(DateTime utcTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcTime, Zone));

    public static DateTime ToUtc(DateOnly date) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(TimeOnly.MinValue), Zone);
}
