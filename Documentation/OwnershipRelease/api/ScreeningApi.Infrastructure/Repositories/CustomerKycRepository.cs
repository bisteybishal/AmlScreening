using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;
using ScreeningApi.Infrastructure.Persistence;

namespace ScreeningApi.Infrastructure.Repositories;

public sealed class CustomerKycRepository(IDBConnectionFactory factory) : ICustomerKycRepository
{
    public async Task<ICustomerKycSession> BeginAsync(ApiActor actor, CancellationToken ct)
    {
        var connection = factory.CreateConnection();
        try
        {
            await connection.OpenAsync(ct);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            return new Session(connection, transaction, actor);
        }
        catch { await connection.DisposeAsync(); throw; }
    }


    public async Task<CustomerKycResponse?> GetResultAsync(ApiActor actor, long resultId, CancellationToken ct)
    {
        using var connection = factory.CreateConnection();
        const string sql = """
            SELECT k.Id AS ResultId,k.CustomerId,k.CustomerReference,k.RiskScore,k.RiskLevel,k.Decision,
              k.RiskBandId,k.BandVersion,k.CalculatedAtUtc,k.FactorsJson,k.AmlResultJson
            FROM dbo.CustomerKycResults k
            WHERE k.Id=@ResultId
              AND dbo.fn_HasPermission(@UserId,N'Customers.View')=1
              AND (dbo.fn_IsSystemAdmin(@UserId)=1 OR (k.ClientId=@ClientId AND k.OwnerUserId=@UserId))
              AND EXISTS(SELECT 1 FROM dbo.ApiApplications a JOIN dbo.Users u ON u.UserId=a.OwnerUserId
                JOIN dbo.Clients c ON c.Id=u.ClientId WHERE a.Id=@ApplicationId AND a.IsActive=1
                AND u.UserId=@UserId AND u.ClientId=@ClientId AND u.IsActive=1 AND c.IsActive=1 AND c.DeleteFg='N');
            """;
        var row = await connection.QuerySingleOrDefaultAsync<HistoryRow>(new CommandDefinition(sql,
            new { ResultId=resultId, UserId=actor.UserId, actor.ClientId, ApplicationId=actor.ApiApplicationId }, cancellationToken:ct));
        if (row == null) return null;
        return new CustomerKycResponse
        {
            ResultId=row.ResultId, CustomerId=row.CustomerId, CustomerReference=row.CustomerReference,
            RiskScore=row.RiskScore, RiskLevel=row.RiskLevel, Decision=row.Decision,
            RiskBandId=row.RiskBandId, BandVersion=row.BandVersion, CalculatedAtUtc=row.CalculatedAtUtc,
            Factors=JsonSerializer.Deserialize<List<KycFactorResult>>(row.FactorsJson) ?? throw new InvalidOperationException("Invalid saved factor history."),
            AmlResult=JsonSerializer.Deserialize<CustomerScreeningResponse>(row.AmlResultJson) ?? throw new InvalidOperationException("Invalid saved AML history.")
        };
    }
    private sealed class HistoryRow
    {
        public long ResultId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerReference { get; set; } = "";
        public int RiskScore { get; set; }
        public string RiskLevel { get; set; } = "";
        public string Decision { get; set; } = "";
        public int RiskBandId { get; set; }
        public int BandVersion { get; set; }
        public DateTime CalculatedAtUtc { get; set; }
        public string FactorsJson { get; set; } = "[]";
        public string AmlResultJson { get; set; } = "{}";
    }

    private sealed class Session(SqlConnection connection, SqlTransaction transaction, ApiActor actor) : ICustomerKycSession
    {
        private int clientId => actor.ClientId;
        private string? requestJson;
        private CommandDefinition Command(string sql, object? args, CancellationToken ct,
            CommandType type = CommandType.Text) => new(sql, args, transaction,
                commandTimeout: 60, commandType: type, cancellationToken: ct);

