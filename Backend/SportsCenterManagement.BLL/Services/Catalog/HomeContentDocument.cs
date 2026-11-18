using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SportsCenterManagement.BLL.Services.Catalog;

/// <summary>
/// Pure JSON rules for the home-content document. Admin-written text is stored as <c>{"vi": "...", "en": "..."}</c>;
/// URLs, links, icons, ids, numbers and flags are stored once and shared by both languages.
/// </summary>
public static partial class HomeContentDocument
{
    public const string DefaultLanguage = "vi";
    public const int SchemaVersion = 2;
    public static readonly string[] Languages = ["vi", "en"];
    public static readonly string[] RequiredSections = ["heroBanner", "whyUs", "coursesSection", "facilitiesSection", "zonesLayout", "ctaAndFooter"];
    public static readonly string[] Sections = [.. RequiredSections, "authShowcase"];

    private static readonly HashSet<string> ObjectArrays = ["metrics", "features", "stats", "sportsList", "facilitiesList", "zones", "serviceAmenities", "specs", "trustBadges"];
    private static readonly HashSet<string> TextArrays = ["tags", "badges", "amenities", "items"];
    private static readonly HashSet<string> Booleans = ["isVisible", "isAutoCount", "formatThousands"];
    private static readonly HashSet<string> SharedKeys =
        ["id", "icon", "image", "secondaryImage", "facilityId", "catalogSource", "contactPhone", "contactEmail",
         "contactZaloPhone", "badgeColor", "color", "suffix", "rating", "ratingCount"];
    // Must have a Vietnamese value before the content can be published.
    private static readonly HashSet<string> RequiredText =
        ["heroBanner.badge", "heroBanner.title", "heroBanner.description", "heroBanner.primaryCtaText",
         "whyUs.title", "coursesSection.title", "facilitiesSection.title", "zonesLayout.title"];
    private const int MaxHeroMetrics = 12;
    private const int ShortTextMax = 300;
    private const int LongTextMax = 10000;

    /// <summary>Strings at these keys are shared across languages and never become {vi,en} objects.</summary>
    public static bool IsSharedKey(string key) =>
        SharedKeys.Contains(key) || Booleans.Contains(key) || key is "number" or "schemaVersion" or "value"
        || key.EndsWith("Url", StringComparison.Ordinal) || key.EndsWith("Link", StringComparison.Ordinal)
        || key.EndsWith("Icon", StringComparison.Ordinal) || key.EndsWith("Value", StringComparison.Ordinal);

    public static bool IsLocalized(JsonNode? node) =>
        node is JsonObject obj && obj.Count > 0
        && obj.All(p => Languages.Contains(p.Key) && p.Value is JsonValue v && v.TryGetValue<string>(out _));

    public static string Text(JsonNode? localized, string lang) =>
        localized?[lang]?.GetValue<string>() ?? string.Empty;

    /// <summary>
    /// Converts plain strings at translatable keys into {vi,en}. The English side is looked up in
    /// <paramref name="translations"/> (Vietnamese text → localized object), so legacy single-language
    /// content and the old flat API keep any English already known.
    /// </summary>
    public static void Normalize(JsonNode node, IReadOnlyDictionary<string, JsonObject> translations)
    {
        if (node is not JsonObject root) return;
        foreach (var property in root.ToArray())
            if (property.Value is not null && property.Key != "schemaVersion")
                Replace(root, property.Key, property.Value, NormalizeNode(property.Value, property.Key, translations));
    }

