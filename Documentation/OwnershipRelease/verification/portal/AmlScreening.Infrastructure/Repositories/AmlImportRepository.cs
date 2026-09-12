using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class AmlImportRepository(IDBConnectionFactory factory, IConfiguration configuration) : IAmlImportRepository
{
    public async Task SaveAsync(IEnumerable<AmlRecord> records, CancellationToken cancellationToken = default)
    {
        if (!int.TryParse(configuration["SourceSync:StorageClientId"], out var storageClientId) || storageClientId <= 0)
            throw new InvalidOperationException("Configure SourceSync:StorageClientId using your existing system storage client.");
        var rows = records.ToList();
        if (rows.Count == 0) throw new InvalidOperationException("An empty import is not allowed.");
        var sourceName = rows.Select(x => x.SourceName).Distinct(StringComparer.Ordinal).Single();
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        var source = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT SourceCode FROM dbo.AmlSources WHERE SourceName=@SourceName AND IsActive=1;",
            new { SourceName = sourceName }, cancellationToken:cancellationToken));
        if (source is null) throw new InvalidOperationException("Unknown or disabled import source.");
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var lockResult = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                DECLARE @Result int;
                EXEC @Result=sys.sp_getapplock @Resource=@Resource,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=0;
                SELECT @Result;
                """, new { Resource="AML-SourceImport-"+source }, transaction, cancellationToken:cancellationToken));
            if (lockResult < 0) throw new InvalidOperationException("This source is already being imported.");
            foreach (var record in rows)
            {
                await connection.ExecuteAsync(new CommandDefinition("dbo.spa_AML_Record_BulkInsert", new
                {
                    record.RecordType,record.FullName,record.SourceName,record.DateOfBirth,
                    record.Nationality,record.Remarks,record.ReferenceNo,record.Country,
                    ClientId=storageClientId,OwnerUserId=(long?)null,IsShared=true,CreatedBy="SourceSync:"+source
                }, transaction, commandType:CommandType.StoredProcedure,cancellationToken:cancellationToken));
            }
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT dbo.AmlSyncRuns(SourceCode,StartedAtUtc,CompletedAtUtc,Status,RecordCount)
                VALUES(@Source,SYSUTCDATETIME(),SYSUTCDATETIME(),N'SUCCEEDED',@Count);
                """, new { Source=source,Count=rows.Count }, transaction,cancellationToken:cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // Dispose also rolls back if cancellation prevents an explicit rollback.
            if (transaction.Connection != null) await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