        public async Task<int> SaveCustomerAsync(CustomerKycRequest request, CancellationToken ct)
        {
            var active = await connection.ExecuteScalarAsync<int>(Command("""
                SELECT COUNT(*) FROM dbo.Clients c
                JOIN dbo.Users u ON u.ClientId=c.Id AND u.UserId=@OwnerUserId AND u.IsActive=1
                JOIN dbo.ApiApplications a ON a.OwnerUserId=u.UserId AND a.Id=@ApiApplicationId AND a.IsActive=1
                WHERE c.Id=@ClientId AND c.IsActive=1 AND c.DeleteFg='N'
                  AND dbo.fn_HasPermission(u.UserId,N'Customers.Manage')=1
                  AND dbo.fn_HasPermission(u.UserId,N'Screening.View')=1;
                """, new { ClientId = clientId, OwnerUserId = actor.UserId, actor.ApiApplicationId }, ct));
            if (active != 1) throw new KycClientException("The authenticated client is not active.");
            var c = request.Customer;
            requestJson = JsonSerializer.Serialize(request);
            return await connection.QuerySingleAsync<int>(Command("""
                DECLARE @Id int;
                IF (SELECT COUNT(*) FROM dbo.Customers WITH (UPDLOCK,HOLDLOCK)
                    WHERE ClientId=@ClientId AND OwnerUserId=@OwnerUserId AND CustomerReference=@Reference) > 1
                    THROW 50010, 'Duplicate customer references require correction.', 1;
                SELECT @Id=Id FROM dbo.Customers WITH (UPDLOCK,HOLDLOCK)
                    WHERE ClientId=@ClientId AND OwnerUserId=@OwnerUserId AND CustomerReference=@Reference;
                IF @Id IS NULL
                BEGIN
                    INSERT dbo.Customers (ClientId,OwnerUserId,CustomerReference,FullName,DateOfBirth,Country,
                        Nationality,Occupation,SourceOfFunds,IdentificationType,IdentificationNumber,IsPep,RiskScore,RiskLevel,CreatedBy)
                    VALUES (@ClientId,@OwnerUserId,@Reference,@FullName,@DateOfBirth,@Country,@Nationality,
                        @Occupation,@SourceOfFunds,@IdentificationType,@IdentificationNumber,0,0,N'PENDING',N'KYC API');
                    SET @Id=CONVERT(int,SCOPE_IDENTITY());
                END
                ELSE
                BEGIN
                    IF EXISTS (SELECT 1 FROM dbo.Customers WHERE Id=@Id AND ClientId=@ClientId AND OwnerUserId=@OwnerUserId
                        AND (IsActive=0 OR DeleteFg<>'N'))
                        THROW 50011, 'Customer is inactive or deleted.', 1;
                    UPDATE dbo.Customers SET FullName=@FullName,DateOfBirth=@DateOfBirth,
                        Country=@Country,Nationality=@Nationality,Occupation=@Occupation,
                        SourceOfFunds=@SourceOfFunds,IdentificationType=@IdentificationType,IdentificationNumber=@IdentificationNumber,UpdatedDate=SYSUTCDATETIME(),UpdatedBy=N'KYC API'
                    WHERE Id=@Id AND ClientId=@ClientId AND OwnerUserId=@OwnerUserId;
                END;
                SELECT @Id;
                """, new
            {
                ClientId = clientId, OwnerUserId = actor.UserId, Reference = request.CustomerReference.Trim(), FullName = c.FullName.Trim(),
                DateOfBirth = c.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
                Country = c.Country?.Trim() ?? "", Nationality = c.Nationality?.Trim() ?? "",
                Occupation = c.Occupation?.Trim() ?? "", SourceOfFunds = c.SourceOfFunds?.Trim() ?? "",
                IdentificationType = c.IdentificationType?.Trim(), IdentificationNumber = c.IdentificationNumber?.Trim()
            }, ct));
        }

        public Task<CustomerScreeningDbResult> ScreenAsync(string fullName, CancellationToken ct) =>
            connection.QuerySingleAsync<CustomerScreeningDbResult>(Command("dbo.spa_CustomerScreening_Check",
                new { FullName = fullName.Trim(), ClientId = clientId, UserId = actor.UserId }, ct, CommandType.StoredProcedure));

