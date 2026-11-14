using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SportsCenterManagement.API.Localization;

/// <summary>
/// Vietnamese → English lookup for API messages. Vietnamese stays the source text in the BLL;
/// <c>messages_en.json</c> holds exact translations plus templates for interpolated messages
/// (placeholders such as <c>{member.FullName}</c> are matched as wildcards and copied across).
/// </summary>
public static class ApiMessageLocalizer
{
    private sealed record Pattern(Regex Regex, string[] Names, string English);
    private sealed record Catalog(Dictionary<string, string> Exact, List<Pattern> Patterns);

    private static readonly Regex Placeholder = new(@"\{[^{}]*\}", RegexOptions.Compiled);
    private static readonly Lazy<Catalog> Data = new(Load);

    public static bool PrefersEnglish(HttpRequest request) =>
        request.Headers.AcceptLanguage.ToString().TrimStart().StartsWith("en", StringComparison.OrdinalIgnoreCase);

    public static bool TryTranslate(string message, out string english)
    {
        english = message;
        if (string.IsNullOrWhiteSpace(message)) return false;
        var catalog = Data.Value;
        if (catalog.Exact.TryGetValue(message, out var exact))
        {
            english = exact;
            return true;
        }
        foreach (var pattern in catalog.Patterns)
        {
            var match = pattern.Regex.Match(message);
            if (!match.Success) continue;
            english = Placeholder.Replace(pattern.English, token =>
            {
                var index = Array.IndexOf(pattern.Names, token.Value);
                return index >= 0 ? match.Groups[index + 1].Value : token.Value;
            });
            return true;
        }
        return false;
    }

    private static Catalog Load()
    {
        var assembly = typeof(ApiMessageLocalizer).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("messages_en.json", StringComparison.OrdinalIgnoreCase));
        if (resource is null) return new Catalog(new(StringComparer.Ordinal), []);

        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var document = JsonDocument.Parse(stream);
        var exact = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.GetProperty("exact").EnumerateObject())
            exact[entry.Name] = entry.Value.GetString() ?? entry.Name;

        var patterns = new List<Pattern>();
        foreach (var entry in document.RootElement.GetProperty("patterns").EnumerateArray())
        {
            var vi = entry.GetProperty("vi").GetString() ?? string.Empty;
            var en = entry.GetProperty("en").GetString() ?? string.Empty;
            var names = Placeholder.Matches(vi).Select(m => m.Value).ToArray();
            var body = string.Join("(.+?)", Placeholder.Split(vi).Select(Regex.Escape));
            patterns.Add(new Pattern(
                new Regex($"^{body}$", RegexOptions.Compiled | RegexOptions.Singleline, TimeSpan.FromMilliseconds(50)),
                names, en));
        }
        // Longer templates first so a specific message wins over a shorter, more generic one.
        patterns.Sort((a, b) => b.Regex.ToString().Length.CompareTo(a.Regex.ToString().Length));
        return new Catalog(exact, patterns);
    }
}
