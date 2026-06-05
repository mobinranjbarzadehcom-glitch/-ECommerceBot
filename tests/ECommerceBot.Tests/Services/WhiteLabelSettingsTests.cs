using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Telegram.Services;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

/// <summary>
/// Validates that white-label branding keys are resolvable via IBotTextService (CMS-backed).
/// All brand values must be readable from BotSettings so they can be customized per customer.
/// </summary>
public class WhiteLabelSettingsTests
{
    private readonly Mock<IBotTextService> _textsMock = new();

    private void SetupKey(string key, string value)
    {
        _textsMock.Setup(t => t.GetAsync(key, It.IsAny<string>())).ReturnsAsync(value);
    }

    [Theory]
    [InlineData(BotSettingKeys.BrandName, "MyShop")]
    [InlineData(BotSettingKeys.BrandShortName, "MS")]
    [InlineData(BotSettingKeys.BrandSupportUsername, "@support")]
    [InlineData(BotSettingKeys.BrandWebsiteUrl, "https://myshop.com")]
    [InlineData(BotSettingKeys.BrandFooterText, "Powered by MyShop")]
    [InlineData(BotSettingKeys.BrandPrimaryEmoji, "🛒")]
    [InlineData(BotSettingKeys.BrandSuccessEmoji, "✅")]
    [InlineData(BotSettingKeys.BrandWarningEmoji, "⚠️")]
    [InlineData(BotSettingKeys.BrandErrorEmoji, "❌")]
    public async Task BrandKey_IsReadableFromBotTextService(string key, string expected)
    {
        SetupKey(key, expected);

        var result = await _textsMock.Object.GetAsync(key, string.Empty);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task BrandName_WithPremiumEmoji_PreservesTag()
    {
        const string brandWithEmoji = "<tg-emoji emoji-id=\"5368324170671202286\">🛒</tg-emoji> MyShop";
        SetupKey(BotSettingKeys.BrandName, brandWithEmoji);

        var result = await _textsMock.Object.GetAsync(BotSettingKeys.BrandName, string.Empty);

        Assert.Contains("<tg-emoji emoji-id=", result);
        Assert.Contains("</tg-emoji>", result);
        Assert.Contains("MyShop", result);
    }

    [Fact]
    public async Task BrandKey_WhenNotConfigured_ReturnsFallback()
    {
        _textsMock.Setup(t => t.GetAsync(BotSettingKeys.BrandName, It.IsAny<string>()))
            .ReturnsAsync((string key, string fallback) => fallback);

        var result = await _textsMock.Object.GetAsync(BotSettingKeys.BrandName, "ECommerceBot");

        Assert.Equal("ECommerceBot", result);
    }

    [Fact]
    public void AllBrandKeys_AreNonEmpty()
    {
        var keys = new[]
        {
            BotSettingKeys.BrandName,
            BotSettingKeys.BrandShortName,
            BotSettingKeys.BrandSupportUsername,
            BotSettingKeys.BrandWebsiteUrl,
            BotSettingKeys.BrandFooterText,
            BotSettingKeys.BrandPrimaryEmoji,
            BotSettingKeys.BrandSuccessEmoji,
            BotSettingKeys.BrandWarningEmoji,
            BotSettingKeys.BrandErrorEmoji
        };

        foreach (var key in keys)
            Assert.False(string.IsNullOrWhiteSpace(key), $"Key '{key}' should not be empty.");
    }
}