        public async Task<(List<KycRiskRule> Rules, List<KycRiskBand> Bands)> GetApprovedSetupAsync(CancellationToken ct)
        {
            using var grid = await connection.QueryMultipleAsync(Command("""
                DECLARE @Now datetime2=SYSUTCDATETIME();
                SELECT f.Id AS FactorId,f.FactorCode,f.WeightPercent,r.Id AS RuleId,
                    r.RuleVersion,r.ValueCode,r.Score
                FROM dbo.RiskFactors f
                LEFT JOIN dbo.RiskFactorRules r ON r.RiskFactorId=f.Id AND r.ClientId=f.ClientId
                    AND r.IsActive=1 AND r.DeleteFg='N' AND r.ApprovalStatus='APPROVED'
                    AND r.EffectiveFrom<=@Now AND (r.EffectiveTo IS NULL OR r.EffectiveTo>@Now)
                WHERE f.ClientId=@ClientId AND f.ScoreCategory='CUSTOMER'
                    AND f.IsActive=1 AND f.DeleteFg='N'
                ORDER BY f.DisplayOrder,f.Id;
                SELECT Id,BandVersion,RiskLevel,MinimumScore,MaximumScore,Decision
                FROM dbo.RiskBands WHERE ClientId=@ClientId AND IsActive=1
                    AND DeleteFg='N' AND ApprovalStatus='APPROVED';
                """, new { ClientId = clientId, OwnerUserId = actor.UserId, actor.ApiApplicationId }, ct));
            return ((await grid.ReadAsync<KycRiskRule>()).ToList(), (await grid.ReadAsync<KycRiskBand>()).ToList());
        }

        public async Task SaveResultAsync(CustomerKycResponse result, CancellationToken ct)
        {
            result.ResultId = await connection.QuerySingleAsync<long>(Command("""
                INSERT dbo.CustomerKycResults (ClientId,OwnerUserId,ApiApplicationId,SubmissionChannel,CustomerId,CustomerReference,RiskScore,RiskLevel,
                    Decision,RiskBandId,BandVersion,CalculatedAtUtc,FactorsJson,AmlResultJson,CorrelationId,RequestJson)
                OUTPUT INSERTED.Id
                VALUES (@ClientId,@OwnerUserId,@ApiApplicationId,N'API',@CustomerId,@CustomerReference,@RiskScore,@RiskLevel,@Decision,
                    @RiskBandId,@BandVersion,@CalculatedAtUtc,@FactorsJson,@AmlResultJson,@CorrelationId,@RequestJson);
                UPDATE dbo.Customers SET RiskScore=@RiskScore,RiskLevel=@RiskLevel,IsPep=@IsPep,
                    UpdatedDate=SYSUTCDATETIME(),UpdatedBy=N'KYC API'
                WHERE Id=@CustomerId AND ClientId=@ClientId AND OwnerUserId=@OwnerUserId;
                IF @@ROWCOUNT<>1 THROW 50012, 'Customer owner changed; retry the request.', 1;
                INSERT dbo.AuditLogs(ClientId,UserId,Username,Action,EntityType,EntityId,Details,CreatedDate)
                SELECT @ClientId,@OwnerUserId,Username,N'Screen',N'Customer',CONVERT(nvarchar(50),@CustomerId),
                    CONCAT(N'API application ',@ApiApplicationId,N'; correlation ',@CorrelationId),SYSUTCDATETIME()
                FROM dbo.Users WHERE UserId=@OwnerUserId;
                """, new
            {
                ClientId = clientId, OwnerUserId = actor.UserId, actor.ApiApplicationId, result.CustomerId, result.CustomerReference, result.RiskScore,
                result.RiskLevel, result.Decision, result.RiskBandId, result.BandVersion, result.CalculatedAtUtc,
                FactorsJson = JsonSerializer.Serialize(result.Factors),
                RequestJson = requestJson ?? throw new InvalidOperationException("Customer must be saved before the result."),
                AmlResultJson = JsonSerializer.Serialize(result.AmlResult),
                result.AmlResult.CorrelationId, result.AmlResult.IsPep
            }, ct));
        }

        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);

        public async ValueTask DisposeAsync()
        {
            // Disposing an uncommitted transaction rolls back customer and result writes together.
            try { await transaction.DisposeAsync(); }
            finally { await connection.DisposeAsync(); }
        }
    }
}
