using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Interface.Repositories;

public interface ICustomerKycRepository
{
    Task<ICustomerKycSession> BeginAsync(ApiActor actor, CancellationToken cancellationToken);
    Task<CustomerKycResponse?> GetResultAsync(ApiActor actor, long resultId, CancellationToken cancellationToken);
}

public interface ICustomerKycSession : IAsyncDisposable
{
    Task<int> SaveCustomerAsync(CustomerKycRequest request, CancellationToken cancellationToken);
    Task<CustomerScreeningDbResult> ScreenAsync(string fullName, CancellationToken cancellationToken);
    Task<(List<KycRiskRule> Rules, List<KycRiskBand> Bands)> GetApprovedSetupAsync(CancellationToken cancellationToken);
    Task SaveResultAsync(CustomerKycResponse result, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}

