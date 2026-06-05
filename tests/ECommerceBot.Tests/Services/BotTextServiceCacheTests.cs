using ECommerceBot.API.Infrastructure.Cache;
using ECommerceBot.API.Repositories.Interfaces;
using ECommerceBot.API.Telegram.Services;
using ECommerceBot.API.UnitOfWork;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class BotTextServiceCacheTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IBotSettingRepository> _settingRepoMock;
    private readonly Mock<ICacheService> _cacheMock;
    private readonly BotTextService _sut;

    public BotTextServiceCacheTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _settingRepoMock = new Mock<IBotSettingRepository>();
        _uowMock.Setup(u => u.BotSettings).Returns(_settingRepoMock.Object);

        _cacheMock = new Mock<ICacheService>();
        // Default: cache returns null (miss) unless overridden per test
        _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>())).ReturnsAsync((string?)null);
        _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
                  .Returns(Task.CompletedTask);
        _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

        _sut = new BotTextService(_uowMock.Object, _cacheMock.Object);
    }

    [Fact]
    public async Task GetAsync_WhenNotInDbOrCache_ReturnsFallbackDefault()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("WelcomeMessage")).ReturnsAsync((string?)null);

        var result = await _sut.GetAsync("WelcomeMessage");

        Assert.Contains("Welcome", result);
    }

    [Fact]
    public async Task GetAsync_WhenInDb_ReturnsDatabaseValue()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("WelcomeMessage"))
            .ReturnsAsync("Custom welcome!");

        var result = await _sut.GetAsync("WelcomeMessage");

        Assert.Equal("Custom welcome!", result);
    }

    [Fact]
    public async Task GetAsync_CalledTwice_OnlyHitsDbOnce()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("HelpMessage")).ReturnsAsync("Help text");

        // First call: cache miss → DB hit; second call: cache hit
        _cacheMock.SetupSequence(c => c.GetAsync("botsettings:HelpMessage"))
            .ReturnsAsync((string?)null)
            .ReturnsAsync("Help text");

        await _sut.GetAsync("HelpMessage");
        await _sut.GetAsync("HelpMessage");

        _settingRepoMock.Verify(r => r.GetValueAsync("HelpMessage"), Times.Once);
    }

    [Fact]
    public async Task SetAsync_InvalidatesCacheForKey()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("WelcomeMessage")).ReturnsAsync("Old value");
        _settingRepoMock.Setup(r => r.UpsertAsync("WelcomeMessage", "New value", null)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        // Populate via DB
        await _sut.GetAsync("WelcomeMessage");

        // Update — should remove the cache entry
        await _sut.SetAsync("WelcomeMessage", "New value");

        _cacheMock.Verify(c => c.RemoveAsync("botsettings:WelcomeMessage"), Times.Once);
    }

    [Fact]
    public async Task FormatAsync_ReplacesTemplateVariables()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("OrderApprovedMessage"))
            .ReturnsAsync("✅ Order #{orderId} Approved!\n\nKeys:\n{keys}");

        var result = await _sut.FormatAsync("OrderApprovedMessage", new()
        {
            ["orderId"] = "123",
            ["keys"] = "🔑 ABC-DEF"
        });

        Assert.Contains("123", result);
        Assert.Contains("ABC-DEF", result);
        Assert.DoesNotContain("{orderId}", result);
        Assert.DoesNotContain("{keys}", result);
    }

    [Fact]
    public async Task GetAsync_WithTgEmojiInTemplate_PreservesTag()
    {
        var templateWithEmoji = "Hello <tg-emoji emoji-id=\"5368324170671202286\">👋</tg-emoji>!";
        _settingRepoMock.Setup(r => r.GetValueAsync("WelcomeMessage")).ReturnsAsync(templateWithEmoji);

        var result = await _sut.GetAsync("WelcomeMessage");

        // <tg-emoji> must survive unchanged — HTML is not sanitised in bot messages
        Assert.Contains("<tg-emoji emoji-id=\"5368324170671202286\">", result);
        Assert.Contains("</tg-emoji>", result);
    }

    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsCustomDefaultValue()
    {
        _settingRepoMock.Setup(r => r.GetValueAsync("NonExistent.Key")).ReturnsAsync((string?)null);

        var result = await _sut.GetAsync("NonExistent.Key", "my default");

        Assert.Equal("my default", result);
    }
}
