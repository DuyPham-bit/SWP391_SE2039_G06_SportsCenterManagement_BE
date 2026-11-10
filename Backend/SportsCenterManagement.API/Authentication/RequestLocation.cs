using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SportsCenterManagement.API.Authentication;

/// <summary>
/// Human-readable origin of a security request ("Hanoi, Vietnam"), shown in OTP emails like Vercel does.
/// Uses Cloudflare visitor-location headers (CF-IPCity / CF-IPCountry) only when the API is configured to
/// trust them, because a direct caller could otherwise inject text into an email sent to someone else.
/// </summary>
public static partial class RequestLocation
{
    public const string Default = "Ho Chi Minh City, Vietnam";
    public const string TrustSetting = "ExternalCommunications:Email:TrustCloudflareLocationHeaders";

    public static string Describe(HttpRequest request, IConfiguration configuration)
    {
        if (!configuration.GetValue(TrustSetting, false)) return Default;
        var city = CleanCity(request.Headers["CF-IPCity"].ToString());
        var country = CountryName(request.Headers["CF-IPCountry"].ToString());
        if (country is null) return Default;
        return city is null ? country : $"{city}, {country}";
    }

    private static string? CleanCity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = RepairUtf8(value.Trim());
        // Letters, marks, spaces and . ' - only: no digits, URLs or markup can reach the email.
        return SafeCity().IsMatch(value) ? value : null;
    }

    private static string? CountryName(string value)
    {
        var code = value.Trim().ToUpperInvariant();
        // XX = unknown, T1 = Tor (Cloudflare conventions).
        if (code.Length != 2 || code is "XX" or "T1" || !code.All(char.IsAsciiLetterUpper)) return null;
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Servers may decode raw UTF-8 header bytes as Latin-1 ("HÃ  Ná»™i"); recover the original text.
    private static string RepairUtf8(string value)
    {
        if (!value.Any(c => c is >= '\u0080' and <= 'ÿ') || value.Any(c => c > 'ÿ')) return value;
        try
        {
            var bytes = Encoding.Latin1.GetBytes(value);
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return value;
        }
    }

    [GeneratedRegex(@"^[\p{L}\p{M}][\p{L}\p{M} .'\-]{0,59}$")]
    private static partial Regex SafeCity();
}
