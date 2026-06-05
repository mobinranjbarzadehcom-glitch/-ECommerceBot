using System.Text.Json;
using ECommerceBot.API.Entities;
using ECommerceBot.API.Enums;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Telegram.States;

public class ConversationManager : IConversationManager
{
    private readonly IUnitOfWork _uow;

    public ConversationManager(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task SetStateAsync(TelegramUser user, ConversationState state, CancellationToken ct = default)
    {
        user.CurrentState = state;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task ClearStateAsync(TelegramUser user, CancellationToken ct = default)
    {
        user.CurrentState = ConversationState.None;
        user.TempData = null;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    public Task<OrderContext?> GetOrderContextAsync(TelegramUser user)
    {
        if (string.IsNullOrEmpty(user.TempData)) return Task.FromResult<OrderContext?>(null);
        try
        {
            var ctx = JsonSerializer.Deserialize<OrderContext>(user.TempData);
            return Task.FromResult(ctx);
        }
        catch
        {
            return Task.FromResult<OrderContext?>(null);
        }
    }

    public async Task SetOrderContextAsync(TelegramUser user, OrderContext context, CancellationToken ct = default)
    {
        user.TempData = JsonSerializer.Serialize(context);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    public Task<AdminContext?> GetAdminContextAsync(TelegramUser user)
    {
        if (string.IsNullOrEmpty(user.TempData)) return Task.FromResult<AdminContext?>(null);
        try
        {
            var ctx = JsonSerializer.Deserialize<AdminContext>(user.TempData);
            return Task.FromResult(ctx);
        }
        catch
        {
            return Task.FromResult<AdminContext?>(null);
        }
    }

    public async Task SetAdminContextAsync(TelegramUser user, AdminContext context, CancellationToken ct = default)
    {
        user.TempData = JsonSerializer.Serialize(context);
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateActivityAsync(TelegramUser user, CancellationToken ct = default)
    {
        user.LastActivity = DateTime.UtcNow;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);
    }
}
