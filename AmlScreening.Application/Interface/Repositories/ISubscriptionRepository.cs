
using AmlScreening.Domain.Entities;
namespace AmlScreening.Application.Interface.Repositories
{
    public interface ISubscriptionRepository
    {
        Task<PagedResult<SubscriptionPlan>> GetPageAsync(int pageNumber, int pageSize);
        Task<SubscriptionPlan?> GetByIdAsync(int id);
        Task<int> CreateSubscriptionAsync(SubscriptionPlan plan);
        Task<bool> UpdateAsync(SubscriptionPlan plan);
        Task<bool> DeleteAsync(int id, byte[] rowVersion);
    }
}
