
using AmlScreening.Domain.Entities;
namespace AmlScreening.Application.Interface.Repositories
{
    public interface ISubscriptionRepository
    {
       Task<IEnumerable<SubscriptionPlan>> GetAllAsync();
        Task<SubscriptionPlan> GetByIdAsync(int id);
        Task<int> CreateSubscriptionAsync(SubscriptionPlan plan);
        Task<bool> DeleteAsync(int id);
    }
}
