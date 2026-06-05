using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace ECommerceBot.API.Infrastructure.Licensing;

/// <summary>
/// Generates a stable server fingerprint from non-sensitive environment identifiers.
/// Does not collect personal data.
/// </summary>
public class ServerFingerprintService : IServerFingerprintService
{
    private readonly Lazy<string> _fingerprint;

    public ServerFingerprintService()
    {
        _fingerprint = new Lazy<string>(Compute);
    }

    public string GetFingerprint() => _fingerprint.Value;

    private static string Compute()
    {
        var parts = new[]
        {
            Environment.MachineName,
            Environment.OSVersion.Platform.ToString(),
            Environment.OSVersion.Version.ToString(),
            Dns.GetHostName(),
            Environment.GetEnvironmentVariable("HOSTNAME") ?? string.Empty,
            Environment.GetEnvironmentVariable("COMPUTERNAME") ?? string.Empty
        };

        var combined = string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
