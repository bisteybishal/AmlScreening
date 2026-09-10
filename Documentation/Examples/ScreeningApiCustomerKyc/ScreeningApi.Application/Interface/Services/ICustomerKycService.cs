using ScreeningApi.Application.Models;
namespace ScreeningApi.Application.Interface.Services;
public interface ICustomerKycService
{
    Task<CustomerScreeningResponse> CheckAsync(CustomerScreeningRequest request,
        string idempotencyKey, CancellationToken cancellationToken);
}
