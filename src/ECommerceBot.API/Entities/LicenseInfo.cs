namespace ECommerceBot.API.Entities;

public class LicenseInfo : BaseEntity
{
    public int TenantId { get; set; }
    public string LicenseKey { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerEmail { get; set; }
    public string? CustomerName { get; set; }
    public string ProductName { get; set; } = "ECommerceBot";
    public string Edition { get; set; } = "Standard";

    /// <summary>Bot @username this license is locked to. Null = any bot.</summary>
    public string? BotUsername { get; set; }
    /// <summary>Allowed domain (e.g. example.com). Null = any domain.</summary>
    public string? AllowedDomain { get; set; }
    /// <summary>SHA-256 server fingerprint this license is locked to. Null = any server.</summary>
    public string? ServerFingerprint { get; set; }

    public int MaxUsers { get; set; } = 0;    // 0 = unlimited
    public int MaxAdmins { get; set; } = 0;   // 0 = unlimited
    public bool IsTrial { get; set; } = false;
    public bool IsActive { get; set; } = true;

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastValidatedAt { get; set; }
    public DateTime? GracePeriodEndsAt { get; set; }

    /// <summary>Base64-encoded RSA signature over the canonical license fields.</summary>
    public string Signature { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
