using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.UnitOfWork;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Telegram.Bot;

namespace ECommerceBot.API.Infrastructure.Licensing;

public class LicenseService : ILicenseService
{
    private readonly IUnitOfWork _uow;
    private readonly ILicenseSignatureValidator _signatureValidator;
    private readonly IServerFingerprintService _fingerprint;
    private readonly ITelegramBotClient _botClient;
    private readonly LicenseOptions _options;
    private readonly LicenseStatusCache _cache;
    private readonly ILogger<LicenseService> _logger;

    public LicenseService(
        IUnitOfWork uow,
        ILicenseSignatureValidator signatureValidator,
        IServerFingerprintService fingerprint,
        ITelegramBotClient botClient,
        IOptions<LicenseOptions> options,
        LicenseStatusCache cache,
        ILogger<LicenseService> logger)
    {
        _uow = uow;
        _signatureValidator = signatureValidator;
        _fingerprint = fingerprint;
        _botClient = botClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    public LicenseValidationResult GetCachedResult() => _cache.Result;

    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            var bypass = LicenseValidationResult.From(LicenseStatus.Valid, "Licensing is disabled (development mode).");
            _cache.Result = bypass;
            _cache.LastCheckedAt = DateTime.UtcNow;
            return bypass;
        }

        var license = await _uow.Licenses.GetActiveAsync();

        if (license is null)
        {
            var result = LicenseValidationResult.NotActivated();
            _cache.Result = result;
            _cache.LastCheckedAt = DateTime.UtcNow;
            return result;
        }

        if (!license.IsActive)
            return Cache(LicenseValidationResult.From(LicenseStatus.Disabled, "License has been deactivated."));

        // ── Signature ─────────────────────────────────────────────────────────
        if (!_signatureValidator.Validate(license))
            return Cache(LicenseValidationResult.From(LicenseStatus.SignatureInvalid,
                "License signature is invalid. Contact your vendor."));

        // ── Expiration ────────────────────────────────────────────────────────
        if (license.ExpiresAt.HasValue && license.ExpiresAt < DateTime.UtcNow)
        {
            if (license.GracePeriodEndsAt.HasValue && license.GracePeriodEndsAt > DateTime.UtcNow)
                _logger.LogWarning("License in grace period. Expires: {GraceEnd}", license.GracePeriodEndsAt);
            else
                return Cache(LicenseValidationResult.From(LicenseStatus.Expired,
                    $"License expired on {license.ExpiresAt:yyyy-MM-dd}."));
        }

        // ── Bot binding ───────────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(license.BotUsername))
        {
            try
            {
                var me = await _botClient.GetMe(ct);
                if (!string.Equals(me.Username, license.BotUsername.TrimStart('@'), StringComparison.OrdinalIgnoreCase))
                    return Cache(LicenseValidationResult.From(LicenseStatus.BotMismatch,
                        $"License is bound to @{license.BotUsername}, but this bot is @{me.Username}."));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not verify bot username for license check.");
            }
        }

        // ── Server fingerprint ────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(license.ServerFingerprint))
        {
            var current = _fingerprint.GetFingerprint();
            if (!string.Equals(license.ServerFingerprint, current, StringComparison.OrdinalIgnoreCase))
                return Cache(LicenseValidationResult.From(LicenseStatus.ServerMismatch,
                    "License is bound to a different server fingerprint."));
        }

        // ── User / Admin limits ───────────────────────────────────────────────
        int currentUsers = 0, currentAdmins = 0;
        if (license.MaxUsers > 0)
        {
            currentUsers = await _uow.Users.CountAsync(u => !u.IsBlocked);
            if (currentUsers > license.MaxUsers)
                return Cache(LicenseValidationResult.From(LicenseStatus.UserLimitExceeded,
                    $"User limit exceeded ({currentUsers}/{license.MaxUsers})."));
        }
        if (license.MaxAdmins > 0)
        {
            currentAdmins = await _uow.Users.CountAsync(u => u.Role == Enums.UserRole.Admin);
            if (currentAdmins > license.MaxAdmins)
                return Cache(LicenseValidationResult.From(LicenseStatus.AdminLimitExceeded,
                    $"Admin limit exceeded ({currentAdmins}/{license.MaxAdmins})."));
        }

