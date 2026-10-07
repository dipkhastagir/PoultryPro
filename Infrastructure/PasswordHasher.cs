using System.Security.Cryptography;
using System.Text;

namespace PoultryPro.Infrastructure;

/// <summary>
/// Password hashing. New passwords use PBKDF2-SHA256 (100k iterations, random salt):
///   pbkdf2$100000$&lt;saltBase64&gt;$&lt;hashBase64&gt;
/// Legacy hashes (lower-case hex SHA-256 of password + "PoultryProSalt2025") produced by the
/// original FieldOfficer module and by the SQL seed scripts are still verified.
/// </summary>
public static class PasswordHasher
{
    private const string LegacySalt = "PoultryProSalt2025";
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static string LegacyHash(string password)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password + LegacySalt))).ToLowerInvariant();

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;
        if (storedHash.StartsWith("pbkdf2$", StringComparison.Ordinal))
        {
            var parts = storedHash.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iter)) return false;
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        return string.Equals(LegacyHash(password), storedHash, StringComparison.OrdinalIgnoreCase);
    }
}
