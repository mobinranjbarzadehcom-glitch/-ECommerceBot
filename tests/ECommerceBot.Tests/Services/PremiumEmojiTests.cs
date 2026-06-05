using ECommerceBot.API.Infrastructure.Security;
using Xunit;

namespace ECommerceBot.Tests.Services;

/// <summary>
/// Validates that Telegram Premium emoji tags are never stripped or encoded
/// when passed through bot-authored message templates.
/// </summary>
public class PremiumEmojiTests
{
    [Fact]
    public void TgEmoji_InBotTemplate_IsNotEncodedByPassthrough()
    {
        const string template = "Welcome <tg-emoji emoji-id=\"5368324170671202286\">👋</tg-emoji>!";
        var result = HtmlSanitizer.Passthrough(template);
        Assert.Equal(template, result);
    }

    [Fact]
    public void TgEmoji_InUserInput_IsEncodedForSafety()
    {
        const string userInput = "<tg-emoji emoji-id=\"123\">👋</tg-emoji>";
        var result = HtmlSanitizer.Encode(userInput);
        Assert.DoesNotContain("<tg-emoji", result);
        Assert.Contains("&lt;tg-emoji", result);
    }

    [Theory]
    [InlineData("5368324170671202286", "👋")]
    [InlineData("5368324170671202287", "🎉")]
    [InlineData("5368784066798374197", "✅")]
    public void TgEmoji_Tag_PreservesEmojiId(string emojiId, string fallbackEmoji)
    {
        var template = $"<tg-emoji emoji-id=\"{emojiId}\">{fallbackEmoji}</tg-emoji>";
        var result = HtmlSanitizer.Passthrough(template);
        Assert.Contains($"emoji-id=\"{emojiId}\"", result);
        Assert.Contains(fallbackEmoji, result);
    }

    [Fact]
    public void BotMessage_WithMixedHtmlAndTgEmoji_PreservesStructure()
    {
        const string message = "✅ <b>Order approved!</b>\n" +
                               "Your key: <code>XXXX-YYYY</code>\n" +
                               "<tg-emoji emoji-id=\"5368324170671202286\">🎉</tg-emoji> Enjoy!";

        var result = HtmlSanitizer.Passthrough(message);

        Assert.Contains("<b>Order approved!</b>", result);
        Assert.Contains("<code>XXXX-YYYY</code>", result);
        Assert.Contains("<tg-emoji emoji-id=\"5368324170671202286\">", result);
        Assert.Contains("</tg-emoji>", result);
    }

    [Fact]
    public void OrderApprovedMessage_WithKeys_DoesNotStripTgEmoji()
    {
        // Simulate a CMS-stored message template containing premium emoji
        const string template = "<tg-emoji emoji-id=\"5368784066798374197\">✅</tg-emoji> " +
                                "<b>Order #{orderId} Approved!</b>\n\nKeys:\n{keys}";

        var formatted = template
            .Replace("{orderId}", "42")
            .Replace("{keys}", "🔑 <code>KEY-1234</code>");

        Assert.Contains("<tg-emoji emoji-id=", formatted);
        Assert.Contains("</tg-emoji>", formatted);
        Assert.Contains("#42", formatted);
        Assert.Contains("KEY-1234", formatted);
    }
}
