using System.Security.Cryptography;
using System.Text;

namespace ECommerceBot.API.Infrastructure.Security;

/// <summary>
/// AES-256-GCM symmetric encryption for bot tokens stored in DB.
/// Key must be 32 bytes (64 hex chars) set via Security:AesKey config.
/// </summary>
public class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(IConfiguration configuration)
    {
        var hexKey = configuration["Security:AesKey"] ?? string.Empty;

        if (hexKey.Length == 64)
        {
            _key = Convert.FromHexString(hexKey);
        }
        else
        {
            // Derive a 32-byte key from whatever string is provided (dev fallback only)
            using var sha = SHA256.Create();
            _key = sha.ComputeHash(Encoding.UTF8.GetBytes(hexKey.PadRight(8, '0')));
        }
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;

        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        // Format: base64(nonce + tag + ciphertext)
        var combined = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        nonce.CopyTo(combined, 0);
        tag.CopyTo(combined, nonce.Length);
        cipherBytes.CopyTo(combined, nonce.Length + tag.Length);

        return Convert.ToBase64String(combined);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return string.Empty;

        var combined = Convert.FromBase64String(cipherText);
        const int nonceSize = 12;
        const int tagSize = 16;

        var nonce = combined[..nonceSize];
        var tag = combined[nonceSize..(nonceSize + tagSize)];
        var cipher = combined[(nonceSize + tagSize)..];

        var plainBytes = new byte[cipher.Length];

        using var aes = new AesGcm(_key, AesGcm.TagByteSizes.MaxSize);
        aes.Decrypt(nonce, cipher, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }
}
