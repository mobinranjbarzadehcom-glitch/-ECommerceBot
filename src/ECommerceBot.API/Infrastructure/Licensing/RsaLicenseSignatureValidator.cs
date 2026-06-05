using ECommerceBot.API.Entities;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ECommerceBot.API.Infrastructure.Licensing;

/// <summary>
/// Validates license signatures using RSA public key verification only.
/// The private key is never stored in customer deployments — it is kept by the vendor.
/// </summary>
public class RsaLicenseSignatureValidator : ILicenseSignatureValidator
{
    private readonly LicenseOptions _options;
    private readonly ILogger<RsaLicenseSignatureValidator> _logger;

    public RsaLicenseSignatureValidator(IOptions<LicenseOptions> options, ILogger<RsaLicenseSignatureValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool Validate(LicenseInfo license)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicKey))
        {
            _logger.LogWarning("License:PublicKey is not configured. Signature validation skipped.");
            return true; // Permissive when key not configured (dev/test mode)
        }

        if (string.IsNullOrWhiteSpace(license.Signature))
        {
            _logger.LogWarning("License {Key} has no signature.", license.LicenseKey);
            return false;
        }

        try
        {
            var payload = BuildSignaturePayload(license);
            var payloadBytes = Encoding.UTF8.GetBytes(payload);
            var signatureBytes = Convert.FromBase64String(license.Signature);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(_options.PublicKey);

            var valid = rsa.VerifyData(
                payloadBytes,
                signatureBytes,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            if (!valid)
                _logger.LogWarning("Signature validation failed for license {Key}.", license.LicenseKey);

            return valid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during signature validation for license {Key}.", license.LicenseKey);
            return false;
        }
    }

    /// <summary>
    /// Builds the canonical string that was signed. Order and formatting must be identical
    /// to the LicenseTool implementation.
    /// </summary>
    public static string BuildSignaturePayload(LicenseInfo license) =>
        JsonSerializer.Serialize(new
        {
            license.LicenseKey,
            license.OwnerName,
            license.OwnerEmail,
            license.CustomerName,
            license.ProductName,
            license.Edition,
            license.BotUsername,
            license.AllowedDomain,
            license.MaxUsers,
            license.MaxAdmins,
            license.IsTrial,
            IssuedAt = license.IssuedAt.ToString("O"),
            ExpiresAt = license.ExpiresAt?.ToString("O")
        });
}
