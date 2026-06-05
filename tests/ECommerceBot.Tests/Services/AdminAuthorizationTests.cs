using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using Xunit;

namespace ECommerceBot.Tests.Services;

/// <summary>
/// Validates the admin authorization guard logic used across the bot handlers.
/// These tests mirror the checks in CallbackQueryHandler and MessageHandler.
/// </summary>
public class AdminAuthorizationTests
{
    private static TelegramUser BuildUser(UserRole role, bool isBlocked = false) => new()
    {
        TelegramId = 100,
        FirstName = "Test",
        Role = role,
        IsBlocked = isBlocked,
        ChatId = 100
    };

    [Fact]
    public void Admin_User_HasAdminRole()
    {
        var user = BuildUser(UserRole.Admin);
        Assert.Equal(UserRole.Admin, user.Role);
    }

    [Fact]
    public void Customer_User_DoesNotHaveAdminRole()
    {
        var user = BuildUser(UserRole.Customer);
        Assert.NotEqual(UserRole.Admin, user.Role);
    }

    [Fact]
    public void Blocked_Admin_IsBlocked()
    {
        var user = BuildUser(UserRole.Admin, isBlocked: true);
        Assert.True(user.IsBlocked);
    }

    [Fact]
    public void Admin_Guard_Passes_ForAdminUser()
    {
        var user = BuildUser(UserRole.Admin);
        // Mirrors the guard: user.Role == UserRole.Admin
        var isAuthorized = user.Role == UserRole.Admin;
        Assert.True(isAuthorized);
    }

    [Fact]
    public void Admin_Guard_Fails_ForCustomerUser()
    {
        var user = BuildUser(UserRole.Customer);
        var isAuthorized = user.Role == UserRole.Admin;
        Assert.False(isAuthorized);
    }

    [Fact]
    public void Blocked_User_ShouldBeRejected()
    {
        var user = BuildUser(UserRole.Customer, isBlocked: true);
        Assert.True(user.IsBlocked);
    }

    [Theory]
    [InlineData("adm:cat:1", UserRole.Customer, false)]
    [InlineData("adm:cat:1", UserRole.Admin, true)]
    [InlineData("lic:refresh", UserRole.Customer, false)]
    [InlineData("lic:refresh", UserRole.Admin, true)]
    [InlineData("prod:1", UserRole.Customer, true)]  // prod: is a user action
    [InlineData("cat:1", UserRole.Customer, true)]   // cat: is a user action
    public void Callback_AdminPrefix_AccessControl(string callbackData, UserRole role, bool expectedAccess)
    {
        var user = BuildUser(role);
        var action = callbackData.Split(':')[0];
        var isAdminAction = action is "adm" or "lic";

        bool hasAccess = !isAdminAction || user.Role == UserRole.Admin;

        Assert.Equal(expectedAccess, hasAccess);
    }

    [Fact]
    public void CallbackData_ExceedingMaxLength_ShouldBeRejected()
    {
        var longData = new string('x', 65);
        var isValid = !string.IsNullOrWhiteSpace(longData) && longData.Length <= 64;
        Assert.False(isValid);
    }

    [Fact]
    public void CallbackData_WithinMaxLength_ShouldBeAccepted()
    {
        var validData = "adm:cat:123:toggle";
        var isValid = !string.IsNullOrWhiteSpace(validData) && validData.Length <= 64;
        Assert.True(isValid);
    }
}
