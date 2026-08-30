using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class AmlRecordRepository : IAmlRecordRepository
{
    private readonly AmlScreeningDbContext _dbContext;
    public AmlRecordRepository(AmlScreeningDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<PagedResult<AmlRecord>> GetAllAsync(int pageNumber, int pageSize)
    {
        var query = _dbContext.AmlRecords.AsNoTracking().Where(r => r.DeleteFg != "y");
        var totalRecords = await query.CountAsync();
        var record = await query.OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AmlRecord>
        {
            Items = record,
            TotalCount = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        //return await _dbContext.AmlRecords.AsNoTracking().Where(r=>r.DeleteFg!="y").OrderByDescending(r=>r.CreatedDate)
        //    .ThenByDescending(r=> r.Id).ToListAsync();
    }
    public async Task<IReadOnlyList<AmlRecord>> GetAllAsync()
    {
        return await _dbContext.AmlRecords.AsNoTracking().Where(r => r.DeleteFg != "y").OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id).ToListAsync();
    }


    public async Task<AmlRecord?> GetByIdAsync(int id)
    {
        return await _dbContext.AmlRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> CreateAsync(AmlRecord record)
    {
        _dbContext.AmlRecords.Add(record);
        await _dbContext.SaveChangesAsync();
        return record.Id;
    }

    public async Task<bool> UpdateAsync(AmlRecord record, string updateby)
    {

        record.UpdatedDate = DateTime.UtcNow;
        record.UpdatedBy = updateby;
        _dbContext.AmlRecords.Update(record);
        return await _dbContext.SaveChangesAsync() > 0;


    }
    public async Task<bool> DeleteAsync(int id, string deletedBy)
    {
        var record = await _dbContext.AmlRecords.FindAsync(id);
        if (record == null) return false;

        record.DeleteFg = "Y";
        record.DeleteDate = DateTime.UtcNow;
        record.DeleteBy = deletedBy;
        record.IsActive = false;

        return await _dbContext.SaveChangesAsync() > 0;
    }
    //private readonly IDBConnectionFactory _connectionFactory;

    //public AmlRecordRepository(IDBConnectionFactory connectionFactory)
    //{
    //    _connectionFactory = connectionFactory;
    //}

    //public async Task<IReadOnlyList<AmlRecord>> GetAllAsync()
    //{
    //    const string sql = """
    //        SELECT Id, RecordType, FullName, DateOfBirth, Country, Nationality,
    //               ReferenceNo, SourceName, Remarks, IsActive, CreatedDate,
    //               CreatedBy, UpdatedDate, UpdatedBy
    //        FROM dbo.AmlRecords
    //        ORDER BY CreatedDate DESC, Id DESC;
    //        """;

    //    using var connection = _connectionFactory.CreateConnection();
    //    var records = await connection.QueryAsync<AmlRecord>(sql);
    //    return records.AsList();
    //}

    //public async Task<AmlRecord?> GetByIdAsync(int id)
    //{
    //    const string sql = """
    //        SELECT Id, RecordType, FullName, DateOfBirth, Country, Nationality,
    //               ReferenceNo, SourceName, Remarks, IsActive, CreatedDate,
    //               CreatedBy, UpdatedDate, UpdatedBy
    //        FROM dbo.AmlRecords
    //        WHERE Id = @Id;
    //        """;

    //    using var connection = _connectionFactory.CreateConnection();
    //    return await connection.QuerySingleOrDefaultAsync<AmlRecord>(sql, new { Id = id });
    //}

    //public async Task<int> CreateAsync(AmlRecord record)
    //{
    //    const string sql = """
    //        INSERT INTO dbo.AmlRecords
    //            (RecordType, FullName, DateOfBirth, Country, Nationality,
    //             ReferenceNo, SourceName, Remarks, IsActive, CreatedDate, CreatedBy)
    //        OUTPUT INSERTED.Id
    //        VALUES
    //            (@RecordType, @FullName, @DateOfBirth, @Country, @Nationality,
    //             @ReferenceNo, @SourceName, @Remarks, @IsActive, @CreatedDate, @CreatedBy);
    //        """;

    //    using var connection = _connectionFactory.CreateConnection();
    //    return await connection.ExecuteScalarAsync<int>(sql, record);
    //}

    //public async Task<bool> UpdateAsync(AmlRecord record)
    //{
    //    const string sql = """
    //        UPDATE dbo.AmlRecords
    //        SET RecordType = @RecordType,
    //            FullName = @FullName,
    //            DateOfBirth = @DateOfBirth,
    //            Country = @Country,
    //            Nationality = @Nationality,
    //            ReferenceNo = @ReferenceNo,
    //            SourceName = @SourceName,
    //            Remarks = @Remarks,
    //            IsActive = @IsActive,
    //            UpdatedDate = @UpdatedDate,
    //            UpdatedBy = @UpdatedBy
    //        WHERE Id = @Id;
    //        """;

    //    using var connection = _connectionFactory.CreateConnection();
    //    return await connection.ExecuteAsync(sql, record) == 1;
    //}

    //public async Task<bool> DeleteAsync(int id)
    //{
    //    const string sql = "DELETE FROM dbo.AmlRecords WHERE Id = @Id;";
    //    using var connection = _connectionFactory.CreateConnection();
    //    return await connection.ExecuteAsync(sql, new { Id = id }) == 1;
    //}
}
