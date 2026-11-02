namespace SportsCenterManagement.BLL.Services.Interfaces;

public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateTime BusinessNow { get; }
    DateOnly Today { get; }
    TimeZoneInfo TimeZone { get; }
    DateTime ToBusinessTime(DateTime utcDateTime);
    DateTime ToUtcTime(DateTime businessDateTime);
    DateOnly ToBusinessDate(DateTime utcDateTime);
    DateTime StartOfBusinessDayUtc(DateOnly businessDate);
    DateTime EndOfBusinessDayUtc(DateOnly businessDate);
}

