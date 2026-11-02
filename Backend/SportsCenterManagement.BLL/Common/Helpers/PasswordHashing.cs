using System.Security.Cryptography;

namespace SportsCenterManagement.BLL.Common.Helpers;

public static class PasswordHashing
{
    // PBKDF2 with unique salt per account to prevent rainbow table attacks.
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encodedHash)
    {
        if (encodedHash.StartsWith("$2", StringComparison.Ordinal))
        {
            try
            {
                return BCrypt.Net.BCrypt.Verify(password, encodedHash);
            }
            catch (Exception)
            {
                return false;
            }
        }

        if (encodedHash.Contains(':') && !encodedHash.StartsWith("$", StringComparison.Ordinal))
        {
            var colonParts = encodedHash.Split(':');
            if (colonParts.Length == 3 && int.TryParse(colonParts[1], out var iter) && iter is >= 10_000 and <= 1_000_000)
            {
                try
                {
                    var salt = Convert.FromBase64String(colonParts[0]);
                    var expected = Convert.FromBase64String(colonParts[2]);
                    if (salt.Length != SaltSize || expected.Length != HashSize)
                    {
                        _ = Rfc2898DeriveBytes.Pbkdf2(password, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
                        return false;
                    }
                    var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
                    return CryptographicOperations.FixedTimeEquals(actual, expected);
                }
                catch (FormatException)
                {
                    _ = Rfc2898DeriveBytes.Pbkdf2(password, new byte[SaltSize], Iterations, HashAlgorithmName.SHA256, HashSize);
                    return false;
                }
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
