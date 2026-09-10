using ScreeningApi.Application.Models;
namespace ScreeningApi.Application.Interface.Repositories;

// All methods use one connection and one transaction for this request.
public interface ICustomerKycRepository : IAsyncDisposable
{
    Task BeginAsync(int clientId, CancellationToken ct);
    Task<SavedKycResponse?> GetSavedAsync(int clientId, string key, CancellationToken ct);
    Task<int> SaveCustomerAsync(int clientId, CustomerScreeningRequest request, CancellationToken ct);
    Task<List<NameMatch>> ScreenAsync(int clientId, string fullName, CancellationToken ct);
    Task<RiskConfiguration> GetApprovedRiskAsync(int clientId, CancellationToken ct);
    Task SaveResultAsync(int clientId, int customerId, string key, byte[] requestHash,
        string requestJson, CustomerScreeningResponse response, RiskResult risk,
        List<NameMatch> matches, CancellationToken ct);
    Task CommitAsync(CancellationToken ct);
}
