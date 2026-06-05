using System.Text.Json.Serialization;

namespace ECommerceBot.LicenseTool;

/// <summary>Represents the signed license data sent to the customer.</summary>
public class LicensePackage
{
    [JsonPropertyName("licenseKey")]    public string LicenseKey { get; set; } = string.Empty;
    [JsonPropertyName("ownerName")]     public string OwnerName { get; set; } = string.Empty;
    [JsonPropertyName("ownerEmail")]    public string? OwnerEmail { get; set; }
    [JsonPropertyName("customerName")]  public string? CustomerName { get; set; }
    [JsonPropertyName("productName")]   public string ProductName { get; set; } = "ECommerceBot";
    [JsonPropertyName("edition")]       public string Edition { get; set; } = "Standard";
    [JsonPropertyName("botUsername")]   public string? BotUsername { get; set; }
    [JsonPropertyName("allowedDomain")] public string? AllowedDomain { get; set; }
    [JsonPropertyName("maxUsers")]      public int MaxUsers { get; set; } = 0;
    [JsonPropertyName("maxAdmins")]     public int MaxAdmins { get; set; } = 0;
    [JsonPropertyName("isTrial")]       public bool IsTrial { get; set; } = false;
    [JsonPropertyName("issuedAt")]      public string IssuedAt { get; set; } = DateTime.UtcNow.ToString("O");
    [JsonPropertyName("expiresAt")]     public string? ExpiresAt { get; set; }
    [JsonPropertyName("signature")]     public string Signature { get; set; } = string.Empty;
}
