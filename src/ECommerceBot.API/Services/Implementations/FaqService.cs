using ECommerceBot.API.Entities;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class FaqService : IFaqService
{
    private readonly IUnitOfWork _uow;

    public FaqService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<IEnumerable<FaqItem>> GetActiveAsync(int tenantId) =>
        await _uow.FaqItems.GetActiveByTenantIdAsync(tenantId);

    public async Task<IEnumerable<FaqItem>> GetAllAsync(int tenantId) =>
        await _uow.FaqItems.FindAsync(f => f.TenantId == tenantId);

    public async Task<FaqItem?> GetByIdAsync(int id) =>
        await _uow.FaqItems.GetByIdAsync(id);

    public async Task<ServiceResult<FaqItem>> CreateAsync(int tenantId, string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question))
            return ServiceResult<FaqItem>.Failure("سوال نمی‌تواند خالی باشد.");
        if (string.IsNullOrWhiteSpace(answer))
            return ServiceResult<FaqItem>.Failure("پاسخ نمی‌تواند خالی باشد.");

        var order = await _uow.FaqItems.GetNextDisplayOrderAsync(tenantId);
        var item = new FaqItem
        {
            TenantId = tenantId,
            Question = question.Trim(),
            Answer = answer.Trim(),
            DisplayOrder = order,
            IsActive = true
        };

        await _uow.FaqItems.AddAsync(item);
        await _uow.SaveChangesAsync();
        return ServiceResult<FaqItem>.Success(item);
    }

    public async Task<ServiceResult<FaqItem>> UpdateAsync(int id, string question, string answer)
    {
        var item = await _uow.FaqItems.GetByIdAsync(id);
        if (item is null) return ServiceResult<FaqItem>.Failure("سوال یافت نشد.");

        if (!string.IsNullOrWhiteSpace(question)) item.Question = question.Trim();
        if (!string.IsNullOrWhiteSpace(answer)) item.Answer = answer.Trim();

        _uow.FaqItems.Update(item);
        await _uow.SaveChangesAsync();
        return ServiceResult<FaqItem>.Success(item);
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var item = await _uow.FaqItems.GetByIdAsync(id);
        if (item is null) return ServiceResult.Failure("سوال یافت نشد.");

        _uow.FaqItems.Remove(item);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> ToggleActiveAsync(int id)
    {
        var item = await _uow.FaqItems.GetByIdAsync(id);
        if (item is null) return ServiceResult.Failure("سوال یافت نشد.");

        item.IsActive = !item.IsActive;
        _uow.FaqItems.Update(item);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success();
    }
}
