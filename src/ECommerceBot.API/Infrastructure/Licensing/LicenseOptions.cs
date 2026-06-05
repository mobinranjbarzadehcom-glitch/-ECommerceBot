namespace ECommerceBot.API.Infrastructure.Licensing;

public class LicenseOptions
{
    public const string SectionName = "License";

    public bool Enabled { get; set; } = true;
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>RSA public key in PEM format used to verify license signatures.</summary>
    public string PublicKey { get; set; } = string.Empty;

    public bool OfflineValidationEnabled { get; set; } = true;
    public int GracePeriodHours { get; set; } = 72;
    public bool ValidateOnStartup { get; set; } = true;
    public int ValidateIntervalMinutes { get; set; } = 60;

    /// <summary>In production, startup fails and webhook is blocked when license is invalid.</summary>
    public bool RequireValidLicenseInProduction { get; set; } = true;

    public bool AllowTrial { get; set; } = true;
    public int TrialDays { get; set; } = 14;

    public string ProductName { get; set; } = "ECommerceBot";
    public string Edition { get; set; } = "Standard";
}
