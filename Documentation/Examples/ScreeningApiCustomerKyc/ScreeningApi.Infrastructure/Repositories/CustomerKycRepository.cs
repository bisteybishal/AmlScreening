using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;
using ScreeningApi.Infrastructure.Persistence;

namespace ScreeningApi.Infrastructure.Repositories;

public sealed class CustomerKycRepository(
    IDBConnectionFactory connectionFactory,
    IDataProtectionProvider protection) : ICustomerKycRepository
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector =
        protection.CreateProtector("CustomerKyc.InputSnapshot.v1");
    private SqlConnection? _connection;
    private SqlTransaction? _transaction;

    private SqlConnection Connection => _connection
        ?? throw new InvalidOperationException("BeginAsync must be called first.");

    private CommandDefinition Procedure(string name, object parameters, CancellationToken ct)
    {
        if (_transaction == null)
            throw new InvalidOperationException("A database transaction is required.");
        return new(name, parameters, _transaction, commandTimeout: 60,
            commandType: CommandType.StoredProcedure, cancellationToken: ct);
    }

    public async Task BeginAsync(int clientId, CancellationToken ct)
    {
        if (_connection != null) throw new InvalidOperationException("Transaction already started.");
        _connection = connectionFactory.CreateConnection();
        await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(ct);
        await Connection.ExecuteAsync(Procedure("kyc_local.LockClient", new { ClientId = clientId }, ct));
    }

    public Task<SavedKycResponse?> GetSavedAsync(int clientId, string key, CancellationToken ct) =>
        Connection.QuerySingleOrDefaultAsync<SavedKycResponse>(Procedure("kyc_local.GetRetry",
            new { ClientId = clientId, IdempotencyKey = key }, ct));

    public Task<int> SaveCustomerAsync(int clientId, CustomerScreeningRequest request, CancellationToken ct)
    {
        var customer = request.Customer;
        return Connection.QuerySingleAsync<int>(Procedure("kyc_local.SaveCustomer", new
        {
            ClientId = clientId,
            ClientReference = request.ClientReference.Trim(),
            FullName = customer.FullName.Trim(),
            DateOfBirth = customer.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
            Country = customer.Country ?? "",
            Nationality = customer.Nationality ?? "",
            Occupation = customer.Occupation ?? "",
            SourceOfFunds = customer.SourceOfFunds ?? ""
        }, ct));
    }

    public async Task<List<NameMatch>> ScreenAsync(int clientId, string fullName, CancellationToken ct) =>
        (await Connection.QueryAsync<NameMatch>(Procedure("kyc_local.ScreenName",
            new { ClientId = clientId, FullName = fullName }, ct))).ToList();

    public async Task<RiskConfiguration> GetApprovedRiskAsync(int clientId, CancellationToken ct)
    {
        using var results = await Connection.QueryMultipleAsync(
            Procedure("kyc_local.ReadRisk", new { ClientId = clientId }, ct));
        var factors = (await results.ReadAsync<Factor>()).ToList();
        var rules = (await results.ReadAsync<ScreeningApi.Application.Models.Rule>()).ToList();
        var bands = (await results.ReadAsync<Band>()).ToList();
        return new(factors, rules, bands);
    }

    public async Task SaveResultAsync(int clientId, int customerId, string key, byte[] requestHash,
        string requestJson, CustomerScreeningResponse response, RiskResult risk,
        List<NameMatch> matches, CancellationToken ct)
    {
        await Connection.ExecuteAsync(Procedure("kyc_local.SaveResult", new
        {
            ClientId = clientId,
            CustomerId = customerId,
            IdempotencyKey = key,
            RequestHash = requestHash,
            ClientReference = response.ClientReference,
            response.CorrelationId,
            RiskScore = risk.Score,
            RiskLevel = risk.Level,
            CalculationStatus = risk.Score.HasValue && risk.Level != null ? "CALCULATED" : "INCOMPLETE",
            response.Decision,
            risk.BandId,
            risk.BandVersion,
            ResponseJson = JsonSerializer.Serialize(response, Json),
            InputSnapshot = _protector.Protect(requestJson),
            FactorJson = JsonSerializer.Serialize(risk.Factors, Json),
            MatchJson = JsonSerializer.Serialize(matches, Json)
        }, ct));
    }

    public Task CommitAsync(CancellationToken ct) =>
        (_transaction ?? throw new InvalidOperationException("No transaction.")).CommitAsync(ct);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_transaction != null) await _transaction.DisposeAsync();
        }
        finally
        {
            _transaction = null;
            if (_connection != null) await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
