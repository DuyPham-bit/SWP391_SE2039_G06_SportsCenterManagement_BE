using Microsoft.Extensions.Configuration;
using SportsCenterManagement.BLL.Services.Interfaces;

namespace SportsCenterManagement.BLL.Services;

public sealed class BusinessClock : IBusinessClock
{
    private readonly TimeZoneInfo _timeZone;

    public BusinessClock(IConfiguration configuration)
    {
        var tzId = configuration["Reports:TimeZoneId"] ?? configuration["Business:TimeZoneId"] ?? "SE Asia Standard Time";
        try
        {
            _timeZone = TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux / IANA fallback
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
            }
            catch
            {
                _timeZone = TimeZoneInfo.CreateCustomTimeZone("GMT+07", TimeSpan.FromHours(7), "GMT+07", "GMT+07");
            }
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime BusinessNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);

    public DateOnly Today => DateOnly.FromDateTime(BusinessNow);

    public TimeZoneInfo TimeZone => _timeZone;

    public DateTime ToBusinessTime(DateTime utcDateTime) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), _timeZone);

    public DateTime ToUtcTime(DateTime businessDateTime) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(businessDateTime, DateTimeKind.Unspecified), _timeZone);

    public DateOnly ToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(ToBusinessTime(utcDateTime));

    public DateTime StartOfBusinessDayUtc(DateOnly businessDate) =>
        ToUtcTime(businessDate.ToDateTime(TimeOnly.MinValue));

    public DateTime EndOfBusinessDayUtc(DateOnly businessDate) =>
        ToUtcTime(businessDate.ToDateTime(TimeOnly.MaxValue));
}

