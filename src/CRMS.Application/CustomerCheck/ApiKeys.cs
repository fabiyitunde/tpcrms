using System.Security.Cryptography;
using System.Text;

namespace CRMS.Application.CustomerCheck;

/// <summary>
/// Consumer keys: 32 random bytes, URL-safe, behind a fixed prefix so a key is recognisable in a
/// partner's configuration. Only the SHA-256 of the key is stored; the key has enough entropy that
/// a plain hash cannot be reversed by guessing.
/// </summary>
public static class ApiKeys
{
    public const string Prefix = "crms_";

    public static (string Key, string Hash, string KeyPrefix) Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var key = Prefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (key, Hash(key), KeyPrefixOf(key));
    }

    public static string Hash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()))).ToLowerInvariant();

    /// <summary>Prefix plus the first six characters of the random part.</summary>
    public static string KeyPrefixOf(string key) =>
        key.Length <= Prefix.Length + 6 ? key : key[..(Prefix.Length + 6)];

    /// <summary>A callback signing secret: same entropy, its own prefix so the two are never mixed
    /// up in a partner's configuration. Stored encrypted, not hashed, because the CRMS must use it.</summary>
    public const string SecretPrefix = "crms_whsec_";

    public static (string Secret, string SecretPrefix) GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var secret = SecretPrefix + Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (secret, secret[..(SecretPrefix.Length + 6)]);
    }
}
