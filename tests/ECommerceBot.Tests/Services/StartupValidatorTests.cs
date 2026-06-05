using ECommerceBot.API.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ECommerceBot.Tests.Services;

public class StartupValidatorTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static IHostEnvironment BuildEnv(string name)
    {
        var mock = new Mock<IHostEnvironment>();
        mock.Setup(e => e.EnvironmentName).Returns(name);
        return mock.Object;
    }

    private static ILogger BuildLogger() => new Mock<ILogger>().Object;

    [Fact]
    public void Validate_WithAllRequiredConfig_DoesNotThrow()
    {
        var config = BuildConfig(new()
        {
            ["Telegram:BotToken"] = "123:abc",
            ["Telegram:WebhookSecretToken"] = "secret",
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=Test;",
            ["Telegram:AdminChatIds:0"] = "123456789",
            ["Telegram:SuperAdminChatIds:0"] = "987654321",
            ["Security:AesKey"] = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef"
        });

        var exception = Record.Exception(() =>
            StartupValidator.Validate(config, BuildEnv("Production"), BuildLogger()));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingBotToken_InProduction_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Telegram:BotToken"] = "",
            ["Telegram:WebhookSecretToken"] = "secret",
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=Test;",
            ["Telegram:AdminChatIds:0"] = "123456789"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StartupValidator.Validate(config, BuildEnv("Production"), BuildLogger()));
    }

    [Fact]
    public void Validate_MissingConnectionString_InProduction_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Telegram:BotToken"] = "123:abc",
            ["Telegram:WebhookSecretToken"] = "secret",
            ["ConnectionStrings:DefaultConnection"] = "",
            ["Telegram:AdminChatIds:0"] = "123456789"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StartupValidator.Validate(config, BuildEnv("Production"), BuildLogger()));
    }

    [Fact]
    public void Validate_MissingAllRequired_InDevelopment_DoesNotThrow()
    {
        var config = BuildConfig(new()
        {
            ["Telegram:BotToken"] = "",
            ["Telegram:WebhookSecretToken"] = "",
            ["ConnectionStrings:DefaultConnection"] = ""
        });

        // In Development, missing config is a warning, not a fatal error
        var exception = Record.Exception(() =>
            StartupValidator.Validate(config, BuildEnv("Development"), BuildLogger()));

        Assert.Null(exception);
    }

    [Fact]
    public void Validate_MissingWebhookSecret_InProduction_Throws()
    {
        var config = BuildConfig(new()
        {
            ["Telegram:BotToken"] = "123:abc",
            ["Telegram:WebhookSecretToken"] = "",
            ["ConnectionStrings:DefaultConnection"] = "Server=.;Database=Test;",
            ["Telegram:AdminChatIds:0"] = "123456789"
        });

        Assert.Throws<InvalidOperationException>(() =>
            StartupValidator.Validate(config, BuildEnv("Production"), BuildLogger()));
    }
}
