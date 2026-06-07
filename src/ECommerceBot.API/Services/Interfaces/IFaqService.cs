using ECommerceBot.API.Entities;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IFaqService
{
    Task<IEnumerable<FaqItem>> GetActiveAsync(int tenantId);
    Task<IEnumerable<FaqItem>> GetAllAsync(int tenantId);
    Task<FaqItem?> GetByIdAsync(int id);
    Task<ServiceResult<FaqItem>> CreateAsync(int tenantId, string question, string answer);
    Task<ServiceResult<FaqItem>> UpdateAsync(int id, string question, string answer);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> ToggleActiveAsync(int id);
}
