using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SportsCenterManagement.BLL.Services;

/// <summary>
/// Lightweight language guess for a member's chat message, so the reply language is an explicit
/// instruction instead of something the model has to infer against a Vietnamese prompt and history.
/// Returns null when the text gives no clear signal.
/// </summary>
public static class AiLanguageDetector
{
    private static readonly (Regex Script, string Language)[] Scripts =
    [
        (new(@"[぀-ヿ]"), "Japanese"), // Kana first: Japanese also uses Han characters.
        (new(@"[가-힯ᄀ-ᇿ㄰-㆏]"), "Korean"),
        (new(@"[一-鿿]"), "Chinese"),
        (new(@"[฀-๿]"), "Thai"),
        (new(@"[຀-໿]"), "Lao"),
        (new(@"[ក-៿]"), "Khmer"),
        (new(@"[Ѐ-ӿ]"), "Russian"),
        (new(@"[؀-ۿ]"), "Arabic"),
        (new(@"[ऀ-ॿ]"), "Hindi"),
    ];

    private static readonly Regex VietnameseLetters = new(
        "[ăâđêôơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]", RegexOptions.IgnoreCase);
    private static readonly Regex Words = new(@"[\p{L}']+");

    private static readonly Dictionary<string, string> RequestedLanguageAliases = new(StringComparer.Ordinal)
    {
        ["tieng viet"] = "Vietnamese",
        ["vietnamese"] = "Vietnamese",
        ["tieng anh"] = "English",
        ["english"] = "English",
        ["tieng thai"] = "Thai",
        ["tieng thai lan"] = "Thai",
        ["thai"] = "Thai",
        ["tieng nhat"] = "Japanese",
        ["tieng nhat ban"] = "Japanese",
        ["japanese"] = "Japanese",
        ["tieng han"] = "Korean",
        ["tieng han quoc"] = "Korean",
        ["korean"] = "Korean",
        ["tieng trung"] = "Chinese",
        ["tieng trung quoc"] = "Chinese",
        ["tieng hoa"] = "Chinese",
        ["chinese"] = "Chinese",
        ["mandarin"] = "Chinese",
        ["tieng phap"] = "French",
        ["french"] = "French",
        ["tieng tay ban nha"] = "Spanish",
        ["spanish"] = "Spanish",
        ["tieng duc"] = "German",
        ["german"] = "German",
        ["tieng nga"] = "Russian",
        ["russian"] = "Russian",
        ["tieng a rap"] = "Arabic",
        ["arabic"] = "Arabic",
        ["tieng lao"] = "Lao",
        ["lao"] = "Lao",
        ["tieng khmer"] = "Khmer",
        ["tieng campuchia"] = "Khmer",
        ["khmer"] = "Khmer",
        ["tieng y"] = "Italian",
        ["tieng italia"] = "Italian",
        ["italian"] = "Italian",
        ["tieng bo dao nha"] = "Portuguese",
        ["portuguese"] = "Portuguese",
        ["tieng indonesia"] = "Indonesian",
        ["indonesian"] = "Indonesian",
        ["tieng hindi"] = "Hindi",
        ["tieng an do"] = "Hindi",
        ["hindi"] = "Hindi",
    };

    private static readonly Regex VietnameseRequestPattern = new(
        @"(?:^|\b)(?:hay|vui long|xin|muon|co the|thu|noi|tra loi|phan hoi|dich|chuyen|doi|viet|giai thich|tu van|dung|su dung|bang|sang)\b(?:\s+\w+){0,4}?\s+(tieng\s+[a-z]+(?:\s+[a-z]+){0,2})\b|^\s*(tieng\s+[a-z]+(?:\s+[a-z]+){0,2})(?:\s+(?:di|nhe|nha|a|giup|voi|duoc khong|dc khong))?\s*[?.!]*$",
        RegexOptions.CultureInvariant);

    private static readonly Regex TopicNounBeforeVietnameseLanguage = new(
        @"\b(?:lop|khoa|mon|hoc|day|giao vien|huan luyen vien|chung chi|bai)\s+tieng\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex EnglishRequestPattern = new(
        @"(?:^|\b)(?:speak|talk|reply|answer|respond|translate|switch|change|write|explain|chat|use)\s+(?:to\s+|with\s+me\s+)?(?:in\s+|to\s+|into\s+)?(thai|english|vietnamese|japanese|korean|chinese|mandarin|french|spanish|german|russian|arabic|lao|khmer|italian|portuguese|indonesian|hindi)\b|\b(?:in|into)\s+(thai|english|vietnamese|japanese|korean|chinese|mandarin|french|spanish|german|russian|arabic|lao|khmer|italian|portuguese|indonesian|hindi)\s*(?:please|plz)?\s*[?.!]*$|\b(thai|english|vietnamese|japanese|korean|chinese|mandarin|french|spanish|german|russian|arabic|lao|khmer|italian|portuguese|indonesian|hindi)\s+(?:please|plz|only)\b",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> English = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "is", "are", "am", "was", "do", "does", "did", "can", "could", "would", "will", "you", "your",
        "i", "me", "my", "we", "our", "what", "when", "where", "which", "who", "why", "how", "much", "many", "please", "help",
        "want", "need", "book", "slot", "class", "classes", "schedule", "price", "prices", "package", "packages",
        "open", "opening", "hours", "time", "today", "tomorrow", "tomorow", "tonight", "week", "next", "this", "there",
        "any", "have", "has", "in", "on", "at", "for", "to", "of", "with", "and", "or", "about", "hello", "hi", "thanks",
        "thank", "court", "pool", "swimming", "gym", "coach", "trainer", "membership", "speak", "reply", "answer",
        "translate", "tell", "explain", "show", "give", "language", "available", "monday", "tuesday", "wednesday",
        "thursday", "friday", "saturday", "sunday", "yes", "no", "good", "morning", "afternoon", "evening"
    };

