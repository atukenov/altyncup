using System.Security.Cryptography;
using System.Text;

namespace Yurt.Application.Common.Helpers;

/// <summary>
/// Refresh tokens are stored as SHA-256 hashes so a database leak doesn't hand out
/// live sessions. Rows written before hashing was introduced still hold the raw value,
/// so lookups match either form until those legacy tokens expire.
/// </summary>
public static class TokenHasher
{
    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Cryptographically random numeric code, zero-padded to <paramref name="digits"/>.</summary>
    public static string NumericCode(int digits = 4)
        => RandomNumberGenerator.GetInt32(0, (int)Math.Pow(10, digits)).ToString($"D{digits}");
}
