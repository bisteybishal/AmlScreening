using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Interface.Services
{
    public interface ICustomerScreeningServices
    {
        Task<CustomerScreeningResponse>ScreenAsync(CustomerScreeningRequest request,CancellationToken cancellationToken=default);
    }
}