    private static JsonNode? NormalizeNode(JsonNode node, string key, IReadOnlyDictionary<string, JsonObject> translations)
    {
        if (IsLocalized(node))
        {
            var obj = (JsonObject)node;
            foreach (var lang in Languages) obj[lang] ??= string.Empty;
            return obj;
        }
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
        {
            if (IsSharedKey(key)) return node;
            return translations.TryGetValue(text, out var known)
                ? known.DeepClone()
                : new JsonObject { ["vi"] = text, ["en"] = string.Empty };
        }
        if (node is JsonObject child)
        {
            foreach (var property in child.ToArray())
                if (property.Value is not null)
                    Replace(child, property.Key, property.Value, NormalizeNode(property.Value, property.Key, translations));
            return child;
        }
        if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                // Array items inherit the array's key, so text arrays (tags, items…) become localized too.
                if (array[i] is not { } item) continue;
                var normalized = NormalizeNode(item, key, translations);
                if (!ReferenceEquals(normalized, item)) array[i] = normalized;
            }
            return array;
        }
        return node;
    }

    // A node can only have one parent: re-assigning the same instance would throw.
    private static void Replace(JsonObject parent, string key, JsonNode current, JsonNode? normalized)
    {
        if (!ReferenceEquals(current, normalized)) parent[key] = normalized;
    }

    /// <summary>Vietnamese text → its localized object, for every localized value in <paramref name="node"/>.</summary>
    public static void CollectTranslations(JsonNode? node, IDictionary<string, JsonObject> into)
    {
        if (IsLocalized(node))
        {
            var vi = Text(node, "vi");
            if (vi.Length > 0 && Text(node, "en").Length > 0) into[vi] = (JsonObject)node!.DeepClone();
            return;
        }
        if (node is JsonObject obj) foreach (var p in obj) CollectTranslations(p.Value, into);
        else if (node is JsonArray arr) foreach (var item in arr) CollectTranslations(item, into);
    }

    /// <summary>Replaces each {vi,en} with the requested language, falling back to Vietnamese when it is empty.</summary>
    public static JsonNode Localize(JsonNode node, string lang)
    {
        if (IsLocalized(node))
        {
            var text = Text(node, lang);
            return JsonValue.Create(text.Length > 0 ? text : Text(node, DefaultLanguage))!;
        }
        if (node is JsonObject obj)
        {
            var result = new JsonObject();
            foreach (var p in obj) result[p.Key] = p.Value is null ? null : Localize(p.Value, lang);
            return result;
        }
        if (node is JsonArray arr)
        {
            var result = new JsonArray();
            foreach (var item in arr) result.Add(item is null ? null : Localize(item, lang));
            return result;
        }
        return node.DeepClone();
    }

    public static string NormalizeLanguage(string? lang) =>
        lang?.Trim().ToLowerInvariant() is "en" ? "en" : DefaultLanguage;

    /// <summary>Throws 400 on invalid content; returns the paths whose English text is still missing.</summary>
    public static IReadOnlyList<string> Validate(JsonNode? content)
    {
        if (content is not JsonObject root || Encoding.UTF8.GetByteCount(root.ToJsonString()) > 1024 * 1024)
            throw Invalid("Nội dung trang chủ phải là một đối tượng JSON không quá 1 MB.");
        foreach (var section in RequiredSections)
            if (root[section] is not JsonObject) throw Invalid($"Thiếu khu vực \"{section}\".");
        var warnings = new List<string>();
        foreach (var section in root)
        {
            if (section.Key == "schemaVersion") continue;
            if (!Sections.Contains(section.Key)) throw Invalid($"Khu vực \"{section.Key}\" không được hỗ trợ.");
            if (section.Value is not JsonObject sectionObject) throw Invalid($"Khu vực \"{section.Key}\" không hợp lệ.");
            ValidateObject(sectionObject, section.Key, 0, warnings);
        }
        foreach (var path in RequiredText)
        {
            var node = Find(root, path);
            if (!IsNonEmptyString(node) && !(IsLocalized(node) && Text(node, "vi").Trim().Length > 0))
                throw Invalid($"{path}: bản tiếng Việt không được để trống.");
        }
        ValidateHeroMetrics(root["heroBanner"]?["metrics"]);
        return warnings;
    }

    private static void ValidateHeroMetrics(JsonNode? metrics)
    {
        if (metrics is not JsonArray list) return;
        if (list.Count > MaxHeroMetrics) throw Invalid($"heroBanner.metrics: tối đa {MaxHeroMetrics} chỉ số nhanh.");
        for (var i = 0; i < list.Count; i++)
        {
            var metric = list[i];
            var value = metric?["value"];
            var hasValue = IsNonEmptyString(value) || (IsLocalized(value) && Text(value, "vi").Trim().Length > 0);
            var label = metric?["label"];
            var hasLabel = IsNonEmptyString(label) || (IsLocalized(label) && Text(label, "vi").Trim().Length > 0);
            if (!hasValue || !hasLabel)
                throw Invalid($"heroBanner.metrics[{i}]: cần nhập giá trị và nhãn tiếng Việt.");
        }
    }

    private static void ValidateObject(JsonObject value, string path, int depth, List<string> warnings)
    {
        if (depth > 8) throw Invalid($"{path}: cấu trúc lồng quá sâu.");
        foreach (var property in value)
        {
            var key = property.Key;
            var item = property.Value;
            var itemPath = $"{path}.{key}";
            if (ObjectArrays.Contains(key) || TextArrays.Contains(key))
            {
                if (item is not JsonArray array || array.Count > 200) throw Invalid($"{itemPath}: phải là danh sách tối đa 200 phần tử.");
                var ids = new HashSet<string>();
                for (var i = 0; i < array.Count; i++)
                {
                    var entry = array[i];
                    var entryPath = $"{itemPath}[{i}]";
                    if (TextArrays.Contains(key)) { ValidateText(entry, key, entryPath, warnings); continue; }
                    if (entry is not JsonObject entryObject) throw Invalid($"{entryPath}: phải là đối tượng.");
                    ValidateObject(entryObject, entryPath, depth + 1, warnings);
                    if (key is "sportsList" or "facilitiesList")
                    {
                        var id = entryObject["id"] is JsonValue idValue && idValue.TryGetValue<string>(out var s) ? s : null;
                        var name = entryObject["name"];
                        var hasName = IsNonEmptyString(name) || (IsLocalized(name) && Text(name, "vi").Trim().Length > 0);
                        if (string.IsNullOrWhiteSpace(id) || !ids.Add(id) || !hasName)
                            throw Invalid($"{entryPath}: thẻ cần mã (id) duy nhất và tên tiếng Việt.");
                    }
                }
            }
            else if (key == "trustBanner")
            {
                if (item is not JsonObject banner) throw Invalid($"{itemPath}: không hợp lệ.");
                ValidateObject(banner, itemPath, depth + 1, warnings);
            }
            else if (Booleans.Contains(key))
            {
                if (item is not JsonValue flag || !flag.TryGetValue<bool>(out _)) throw Invalid($"{itemPath}: phải là true/false.");
            }
            else if (key == "number")
            {
                if (item is not JsonValue number || !number.TryGetValue<double>(out var n) || !double.IsFinite(n) || n < 0 || n > 1_000_000_000)
                    throw Invalid($"{itemPath}: phải là số từ 0 đến 1.000.000.000.");
            }
            else ValidateText(item, key, itemPath, warnings);
        }
    }

    private static void ValidateText(JsonNode? value, string key, string path, List<string> warnings)
    {
        var max = IsShortTextKey(key) ? ShortTextMax : LongTextMax;
        if (IsLocalized(value))
        {
            var vi = Text(value, "vi");
            var en = Text(value, "en");
            if (vi.Length > max || en.Length > max) throw Invalid($"{path}: tối đa {max} ký tự.");
            if (vi.Trim().Length == 0 && en.Trim().Length > 0) throw Invalid($"{path}: bắt buộc có bản tiếng Việt.");
            if (vi.Trim().Length > 0 && en.Trim().Length == 0) warnings.Add(path);
            return;
        }
        if (value is not JsonValue plain || !plain.TryGetValue<string>(out var text))
            throw Invalid($"{path}: phải là văn bản.");
        if (text.Length > max) throw Invalid($"{path}: tối đa {max} ký tự.");
        if (key.EndsWith("Url", StringComparison.Ordinal) || key is "image" or "secondaryImage") ValidateUrl(text, path);
        else if (key.EndsWith("Link", StringComparison.Ordinal)) ValidateLink(text, path);
    }

    private static bool IsShortTextKey(string key) =>
        key is "badge" or "name" or "label" or "tagline" or "title" or "text"
        || key.EndsWith("Title", StringComparison.Ordinal) || key.EndsWith("Text", StringComparison.Ordinal)
        || key.EndsWith("Label", StringComparison.Ordinal) || key.EndsWith("Badge", StringComparison.Ordinal);

    private static void ValidateUrl(string value, string path)
    {
        var url = value.Trim();
        if (url.Length == 0) return;
        if (url.Any(char.IsControl) || url.Contains('\\') || url.StartsWith("//", StringComparison.Ordinal))
            throw Invalid($"{path}: URL không hợp lệ.");
        if (url.StartsWith('/') || url.StartsWith("./", StringComparison.Ordinal) || url.StartsWith("dashboard/", StringComparison.Ordinal)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw Invalid($"{path}: URL phải bắt đầu bằng http:// hoặc https://.");
    }

    /// <summary>Links are either in-app (#/route, #anchor, /path) or absolute http(s).</summary>
    private static void ValidateLink(string value, string path)
    {
        var link = value.Trim();
        if (link.Length == 0) return;
        if (link.StartsWith('#'))
        {
            if (!InternalLinkPattern().IsMatch(link)) throw Invalid($"{path}: liên kết nội bộ không hợp lệ (ví dụ: #/register hoặc #khoa-hoc).");
            return;
        }
        ValidateUrl(link, path);
    }

    [GeneratedRegex(@"^#(/[A-Za-z0-9\-_/]*|[A-Za-z0-9\-_]+)(\?[A-Za-z0-9\-_=&%.]*)?$")]
    private static partial Regex InternalLinkPattern();

    private static bool IsNonEmptyString(JsonNode? node) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && s.Trim().Length > 0;

    private static JsonNode? Find(JsonNode root, string path)
    {
        JsonNode? node = root;
        foreach (var part in path.Split('.')) node = node?[part];
        return node;
    }

    private static FlowException Invalid(string detail) => new(400, $"Nội dung trang chủ không hợp lệ: {detail}");
}
