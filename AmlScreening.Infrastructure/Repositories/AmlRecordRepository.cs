using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class AmlRecordRepository : IAmlRecordRepository
{
    private readonly IDBConnectionFactory _connectionFactory;

    public AmlRecordRepository(IDBConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AmlRecord>> GetAllAsync()
    {
        const string sql = """
            SELECT Id, RecordType, FullName, DateOfBirth, Country, Nationality,
                   ReferenceNo, SourceName, Remarks, IsActive, CreatedDate,
                   CreatedBy, UpdatedDate, UpdatedBy
            FROM dbo.AmlRecords
            ORDER BY CreatedDate DESC, Id DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AmlRecord>(sql);
        return records.AsList();
    }

    public async Task<AmlRecord?> GetByIdAsync(int id)
    {
        const string sql = """
            SELECT Id, RecordType, FullName, DateOfBirth, Country, Nationality,
                   ReferenceNo, SourceName, Remarks, IsActive, CreatedDate,
                   CreatedBy, UpdatedDate, UpdatedBy
            FROM dbo.AmlRecords
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<AmlRecord>(sql, new { Id = id });
    }

    public async Task<int> CreateAsync(AmlRecord record)
    {
        const string sql = """
            INSERT INTO dbo.AmlRecords
                (RecordType, FullName, DateOfBirth, Country, Nationality,
                 ReferenceNo, SourceName, Remarks, IsActive, CreatedDate, CreatedBy)
            OUTPUT INSERTED.Id
            VALUES
                (@RecordType, @FullName, @DateOfBirth, @Country, @Nationality,
                 @ReferenceNo, @SourceName, @Remarks, @IsActive, @CreatedDate, @CreatedBy);
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteScalarAsync<int>(sql, record);
    }

    public async Task<bool> UpdateAsync(AmlRecord record)
    {
        const string sql = """
            UPDATE dbo.AmlRecords
            SET RecordType = @RecordType,
                FullName = @FullName,
                DateOfBirth = @DateOfBirth,
                Country = @Country,
                Nationality = @Nationality,
                ReferenceNo = @ReferenceNo,
                SourceName = @SourceName,
                Remarks = @Remarks,
                IsActive = @IsActive,
                UpdatedDate = @UpdatedDate,
                UpdatedBy = @UpdatedBy
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, record) == 1;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM dbo.AmlRecords WHERE Id = @Id;";
        using var connection = _connectionFactory.CreateConnection();
        return await connection.ExecuteAsync(sql, new { Id = id }) == 1;
    }
}