        // ── Update last-validated timestamp ───────────────────────────────────
        license.LastValidatedAt = DateTime.UtcNow;
        _uow.Licenses.Update(license);
        await _uow.SaveChangesAsync(ct);

        var isGrace = license.ExpiresAt.HasValue && license.ExpiresAt < DateTime.UtcNow &&
                      license.GracePeriodEndsAt.HasValue && license.GracePeriodEndsAt > DateTime.UtcNow;

        var status = isGrace ? LicenseStatus.GracePeriod
                   : license.IsTrial ? LicenseStatus.Trial
                   : LicenseStatus.Valid;

        var valid = LicenseValidationResult.Valid(
            license.OwnerName, license.CustomerName, license.Edition, license.IsTrial,
            license.ExpiresAt, license.MaxUsers, currentUsers, license.MaxAdmins, currentAdmins,
            license.BotUsername, license.ServerFingerprint, status);

        return Cache(valid);
    }

    public async Task<LicenseValidationResult> ActivateAsync(string licensePackage, CancellationToken ct = default)
    {
        try
        {
            // Decode base64 license package
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(licensePackage.Trim()));
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            var license = new LicenseInfo
            {
                LicenseKey = doc.GetProperty("licenseKey").GetString() ?? string.Empty,
                OwnerName = doc.GetProperty("ownerName").GetString() ?? string.Empty,
                OwnerEmail = doc.TryGetProperty("ownerEmail", out var oe) ? oe.GetString() : null,
                CustomerName = doc.TryGetProperty("customerName", out var cn) ? cn.GetString() : null,
                ProductName = doc.TryGetProperty("productName", out var pn) ? pn.GetString() ?? "ECommerceBot" : "ECommerceBot",
                Edition = doc.TryGetProperty("edition", out var ed) ? ed.GetString() ?? "Standard" : "Standard",
                BotUsername = doc.TryGetProperty("botUsername", out var bu) ? bu.GetString() : null,
                AllowedDomain = doc.TryGetProperty("allowedDomain", out var ad) ? ad.GetString() : null,
                MaxUsers = doc.TryGetProperty("maxUsers", out var mu) ? mu.GetInt32() : 0,
                MaxAdmins = doc.TryGetProperty("maxAdmins", out var ma) ? ma.GetInt32() : 0,
                IsTrial = doc.TryGetProperty("isTrial", out var it) && it.GetBoolean(),
                IssuedAt = doc.TryGetProperty("issuedAt", out var ia) ? DateTime.Parse(ia.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
                ExpiresAt = doc.TryGetProperty("expiresAt", out var ex) && ex.ValueKind != JsonValueKind.Null ? (DateTime?)DateTime.Parse(ex.GetString()!, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
                Signature = doc.GetProperty("signature").GetString() ?? string.Empty,
                IsActive = true,
                ActivatedAt = DateTime.UtcNow,
                GracePeriodEndsAt = null
            };

            if (!_signatureValidator.Validate(license))
                return LicenseValidationResult.From(LicenseStatus.SignatureInvalid,
                    "License package signature is invalid.");

            // Deactivate any existing license
            var existing = await _uow.Licenses.GetActiveAsync();
            if (existing is not null)
            {
                existing.IsActive = false;
                _uow.Licenses.Update(existing);
            }

            await _uow.Licenses.AddAsync(license);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("License activated: {Key} for {Owner} ({Edition})",
                license.LicenseKey, license.OwnerName, license.Edition);

            return await ValidateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License activation failed.");
            return LicenseValidationResult.From(LicenseStatus.Invalid,
                "Failed to parse license package. Ensure you copied the full license key.");
        }
    }

    private LicenseValidationResult Cache(LicenseValidationResult result)
    {
        _cache.Result = result;
        _cache.LastCheckedAt = DateTime.UtcNow;
        return result;
    }
}
