using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CustomerKyc.Application;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;

namespace CustomerKyc.Infrastructure;

public sealed class KycRepository(IConfiguration configuration, IDataProtectionProvider protection)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector = protection.CreateProtector("CustomerKyc.InputSnapshot.v1");

    public async Task<ScreeningResponse> ProcessAsync(int clientId, bool allowClear,
        ScreeningRequest input, string retryKey, CancellationToken ct)
    {
        var requestJson = JsonSerializer.Serialize(input, Json);
        var requestHash = SHA256.HashData(Encoding.UTF8.GetBytes(requestJson));
        using var db = new SqlConnection(configuration.GetConnectionString("AMLDatabase"));
        await db.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await db.BeginTransactionAsync(ct);
        CommandDefinition Command(string name, object parameters) => new(name, parameters,
            transaction, commandTimeout:60, commandType:CommandType.StoredProcedure, cancellationToken:ct);

        // Exclusive per-client workflow lock makes retries/customer upserts deterministic.
        // Shared configuration lock coordinates with the existing portal approval service.
        await db.ExecuteAsync(Command("kyc_local.LockClient", new {ClientId=clientId}));
        var saved = await db.QuerySingleOrDefaultAsync<SavedResponse>(Command("kyc_local.GetRetry",
            new {ClientId=clientId, IdempotencyKey=retryKey}));
        if (saved != null)
        {
            if (!CryptographicOperations.FixedTimeEquals(saved.RequestHash, requestHash))
                throw new RequestConflictException();
            await transaction.CommitAsync(ct);
            return JsonSerializer.Deserialize<ScreeningResponse>(saved.ResponseJson, Json)!;
        }

        var matches = (await db.QueryAsync<NameMatch>(Command("kyc_local.ScreenName",
            new {ClientId=clientId, FullName=input.Customer.FullName.Trim()}))).ToList();
        using var grid = await db.QueryMultipleAsync(Command("kyc_local.ReadRisk", new {ClientId=clientId}));
        var factors = (await grid.ReadAsync<Factor>()).ToList();
        var rules = (await grid.ReadAsync<CustomerKyc.Application.Rule>()).ToList();
        var bands = (await grid.ReadAsync<Band>()).ToList();
        var isUn = matches.Any(x => x.SourceName == "UN Sanctions");
        var isEu = matches.Any(x => x.SourceName == "EU Sanctions");
        var isPep = matches.Any(x => x.SourceName is "PEP" or "PEP Local" or "PEP International");
        var risk = CustomerRiskCalculator.Calculate(input.Customer, isPep, factors, rules, bands);
        var response = new ScreeningResponse
        {
            ClientReference=input.ClientReference,
            ScreeningStatus=matches.Count>0 ? "POTENTIAL_MATCH" : "CLEAR",
            IsUnMatch=isUn, IsEuMatch=isEu, IsPep=isPep,
            HighestMatchScore=matches.Count>0 ? 100 : 0,
            CustomerRiskScore=risk.Score, RiskLevel=risk.Level,
            Decision=risk.Decision == "BLOCK" ? "BLOCK"
                : matches.Count>0 || !allowClear ? "REVIEW" : risk.Decision,
            CorrelationId=Guid.NewGuid().ToString("N")
        };
        if (isUn) response.Reasons.Add("Potential UN exact-name match found.");
        if (isEu) response.Reasons.Add("Potential EU exact-name match found.");
        if (isPep) response.Reasons.Add("Potential PEP exact-name match found; identity is not confirmed.");
        if (matches.Count==0) response.Reasons.Add("No exact-name match in this client's configured shared lists. This is not identity verification.");
        response.Reasons.AddRange(risk.Reasons);
        if (!allowClear)
            response.Reasons.Add("Local testing is in review-only mode; automatic clearance has not been enabled.");

        var c=input.Customer;
        await db.ExecuteAsync(Command("kyc_local.SaveResult", new
        {
            ClientId=clientId, IdempotencyKey=retryKey, RequestHash=requestHash,
            ClientReference=input.ClientReference.Trim(), FullName=c.FullName.Trim(),
            DateOfBirth=c.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            Country=c.Country??"", Nationality=c.Nationality??"",
            Occupation=c.Occupation??"", SourceOfFunds=c.SourceOfFunds??"",
            response.CorrelationId, RiskScore=risk.Score, RiskLevel=risk.Level,
            CalculationStatus=risk.Score.HasValue && risk.Level!=null ? "CALCULATED" : "INCOMPLETE",
            response.Decision, BandId=risk.BandId, BandVersion=risk.BandVersion,
            ResponseJson=JsonSerializer.Serialize(response,Json),
            InputSnapshot=_protector.Protect(requestJson),
            FactorJson=JsonSerializer.Serialize(risk.Factors,Json),
            MatchJson=JsonSerializer.Serialize(matches,Json)
        }));
        await transaction.CommitAsync(ct);
        return response;
    }
    private sealed class SavedResponse
    {
        public byte[] RequestHash {get;set;}=[];
        public string ResponseJson {get;set;}="";
    }
}
