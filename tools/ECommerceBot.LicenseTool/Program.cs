// ECommerceBot License Tool
// ─────────────────────────────────────────────────────────────────────────────
// VENDOR ONLY — Do not distribute with customer packages.
// This tool holds the private RSA key and generates signed customer licenses.
//
// Usage:
//   ECommerceBot.LicenseTool generate    -- Interactive license generation
//   ECommerceBot.LicenseTool keygen      -- Generate a new RSA-2048 key pair
// ─────────────────────────────────────────────────────────────────────────────

using ECommerceBot.LicenseTool;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "generate";

switch (command)
{
    case "keygen":
        RunKeyGen();
        break;
    case "generate":
        await RunGenerateAsync();
        break;
    default:
        Console.WriteLine("Usage: ECommerceBot.LicenseTool [generate|keygen]");
        break;
}

// ── RSA key pair generation ───────────────────────────────────────────────────
static void RunKeyGen()
{
    Console.WriteLine("Generating RSA-2048 key pair...\n");

    using var rsa = RSA.Create(2048);

    var privateKey = rsa.ExportRSAPrivateKeyPem();
    var publicKey  = rsa.ExportRSAPublicKeyPem();

    Console.WriteLine("=== PRIVATE KEY (store securely — NEVER share or commit) ===");
    Console.WriteLine(privateKey);
    Console.WriteLine("\n=== PUBLIC KEY (embed in customer deployments: License:PublicKey) ===");
    Console.WriteLine(publicKey);

    var privateKeyFile = $"private_key_{DateTime.UtcNow:yyyyMMdd}.pem";
    File.WriteAllText(privateKeyFile, privateKey);
    Console.WriteLine($"\nPrivate key saved to: {privateKeyFile}");
    Console.WriteLine("Put the PUBLIC KEY value in the customer's appsettings.json under License:PublicKey.");
}

// ── License generation ────────────────────────────────────────────────────────
static async Task RunGenerateAsync()
{
    Console.WriteLine("=== ECommerceBot License Generator ===\n");

    var privateKeyFile = Prompt("Private key file path (e.g. private_key.pem)");
    if (!File.Exists(privateKeyFile))
    {
        Console.WriteLine($"Error: file not found: {privateKeyFile}");
        return;
    }
    var privateKeyPem = await File.ReadAllTextAsync(privateKeyFile);

    var license = new LicensePackage
    {
        LicenseKey    = Guid.NewGuid().ToString("N").ToUpperInvariant()[..16],
        OwnerName     = Prompt("Owner name"),
        OwnerEmail    = Prompt("Owner email (optional)", optional: true),
        CustomerName  = Prompt("Customer / company name (optional)", optional: true),
        ProductName   = Prompt("Product name", defaultValue: "ECommerceBot"),
        Edition       = Prompt("Edition (Standard/Professional/Enterprise)", defaultValue: "Standard"),
        BotUsername   = Prompt("Bot @username to bind (optional, without @)", optional: true),
        AllowedDomain = Prompt("Domain to bind (optional, e.g. example.com)", optional: true),
        MaxUsers      = int.Parse(Prompt("Max users (0 = unlimited)", defaultValue: "0")),
        MaxAdmins     = int.Parse(Prompt("Max admins (0 = unlimited)", defaultValue: "0")),
        IsTrial       = YesNo("Is trial license?"),
        IssuedAt      = DateTime.UtcNow.ToString("O"),
    };

    var expiryYears = int.Parse(Prompt("License validity (years, 0 = never expires)", defaultValue: "1"));
    license.ExpiresAt = expiryYears > 0 ? DateTime.UtcNow.AddYears(expiryYears).ToString("O") : null;

    // Sign the license
    var payload = BuildPayload(license);
    license.Signature = Sign(payload, privateKeyPem);

    var json = JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
    var packageBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

    Console.WriteLine("\n=== LICENSE PACKAGE (send this to customer) ===");
    Console.WriteLine(packageBase64);

    var outputFile = $"license_{license.LicenseKey}.txt";
    await File.WriteAllTextAsync(outputFile, packageBase64);
    Console.WriteLine($"\nLicense saved to: {outputFile}");
    Console.WriteLine("\nCustomer activates by pasting this in the Telegram admin panel → 🔐 وضعیت لایسنس → 🔑 فعال‌سازی");
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static string BuildPayload(LicensePackage l) =>
    JsonSerializer.Serialize(new
    {
        l.LicenseKey, l.OwnerName, l.OwnerEmail, l.CustomerName,
        l.ProductName, l.Edition, l.BotUsername, l.AllowedDomain,
        l.MaxUsers, l.MaxAdmins, l.IsTrial,
        IssuedAt = l.IssuedAt,
        ExpiresAt = l.ExpiresAt
    });

static string Sign(string payload, string privateKeyPem)
{
    using var rsa = RSA.Create();
    rsa.ImportFromPem(privateKeyPem);
    var bytes = rsa.SignData(
        Encoding.UTF8.GetBytes(payload),
        HashAlgorithmName.SHA256,
        RSASignaturePadding.Pkcs1);
    return Convert.ToBase64String(bytes);
}

static string Prompt(string label, string defaultValue = "", bool optional = false)
{
    var suffix = optional ? " (optional, press Enter to skip)" : (string.IsNullOrEmpty(defaultValue) ? "" : $" [{defaultValue}]");
    Console.Write($"{label}{suffix}: ");
    var input = Console.ReadLine()?.Trim() ?? string.Empty;
    return string.IsNullOrEmpty(input) ? defaultValue : input;
}

static bool YesNo(string label)
{
    Console.Write($"{label} (y/N): ");
    return (Console.ReadLine()?.Trim().ToLowerInvariant() ?? "n") == "y";
}
