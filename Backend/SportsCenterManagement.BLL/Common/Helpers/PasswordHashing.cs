using System.Security.Cryptography;

namespace SportsCenterManagement.BLL.Common.Helpers;

public static class PasswordHashing
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public static bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        if (encodedHash.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, encodedHash);
            }
            catch
            {
                return false;
            }
        }

        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256"
            || !int.TryParse(parts[1], out var iterations)
            || iterations is < 100_000 or > 1_000_000)
        {
            _ = Rfc2898DeriveBytes.Pbkdf2(password, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != SaltSize || expected.Length != HashSize)
            {
                _ = Rfc2898DeriveBytes.Pbkdf2(password, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
                return false;
            }
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            _ = Rfc2898DeriveBytes.Pbkdf2(password, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
            return false;
        }
    }
}
