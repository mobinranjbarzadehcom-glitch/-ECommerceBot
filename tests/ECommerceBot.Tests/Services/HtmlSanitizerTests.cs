using ECommerceBot.API.Infrastructure.Security;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class HtmlSanitizerTests
{
    [Fact]
    public void Encode_WithNull_ReturnsEmpty()
    {
        var result = HtmlSanitizer.Encode(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Encode_WithEmpty_ReturnsEmpty()
    {
        var result = HtmlSanitizer.Encode(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Encode_WithAngleBrackets_EncodesCorrectly()
    {
        var result = HtmlSanitizer.Encode("<script>alert('xss')</script>");
        Assert.Equal("&lt;script&gt;alert('xss')&lt;/script&gt;", result);
    }

    [Fact]
    public void Encode_WithAmpersand_EncodesCorrectly()
    {
        var result = HtmlSanitizer.Encode("Tom & Jerry");
        Assert.Equal("Tom &amp; Jerry", result);
    }

    [Fact]
    public void Encode_WithQuotes_EncodesCorrectly()
    {
        var result = HtmlSanitizer.Encode("Say \"hello\"");
        Assert.Equal("Say &quot;hello&quot;", result);
    }

    [Fact]
    public void Encode_PlainText_ReturnsUnchanged()
    {
        var result = HtmlSanitizer.Encode("Hello World 123");
        Assert.Equal("Hello World 123", result);
    }

    [Fact]
    public void Encode_PersianText_ReturnsUnchanged()
    {
        var result = HtmlSanitizer.Encode("سلام دنیا");
        Assert.Equal("سلام دنیا", result);
    }

    [Fact]
    public void Encode_TgEmojiTag_GetsEncoded()
    {
        // User-supplied tg-emoji tags in user input SHOULD be encoded (not preserved)
        // Bot-authored templates with tg-emoji are passed through a separate code path
        var result = HtmlSanitizer.Encode("<tg-emoji emoji-id=\"123\">👋</tg-emoji>");
        Assert.Contains("&lt;tg-emoji", result);
        Assert.DoesNotContain("<tg-emoji", result);
    }

    [Fact]
    public void Passthrough_WithNull_ReturnsEmpty()
    {
        var result = HtmlSanitizer.Passthrough(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Passthrough_WithHtml_ReturnsUnchanged()
    {
        const string html = "<b>bold</b>";
        var result = HtmlSanitizer.Passthrough(html);
        Assert.Equal(html, result);
    }
}
