using ECommerceBot.API.Infrastructure.Localization;
using ECommerceBot.API.Telegram.Services;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class LocalizationServiceTests
{
    private readonly Mock<IBotTextService> _textsMock = new();
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        _sut = new LocalizationService(_textsMock.Object);
    }

    [Fact]
    public async Task GetAsync_WhenLangKeyExists_ReturnsLangSpecific()
    {
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.en", "")).ReturnsAsync("Welcome EN");

        var result = await _sut.GetAsync("WelcomeMessage", "en");

        Assert.Equal("Welcome EN", result);
    }

    [Fact]
    public async Task GetAsync_WhenLangKeyMissing_FallsBackToPersian()
    {
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.de", "")).ReturnsAsync(string.Empty);
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.fa", "")).ReturnsAsync("خوش آمدید");

        var result = await _sut.GetAsync("WelcomeMessage", "de");

        Assert.Equal("خوش آمدید", result);
    }

    [Fact]
    public async Task GetAsync_WhenPersianKeyMissing_FallsBackToBaseKey()
    {
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.fa", "")).ReturnsAsync(string.Empty);
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage", "")).ReturnsAsync("Base welcome");

        var result = await _sut.GetAsync("WelcomeMessage", "fa");

        Assert.Equal("Base welcome", result);
    }

    [Fact]
    public async Task GetAsync_WhenAllKeysMissing_ReturnsDefaultValue()
    {
        _textsMock.Setup(t => t.GetAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(string.Empty);

        var result = await _sut.GetAsync("NonExistent.Key", "fa", "fallback default");

        Assert.Equal("fallback default", result);
    }

    [Fact]
    public async Task GetAsync_Persian_SkipsEnKeyLookup()
    {
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.fa", "")).ReturnsAsync("خوش آمدید FA");

        var result = await _sut.GetAsync("WelcomeMessage", "fa");

        Assert.Equal("خوش آمدید FA", result);
        _textsMock.Verify(t => t.GetAsync("WelcomeMessage.en", It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetAsync_English_WithTgEmoji_PreservesHtml()
    {
        var template = "Hello <tg-emoji emoji-id=\"5368324170671202286\">👋</tg-emoji>!";
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.en", "")).ReturnsAsync(template);

        var result = await _sut.GetAsync("WelcomeMessage", "en");

        Assert.Contains("<tg-emoji emoji-id=\"5368324170671202286\">", result);
        Assert.Contains("</tg-emoji>", result);
    }

    [Fact]
    public async Task FormatAsync_ReplacesVariablesAfterLookup()
    {
        _textsMock.Setup(t => t.GetAsync("OrderPendingMessage.en", "")).ReturnsAsync("Order #{orderId} pending");

        var result = await _sut.FormatAsync("OrderPendingMessage", "en", new() { ["orderId"] = "42" });

        Assert.Equal("Order #42 pending", result);
        Assert.DoesNotContain("{orderId}", result);
    }

    [Fact]
    public async Task GetAsync_NullLanguage_DefaultsToPersian()
    {
        _textsMock.Setup(t => t.GetAsync("WelcomeMessage.fa", "")).ReturnsAsync("فارسی");

        var result = await _sut.GetAsync("WelcomeMessage", null!);

        Assert.Equal("فارسی", result);
    }
}
