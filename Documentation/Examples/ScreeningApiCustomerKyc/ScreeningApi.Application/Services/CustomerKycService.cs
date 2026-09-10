using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Interface.Services;
using ScreeningApi.Application.Models;

namespace ScreeningApi.Application.Services;

public sealed class CustomerKycService(
    ICurrentClientContext currentClient,
    ICustomerKycRepository repository) : ICustomerKycService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<CustomerScreeningResponse> CheckAsync(
        CustomerScreeningRequest request, string idempotencyKey, CancellationToken ct)
    {
        // Never take ClientId from the request JSON.
        int clientId = currentClient.ClientId;
        if (clientId <= 0) throw new InvalidOperationException("Client context is missing.");
        if (request.Customer is null || string.IsNullOrWhiteSpace(request.ClientReference) ||
            string.IsNullOrWhiteSpace(request.Customer.FullName))
            throw new ArgumentException("Client reference and customer name are required.");
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
            throw new ArgumentException("Idempotency-Key is required and must not exceed 100 characters.");
        if (request.Customer.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Date of birth cannot be in the future.");

        string requestJson = JsonSerializer.Serialize(request, Json);
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(requestJson));

        // Dispose rolls back automatically if anything fails before CommitAsync.
        await using var work = repository;
        await work.BeginAsync(clientId, ct);

        var saved = await work.GetSavedAsync(clientId, idempotencyKey, ct);
        if (saved != null)
        {
            if (!CryptographicOperations.FixedTimeEquals(saved.RequestHash, hash))
                throw new RequestConflictException();
            var previous = JsonSerializer.Deserialize<CustomerScreeningResponse>(saved.ResponseJson, Json)
                ?? throw new InvalidOperationException("Saved response is invalid.");
            await work.CommitAsync(ct);
            return previous;
        }

        // 1. Save/update this client's customer first.
        int customerId = await work.SaveCustomerAsync(clientId, request, ct);

        // 2. Check the client's permitted UN/EU/PEP records.
        var matches = await work.ScreenAsync(clientId, request.Customer.FullName.Trim(), ct);
        bool un = matches.Any(x => x.SourceName == "UN Sanctions");
        bool eu = matches.Any(x => x.SourceName == "EU Sanctions");
        bool pep = matches.Any(x => x.SourceName is "PEP" or "PEP Local" or "PEP International");

        // 3. Read approved rules and bands; calculate the customer score.
        var settings = await work.GetApprovedRiskAsync(clientId, ct);
        var risk = CustomerRiskCalculator.Calculate(request.Customer, pep,
            settings.Factors, settings.Rules, settings.Bands);

        // 4. Build the response using calculated values, not fixed sample numbers.
        var response = new CustomerScreeningResponse
        {
            ClientReference = request.ClientReference.Trim(),
            ScreeningStatus = matches.Count > 0 ? "POTENTIAL_MATCH" : "CLEAR",
            IsUnMatch = un,
            IsEuMatch = eu,
            IsPep = pep,
            HighestMatchScore = matches.Count > 0 ? 100 : 0,
            CustomerRiskScore = risk.Score,
            RiskLevel = risk.Level,
            Decision = risk.Decision == "BLOCK" ? "BLOCK"
                : matches.Count > 0 ? "REVIEW" : risk.Decision,
            CorrelationId = Guid.NewGuid().ToString("N")
        };
        if (un) response.Reasons.Add("Potential UN exact-name match found.");
        if (eu) response.Reasons.Add("Potential EU exact-name match found.");
        if (pep) response.Reasons.Add("Potential PEP exact-name match found; identity is not confirmed.");
        if (matches.Count == 0)
            response.Reasons.Add("No exact-name match found in this client's configured shared AML lists.");
        response.Reasons.AddRange(risk.Reasons);

        // 5. Save the response, factor breakdown and matching evidence.
        await work.SaveResultAsync(clientId, customerId, idempotencyKey, hash,
            requestJson, response, risk, matches, ct);

        // 6. Only return success after all writes commit.
        await work.CommitAsync(ct);
        return response;
    }
}
