using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SportsCenterManagement.DAL.Context;

namespace SportsCenterManagement.BLL.Services;

/// <summary>Builds an allowlist of public facts. Never serializes database entities or private member data.</summary>
public sealed class MemberAiContextService(SportsCenterDbContext db, TimeProvider timeProvider)
{
    private const int MaxPackages = 50;
    private const int MaxFacilities = 100;
    private const int MaxSessions = 200;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<string> BuildAsync(long userId, IReadOnlyList<AiTurn> history, CancellationToken ct)
    {
        var localNow = timeProvider.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var dates = AiScheduleDates.Resolve(history, today);

        // Scope comes from the authenticated account, never from the question or client-supplied IDs.
        var memberCenterId = await db.MemberProfiles.AsNoTracking().Where(member => member.UserId == userId)
            .Select(member => member.CenterId).SingleOrDefaultAsync(ct);
        var center = await db.Centers.AsNoTracking()
            .Where(item => item.Id == memberCenterId && item.Status == "Active")
            .Select(item => new { item.Name, item.Address, item.Phone, item.Email, item.Description,
                item.OpeningTime, item.ClosingTime }).SingleOrDefaultAsync(ct);
        long? visibleCenterId = center is null ? null : memberCenterId;

        var packages = await db.MembershipPackages.AsNoTracking()
            .Where(item => item.CenterId == visibleCenterId && item.Status == "Active")
            .OrderBy(item => item.Name).ThenBy(item => item.Id).Take(MaxPackages + 1)
            .Select(item => new { item.Name, item.Description, item.Price, item.DurationDays,
                item.MaxClasses, item.AccessType, item.AllowedSports }).ToListAsync(ct);
        var facilities = await db.Rooms.AsNoTracking()
            .Where(item => item.CenterId == visibleCenterId && item.Status == "Active")
            .OrderBy(item => item.Name).ThenBy(item => item.Id).Take(MaxFacilities + 1)
            .Select(item => new { item.Name, Type = item.RoomType, item.Capacity, Location = item.LocationDescription })
            .ToListAsync(ct);
        var sessions = await (
            from session in db.ClassSessions.AsNoTracking()
            join cls in db.Classes on session.ClassId equals cls.Id
            join sport in db.Sports on cls.SportId equals sport.Id
            where !dates.NeedsClarification && cls.CenterId == visibleCenterId
                && cls.Status == FlowStatuses.ClassPublished && sport.Status == "Active"
                && (sport.CenterId == null || sport.CenterId == visibleCenterId)
                && session.SessionStatus == FlowStatuses.SessionScheduled
                && session.SessionDate >= dates.From && session.SessionDate <= dates.To
            orderby session.SessionDate, session.StartTime, session.Id
            select new
            {
                ClassName = cls.Name, SportName = sport.Name, cls.Level,
                session.SessionDate, session.StartTime, session.EndTime,
                RoomName = db.Rooms.Where(room => room.Id == (session.RoomId ?? cls.RoomId)
                        && room.CenterId == visibleCenterId && room.Status == "Active")
                    .Select(room => room.Name).FirstOrDefault()
            }).Take(MaxSessions + 1).ToListAsync(ct);

        return JsonSerializer.Serialize(new
        {
            Source = "SCMS public catalog database",
            GeneratedAt = localNow,
            TimeZone = "Asia/Ho_Chi_Minh",
            Today = today,
            Availability = center is null ? "center_unavailable" : "available",
            Center = center,
            Currency = "VND",
            Packages = packages.Take(MaxPackages),
            PackagesTruncated = packages.Count > MaxPackages,
            Facilities = facilities.Take(MaxFacilities),
            FacilitiesTruncated = facilities.Count > MaxFacilities,
            Schedule = new
            {
                dates.From, dates.To, dates.NeedsClarification, dates.Note,
                Items = sessions.Take(MaxSessions),
                IsTruncated = sessions.Count > MaxSessions
            },
            // There is currently no published-events catalog. A class session is not a special event.
            SpecialEventsAvailable = false
        }, JsonOptions);
    }
}

internal sealed record AiScheduleWindow(DateOnly From, DateOnly To, bool NeedsClarification, string Note);

