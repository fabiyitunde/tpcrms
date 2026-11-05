using System.Security.Cryptography;
using System.Text;
using CRMS.Application.CustomerCheck;
using CRMS.Infrastructure.ExternalServices.Rhshf;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.Services;

/// <summary>
/// AES-256-GCM for the customer-check data that must not sit readable in the database: queued
/// requests (they hold the BVN), stored results, and consumers' callback signing secrets.
/// <para>
/// The key is <c>CustomerCheck:DataKey</c> (base64 of 32 random bytes). When it is blank, the key is
/// derived from the hash key so that TEST runs with what is already configured; production must set
/// its own. Rotating the key makes everything stored under the old one unreadable, so it is a
/// deliberate operation, not a routine one.
/// </para>
/// </summary>
public class CustomerCheckCipher : ICustomerCheckCipher
{
    private const string Version = "v1.";
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public CustomerCheckCipher(IOptions<CustomerCheckSettings> settings, IOptions<RhshfSettings> rhshf)
        : this(ResolveKey(settings.Value, rhshf.Value)) { }

    public CustomerCheckCipher(byte[] key)
    {
        if (key.Length != 32)
            throw new ArgumentException("The data key must be 32 bytes.", nameof(key));
        _key = key;
    }

    public static byte[] ResolveKey(CustomerCheckSettings settings, RhshfSettings rhshf)
    {
        if (!string.IsNullOrWhiteSpace(settings.DataKey))
        {
            byte[] configured;
            try { configured = Convert.FromBase64String(settings.DataKey.Trim()); }
            catch (FormatException) { throw new InvalidOperationException("CustomerCheck:DataKey is not valid base64."); }
            if (configured.Length != 32)
                throw new InvalidOperationException("CustomerCheck:DataKey must decode to 32 bytes.");
            return configured;
        }
        return SHA256.HashData(Encoding.UTF8.GetBytes("customer-check-data:" + CustomerCheckService.ResolveHashKey(settings, rhshf)));
    }

    public string Protect(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag);

        var packed = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, packed, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, packed, NonceSize + TagSize, cipher.Length);
        return Version + Convert.ToBase64String(packed);
    }

    public string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext) || !ciphertext.StartsWith(Version, StringComparison.Ordinal))
            throw new CryptographicException("Unrecognised ciphertext format.");
        var packed = Convert.FromBase64String(ciphertext[Version.Length..]);
        if (packed.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext is too short.");

        var nonce = packed.AsSpan(0, NonceSize);
        var tag = packed.AsSpan(NonceSize, TagSize);
        var cipher = packed.AsSpan(NonceSize + TagSize);
        var plain = new byte[cipher.Length];
        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