    private static readonly HashSet<string> VietnameseUnaccented = new(StringComparer.Ordinal)
    {
        "toi", "ban", "lop", "hom", "nay", "mai", "gio", "co", "khong", "ko", "gi", "nao", "may", "cho", "la", "di",
        "tap", "gia", "goi", "lich", "ngay", "tuan", "thang", "san", "phong", "trung", "tam", "mo", "cua", "dang", "ky",
        "duoc", "minh", "em", "anh", "chi", "oi", "a", "nhe", "vay", "sao", "the", "nhieu", "bao", "luc", "tieng", "noi"
    };

    private static readonly HashSet<string> French = new(StringComparer.Ordinal)
        { "bonjour", "merci", "je", "vous", "est", "cours", "quel", "quelle", "demain", "aujourd'hui", "prix", "horaire", "salut", "les", "des", "parler", "français", "francais" };
    private static readonly HashSet<string> Spanish = new(StringComparer.Ordinal)
        { "hola", "gracias", "clase", "clases", "cuanto", "cuánto", "donde", "dónde", "quiero", "mañana", "hoy", "precio", "horario", "por", "favor", "español", "espanol" };
    private static readonly HashSet<string> German = new(StringComparer.Ordinal)
        { "hallo", "danke", "bitte", "wann", "preis", "preise", "kurse", "öffnungszeiten", "heute", "morgen", "sprechen", "deutsch", "ich", "wie" };

    /// <summary>
    /// Detects an explicit request to reply or translate into a target language
    /// (for example "hãy nói tiếng thái", "dịch sang tiếng Nhật", "reply in Thai").
    /// Returns null when the message is not explicitly asking to switch languages.
    /// </summary>
    public static string? DetectRequestedLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = StripMarks(text.Trim().ToLowerInvariant());
        // Keep space separation clean for regex word boundaries.
        normalized = Regex.Replace(normalized, @"\s+", " ");

        if (!TopicNounBeforeVietnameseLanguage.IsMatch(normalized))
        {
            foreach (Match match in VietnameseRequestPattern.Matches(normalized))
            {
                var phrase = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                if (TryResolveAlias(phrase, out var language)) return language;
            }
        }

        var enMatch = EnglishRequestPattern.Match(normalized);
        if (enMatch.Success)
        {
            var token = enMatch.Groups[1].Success ? enMatch.Groups[1].Value
                : enMatch.Groups[2].Success ? enMatch.Groups[2].Value
                : enMatch.Groups[3].Value;
            if (RequestedLanguageAliases.TryGetValue(token, out var language)) return language;
        }

        return null;
    }

    private static bool TryResolveAlias(string phrase, out string language)
    {
        // Try longest suffix/prefix match so "tieng tay ban nha nhe" still resolves to "tieng tay ban nha".
        var parts = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var len = parts.Length; len >= 2; len--)
        {
            var candidate = string.Join(' ', parts.Take(len));
            if (RequestedLanguageAliases.TryGetValue(candidate, out var matched))
            {
                language = matched;
                return true;
            }
        }
        language = string.Empty;
        return false;
    }

    public static string? Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DetectRequestedLanguage(text) is { } requested) return requested;

        foreach (var (script, language) in Scripts)
            if (script.IsMatch(text)) return language;
        if (VietnameseLetters.IsMatch(text)) return "Vietnamese";

        var words = Words.Matches(text.ToLowerInvariant()).Select(match => match.Value).ToList();
        if (words.Count == 0) return null;
        var unaccented = words.Select(StripMarks).ToList();
        var scores = new (string Language, int Score)[]
        {
            ("English", words.Count(English.Contains)),
            ("Vietnamese", unaccented.Count(VietnameseUnaccented.Contains)),
            ("French", words.Count(French.Contains)),
            ("Spanish", words.Count(Spanish.Contains)),
            ("German", words.Count(German.Contains)),
        }.OrderByDescending(item => item.Score).ToArray();
        // Require a clear winner; a tie (e.g. "a", "the" exist in both lists) means "let the model decide".
        return scores[0].Score > 0 && scores[0].Score > scores[1].Score ? scores[0].Language : null;
    }

    private static string StripMarks(string word) => string.Concat(word.Normalize(NormalizationForm.FormD)
        .Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark))
        .Replace('đ', 'd');
}
