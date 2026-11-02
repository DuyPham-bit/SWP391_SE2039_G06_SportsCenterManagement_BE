namespace SportsCenterManagement.BLL.Common;

public static class VietnamTime
{
    public static DateOnly Today => DateAt(DateTimeOffset.UtcNow);

    public static DateOnly DateAt(DateTimeOffset instant) =>
        DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(7)).DateTime);
}
