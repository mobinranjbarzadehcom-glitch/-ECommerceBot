using ECommerceBot.API.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class AesEncryptionTests
{
    private static AesEncryptionService CreateService(string key = "")
    {
        // 64 hex chars = 32 bytes AES-256 key
        var hexKey = string.IsNullOrEmpty(key)
            ? "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
            : key;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:AesKey"] = hexKey })
            .Build();

        return new AesEncryptionService(config);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsSameValue()
    {
        var svc = CreateService();
        const string original = "1234567890:ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        var cipher = svc.Encrypt(original);
        var decrypted = svc.Decrypt(cipher);

        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void Encrypt_SamePlaintext_ProducesDifferentCiphertext()
    {
        var svc = CreateService();
        const string plain = "bot-token-123";

        var c1 = svc.Encrypt(plain);
        var c2 = svc.Encrypt(plain);

        // AES-GCM uses random nonce — ciphertexts must differ
        Assert.NotEqual(c1, c2);
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsException()
    {
        var svc1 = CreateService("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var svc2 = CreateService("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var cipher = svc1.Encrypt("secret");

        Assert.ThrowsAny<Exception>(() => svc2.Decrypt(cipher));
    }

    [Fact]
    public void Encrypt_EmptyString_ReturnsEmpty()
    {
        var svc = CreateService();
        Assert.Equal(string.Empty, svc.Encrypt(string.Empty));
    }

    [Fact]
    public void Decrypt_EmptyString_ReturnsEmpty()
    {
        var svc = CreateService();
        Assert.Equal(string.Empty, svc.Decrypt(string.Empty));
    }

    [Fact]
    public void Encrypt_BotToken_RoundTrips()
    {
        var svc = CreateService();
        const string token = "5555555555:AAFfakeTokenForTestingPurposes123";

        var encrypted = svc.Encrypt(token);
        var decrypted = svc.Decrypt(encrypted);

        Assert.Equal(token, decrypted);
        Assert.NotEqual(token, encrypted);
    }
}
