using ECommerceBot.API.Entities;

namespace ECommerceBot.API.Repositories.Interfaces;

public interface ISubscriptionPlanRepository : IGenericRepository<SubscriptionPlan>
{
    Task<IEnumerable<SubscriptionPlan>> GetActivePlansAsync();
}
