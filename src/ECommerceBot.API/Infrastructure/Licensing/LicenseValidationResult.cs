using ECommerceBot.API.Enums;

namespace ECommerceBot.API.Infrastructure.Licensing;

public class LicenseValidationResult
{
    public LicenseStatus Status { get; init; }
    public bool IsValid => Status is LicenseStatus.Valid or LicenseStatus.Trial or LicenseStatus.GracePeriod;
    public string Message { get; init; } = string.Empty;
    public DateTime? ExpiresAt { get; init; }
    public int DaysRemaining { get; init; }
    public string? OwnerName { get; init; }
    public string? CustomerName { get; init; }
    public string? Edition { get; init; }
    public int MaxUsers { get; init; }
    public int CurrentUsers { get; init; }
    public int MaxAdmins { get; init; }
    public int CurrentAdmins { get; init; }
    public string? BotUsername { get; init; }
    public string? ServerFingerprint { get; init; }
    public bool IsTrial { get; init; }

    public static LicenseValidationResult NotActivated() => new()
    {
        Status = LicenseStatus.NotActivated,
        Message = "No active license found. Please activate your license."
    };

    public static LicenseValidationResult From(LicenseStatus status, string message) => new()
    {
        Status = status,
        Message = message
    };

    public static LicenseValidationResult Valid(
        string ownerName, string? customerName, string edition, bool isTrial,
        DateTime? expiresAt, int maxUsers, int currentUsers, int maxAdmins, int currentAdmins,
        string? botUsername, string? fingerprint, LicenseStatus status = LicenseStatus.Valid) => new()
    {
        Status = status,
        Message = isTrial ? "Trial license active." : "License is valid.",
        OwnerName = ownerName,
        CustomerName = customerName,
        Edition = edition,
        IsTrial = isTrial,
        ExpiresAt = expiresAt,
        DaysRemaining = expiresAt.HasValue ? Math.Max(0, (int)(expiresAt.Value - DateTime.UtcNow).TotalDays) : int.MaxValue,
        MaxUsers = maxUsers,
        CurrentUsers = currentUsers,
        MaxAdmins = maxAdmins,
        CurrentAdmins = currentAdmins,
        BotUsername = botUsername,
        ServerFingerprint = fingerprint
    };
}
