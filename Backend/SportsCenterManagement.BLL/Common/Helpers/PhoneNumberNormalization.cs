namespace SportsCenterManagement.BLL.Common.Helpers;

public static class PhoneNumberNormalization
{
    /// <summary>
    /// Canonical stored form of a phone number, used for every write and every duplicate check.
    /// Separators are removed and Vietnamese numbers written with the country code
    /// (+84… or 84… with 11 digits) become the national 0-prefixed form, so "+84 912 345 678"
    /// and "0912345678" are the same identity. Foreign numbers keep their leading "+".
    /// </summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var phone = new string(value.Trim()
            .Where(character => !char.IsWhiteSpace(character)
                && character is not ('-' or '(' or ')' or '.'))
            .ToArray());
        if (phone.Length == 0) return null;

        if (phone.StartsWith("+84", StringComparison.Ordinal) && phone.Length > 3)
            return "0" + phone[3..];
        if (phone.StartsWith("84", StringComparison.Ordinal) && phone.Length == 11 && phone.All(char.IsAsciiDigit))
            return "0" + phone[2..];
        return phone;
    }

    /// <summary>
    /// Normalized phone for a free-text search term, or the term itself when it does not look like
    /// a phone number (so names, emails and member codes are searched unchanged).
    /// </summary>
    public static string SearchTerm(string term) =>
        term.Length > 0 && term.All(c => char.IsAsciiDigit(c) || c is '+' or ' ' or '-' or '.' or '(' or ')')
            ? Normalize(term) ?? term
            : term;
}