internal static class AiScheduleDates
{
    private const int MaxWindowDays = 31;
    private static readonly Regex ExplicitDates = new(
        @"(?<![\d/\-])(?:(?<iy>\d{4})-(?<im>\d{1,2})-(?<id>\d{1,2})|(?<d>\d{1,2})\s*[/\-]\s*(?<m>\d{1,2})(?:\s*[/\-]\s*(?<y>\d{4}|\d{2}))?|(?:ngay\s+)?(?<vd>\d{1,2})\s+thang\s+(?<vm>\d{1,2})(?:\s+nam\s+(?<vy>\d{4}))?)(?![\d/\-])",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Weekday = new(@"\b(?:thu\s*(?<day>hai|ba|tu|nam|sau|bay|[2-7])|(?<sunday>chu nhat))\b",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static AiScheduleWindow Resolve(IReadOnlyList<AiTurn> history, DateOnly today)
    {
        var messages = history.Where(turn => turn.Role == "user").ToList();
        var question = Normalize(messages.LastOrDefault()?.Text ?? "");
        var dates = Parse(question, today);
        if (dates is not null) return dates;

        if (Regex.IsMatch(question, @"\b(ngay ay|ngay do|hom do|hom ay)\b", RegexOptions.CultureInvariant))
        {
            foreach (var previous in messages.Take(messages.Count - 1).Reverse())
            {
                var referenceDate = previous.CreatedAt is { } createdAt
                    ? DateOnly.FromDateTime(DateTime.SpecifyKind(createdAt, DateTimeKind.Utc).AddHours(7)) : today;
                dates = Parse(Normalize(previous.Text), referenceDate);
                if (dates is not null) return dates;
            }
            return Clarify(today, "Chưa xác định ngày được nhắc đến. Hãy hỏi lại ngày cụ thể.");
        }

        return new(today, today.AddDays(13), false, "Chưa chỉ định ngày: cung cấp lịch công khai trong 14 ngày, cần nêu rõ khoảng ngày khi trả lời.");
    }

    private static AiScheduleWindow? Parse(string text, DateOnly today)
    {
        var matches = ExplicitDates.Matches(text);
        if (matches.Count > 0)
        {
            if (matches.Count > 2) return Clarify(today, "Vui lòng hỏi một ngày hoặc một khoảng ngày tối đa 31 ngày.");
            var parsed = new List<DateOnly>();
            foreach (Match match in matches)
            {
                string Value(params string[] names) => names.Select(name => match.Groups[name].Value).FirstOrDefault(value => value.Length > 0) ?? "";
                var yearText = Value("iy", "y", "vy");
                var year = yearText.Length == 0 ? today.Year : int.Parse(yearText, CultureInfo.InvariantCulture);
                if (yearText.Length == 2) year += 2000;
                var month = int.Parse(Value("im", "m", "vm"), CultureInfo.InvariantCulture);
                var day = int.Parse(Value("id", "d", "vd"), CultureInfo.InvariantCulture);
                if (!DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date)) return Clarify(today, "Ngày được hỏi không hợp lệ. Hãy hỏi lại ngày/tháng/năm chính xác.");
                parsed.Add(date);
            }
            var from = parsed[0];
            var to = parsed[^1];
            if (to < from || to.DayNumber - from.DayNumber >= MaxWindowDays)
                return Clarify(today, "Khoảng ngày phải tăng dần và dài tối đa 31 ngày. Hãy thu hẹp khoảng ngày.");
            return new(from, to, false, $"Ngày không ghi năm được hiểu là năm {today.Year}; hãy nêu đầy đủ ngày/tháng/năm khi trả lời.");
        }

        int? dayOffset = Regex.IsMatch(text, @"\b(ngay kia|ngay mot)\b") ? 2
            : Regex.IsMatch(text, @"\b(ngay mai|mai|tomorrow)\b") ? 1
            : Regex.IsMatch(text, @"\b(hom nay|today)\b") ? 0
            : Regex.IsMatch(text, @"\b(hom qua|yesterday)\b") ? -1
            : Regex.IsMatch(text, @"\b(hom kia)\b") ? -2 : null;
        if (dayOffset.HasValue)
        {
            var date = today.AddDays(dayOffset.Value);
            return new(date, date, false, "Ngày tương đối được tính theo giờ Việt Nam.");
        }

        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var nextWeek = text.Contains("tuan sau", StringComparison.Ordinal);
        var weekday = Weekday.Match(text);
        if (weekday.Success)
        {
            var day = weekday.Groups["sunday"].Success ? 6 : weekday.Groups["day"].Value switch
            {
                "hai" or "2" => 0, "ba" or "3" => 1, "tu" or "4" => 2,
                "nam" or "5" => 3, "sau" or "6" => 4, _ => 5
            };
            var date = monday.AddDays((nextWeek ? 7 : 0) + day);
            if (!nextWeek && !text.Contains("tuan nay", StringComparison.Ordinal) && date < today) date = date.AddDays(7);
            return new(date, date, false, "Thứ trong tuần được tính theo lịch và giờ Việt Nam.");
        }
        if (nextWeek || text.Contains("tuan nay", StringComparison.Ordinal))
        {
            var from = monday.AddDays(nextWeek ? 7 : 0);
            return new(from, from.AddDays(6), false, "Tuần bắt đầu từ thứ Hai, theo giờ Việt Nam.");
        }
        return null;
    }

    private static AiScheduleWindow Clarify(DateOnly today, string note) => new(today, today, true, note);

    private static string Normalize(string text) => string.Concat(text.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Replace('đ', 'd').Normalize(NormalizationForm.FormC);
}
