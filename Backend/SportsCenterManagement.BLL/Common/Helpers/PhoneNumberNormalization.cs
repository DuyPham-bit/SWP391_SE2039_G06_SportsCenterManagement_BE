namespace SportsCenterManagement.BLL.Common.Helpers;

public static class PhoneNumberNormalization
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return new string(value.Trim()
            .Where(character => !char.IsWhiteSpace(character)
                && character is not ('-' or '(' or ')' or '.'))
            .ToArray());
    }
}
