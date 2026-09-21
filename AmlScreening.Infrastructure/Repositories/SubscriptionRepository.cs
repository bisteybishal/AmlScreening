using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly IDBConnectionFactory _connectionFactory;
    private readonly ICurrentUserContext _currentUser;

    public SubscriptionRepository(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<SubscriptionPlan>> GetPageAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken=default)
    {
        pageNumber=Math.Max(1, pageNumber);
        pageSize= Math.Clamp(pageSize, 1, 50);
        var offset = ((long)pageNumber - 1) * pageSize;

        const string sql = """
            SELECT COUNT(1)
            FROM dbo.AML_Subscription_plan
            WHERE DeleteFg = 'N';

            SELECT PlanId, SourceCode, SourceName, DurationMonths, Rate, IsActive,
                   DeleteFg, DeleteDate, DeleteBy, CreatedDate, CreatedBy,
                   UpdatedDate, UpdatedBy, SubscriptionKey,ExpireDate,RowVersion
            FROM dbo.AML_Subscription_plan
            WHERE DeleteFg = 'N'
            ORDER BY CreatedDate DESC, PlanId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _connectionFactory.CreateConnection();
       var command=new CommandDefinition(sql, new
       {
           offset= offset,
           pageSize = pageSize

       },cancellationToken:cancellationToken);
        using var result = await connection.QueryMultipleAsync(command);
        var totalCount = await result.ReadSingleAsync<int>();
        var items = (await result.ReadAsync<SubscriptionPlan>()).AsList();

        return new PagedResult<SubscriptionPlan>
        {
            TotalCount = totalCount,
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<SubscriptionPlan?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT PlanId, SourceCode, SourceName, DurationMonths, Rate, IsActive,
                   DeleteFg, DeleteDate, DeleteBy, CreatedDate, CreatedBy,
                   UpdatedDate, UpdatedBy, RowVersion
            FROM dbo.AML_Subscription_plan
            WHERE PlanId = @Id AND DeleteFg = 'N';
            """;
        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SubscriptionPlan>(sql, new { Id = id });
    }

    public async Task<int> CreateSubscriptionAsync(SubscriptionPlan plan,CancellationToken cancellationToken=default)
    {
        const string sql = """
            INSERT INTO dbo.AML_Subscription_plan
                (SourceCode, SourceName, DurationMonths, Rate, IsActive,
                 DeleteFg, CreatedDate, CreatedBy,SubscriptionKey,ExpireDate)
            OUTPUT INSERTED.PlanId
            VALUES
                (@SourceCode, @SourceName, @DurationMonths, @Rate, @IsActive,
                 'N', SYSUTCDATETIME(), @CreatedBy,@SubscriptionKey,@ExpireDate);
            """;

         await using var connection = _connectionFactory.CreateConnection();
       await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var command= new CommandDefinition(sql, new
            {
                plan.SourceCode,
                plan.SourceName,
                plan.DurationMonths,
                plan.Rate,
                plan.IsActive,
                CreatedBy = _currentUser.Username,
                plan.SubscriptionKey,
                plan.ExpireDate
            }, transaction, cancellationToken: cancellationToken);
            var planId = await connection.ExecuteScalarAsync<int>(command);

            
            transaction.Commit();
            return planId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> UpdateAsync(SubscriptionPlan plan)  
    {
        const string sql = """
            UPDATE dbo.AML_Subscription_plan
            SET SourceCode = @SourceCode,
                SourceName = @SourceName,
                DurationMonths = @DurationMonths,
                Rate = @Rate,
                IsActive = @IsActive,
                UpdatedDate = SYSUTCDATETIME(),
                UpdatedBy = @UpdatedBy
            WHERE PlanId = @PlanId
              AND DeleteFg = 'N'
              AND RowVersion = @RowVersion;
            """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var updated = await connection.ExecuteAsync(sql, new
            {
                plan.PlanId,
                plan.SourceCode,
                plan.SourceName,
                plan.DurationMonths,
                plan.Rate,
                plan.IsActive,
                UpdatedBy = _currentUser.Username,
                plan.RowVersion
            }, transaction) == 1;
            if (!updated)
            {
                transaction.Rollback();
                return false;
            }

            
            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> DeleteAsync(int id, byte[] rowVersion)
    {
        const string sql = """
            UPDATE dbo.AML_Subscription_plan
            SET DeleteFg = 'Y',
                DeleteDate = SYSUTCDATETIME(),
                DeleteBy = @DeletedBy,
                IsActive = 0
            WHERE PlanId = @Id
              AND DeleteFg = 'N'
              AND RowVersion = @RowVersion;
            """;

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var deleted = await connection.ExecuteAsync(sql, new
            {
                Id = id,
                DeletedBy = _currentUser.Username,
                RowVersion = rowVersion
            }, transaction) == 1;
            if (!deleted)
            {
                transaction.Rollback();
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

   
}
