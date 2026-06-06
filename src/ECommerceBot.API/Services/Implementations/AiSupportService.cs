using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.Telegram.Services;

namespace ECommerceBot.API.Services.Implementations;

/// <summary>
/// Provides an auto-reply for support tickets when AllowsAiSupport is enabled on the tenant plan.
/// The reply template is stored as a bot setting under key "AiSupport.AutoReplyTemplate".
/// Operators can customize it via the Settings panel. Leave blank to disable auto-reply.
/// </summary>
public class AiSupportService : IAiSupportService
{
    private const string SettingKey = "AiSupport.AutoReplyTemplate";

    private readonly IBotTextService _texts;

    public AiSupportService(IBotTextService texts) => _texts = texts;

    public async Task<string?> GetAutoReplyAsync(string userMessage)
    {
        var template = await _texts.GetAsync(SettingKey, "fa", string.Empty);
        return string.IsNullOrWhiteSpace(template) ? null : template;
    }
}
