using SportsCenterManagement.BLL.Common;

var cases = new (string Instant, DateOnly Expected)[]
{
    ("2026-12-31T16:59:59Z", new(2026, 12, 31)),
    ("2026-12-31T17:00:00Z", new(2027, 1, 1)),
    ("2027-01-01T00:00:00+07:00", new(2027, 1, 1)),
    ("2026-12-31T09:00:00-08:00", new(2027, 1, 1)),
    ("2027-01-01T07:00:00+14:00", new(2027, 1, 1)),
    ("2028-02-28T17:00:00Z", new(2028, 2, 29)),
    ("2028-02-29T17:00:00Z", new(2028, 3, 1))
};

foreach (var (instant, expected) in cases)
{
    var actual = VietnamTime.DateAt(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture));
    if (actual != expected)
        throw new InvalidOperationException($"{instant}: expected {expected:yyyy-MM-dd}, got {actual:yyyy-MM-dd}");
}

Console.WriteLine($"Passed {cases.Length} Vietnam business-date checks (midnight, year boundary, offsets, leap day).");
