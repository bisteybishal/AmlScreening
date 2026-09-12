using AmlScreening.Application.Models;

namespace AmlScreening.Application.Interface.Repositories;

public interface IAccountAccessRepository
{
    Task<AccountAccessInfo?> GetAsync(long userId);
    Task<IReadOnlyList<ApiApplicationInfo>> GetApplicationsAsync(long userId);
    Task SetPortalAccessAsync(long userId, bool enabled);
    Task LinkApplicationAsync(long userId, string issuer, string subject);
    Task SetApplicationActiveAsync(long userId, int applicationId, bool enabled);
}
