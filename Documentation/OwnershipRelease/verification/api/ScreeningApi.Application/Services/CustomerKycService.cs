using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Services;

public sealed class CustomerKycService(ICustomerKycRepository repository, CustomerKycRiskCalculator calculator)
{
    public async Task<CustomerKycResponse> ProcessAsync(ApiActor actor, CustomerKycRequest request, CancellationToken ct)
    {
        if (actor.UserId<=0 || actor.ClientId<=0 || actor.ApiApplicationId<=0 ||
            !actor.CanManageCustomers || !actor.CanScreen)
            throw new KycClientException("The authenticated application is not permitted to screen customers.");

        request.NormalizeReference();
        if (request.Customer is null || string.IsNullOrWhiteSpace(request.Customer.FullName))
            throw new ArgumentException("Customer full name is required.");
        if (request.Customer.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Date of birth cannot be in the future.");

        await using var session = await repository.BeginAsync(actor, ct);
        var customerId = await session.SaveCustomerAsync(request, ct);
        var aml = await session.ScreenAsync(request.Customer.FullName, ct);
        var setup = await session.GetApprovedSetupAsync(ct);
        var result = calculator.Calculate(request, aml, setup.Rules, setup.Bands);
        result.CustomerId = customerId;
        await session.SaveResultAsync(result, ct);
        await session.CommitAsync(ct);
        return result;
    }

    public Task<CustomerKycResponse?> GetResultAsync(ApiActor actor, long resultId, CancellationToken ct)
    {
        if (actor.UserId<=0 || actor.ClientId<=0 || actor.ApiApplicationId<=0 || !actor.CanViewCustomers)
            throw new KycClientException("The authenticated application is not permitted to view KYC results.");
        if (resultId<=0) throw new ArgumentException("A valid result ID is required.");
        return repository.GetResultAsync(actor, resultId, ct);
    }
}

