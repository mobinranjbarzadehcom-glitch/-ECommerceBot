namespace ECommerceBot.API.Telegram.Options;

public class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BotToken { get; set; } = string.Empty;
    public string WebhookSecretToken { get; set; } = string.Empty;
    public long[] AdminChatIds { get; set; } = Array.Empty<long>();
    public long[] SuperAdminChatIds { get; set; } = Array.Empty<long>();
    public long BackupChannelId { get; set; }

    /// <summary>Slug for the default (legacy single-bot) tenant.</summary>
    public string DefaultTenantSlug { get; set; } = "default";
}
