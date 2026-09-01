using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class AmlRecordRepository : IAmlRecordRepository
{
    private readonly AmlScreeningDbContext _dbContext;
    private readonly ICurrentUserContext _currentUser;
    public AmlRecordRepository(
        AmlScreeningDbContext dbContext,
        ICurrentUserContext currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }
    public async Task<PagedResult<AmlRecord>> GetPageAsync(
        string? search,
        string? recordType,
        bool? isActive,
        int pageNumber,
        int pageSize)
    {
        var query = ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(record =>
                record.FullName.Contains(term) ||
                record.ReferenceNo.Contains(term) ||
                record.SourceName.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(recordType))
        {
            var type = recordType.Trim();
            query = query.Where(record => record.RecordType == type);
        }

        if (isActive.HasValue)
            query = query.Where(record => record.IsActive == isActive.Value);

        var totalRecords = await query.CountAsync();
        var records = await query.OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<AmlRecord>
        {
            Items = records,
            TotalCount = totalRecords,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

    }

    public async Task<AmlRecordSummary> GetSummaryAsync()
    {
        var records = ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking());

        return new AmlRecordSummary
        {
            TotalRecords = await records.CountAsync(),
            ActiveRecords = await records.CountAsync(record => record.IsActive),
            IndividualRecords = await records.CountAsync(record => record.RecordType == "Individual"),
            EntityRecords = await records.CountAsync(record => record.RecordType == "Entity"),
            CountriesCovered = await records
                .Where(record => record.Country != "")
                .Select(record => record.Country)
                .Distinct()
                .CountAsync()
        };
    }

    public Task<bool> ReferenceExistsAsync(string referenceNo, int? excludedId = null)
    {
        var normalizedReference = referenceNo.Trim();
        return _dbContext.AmlRecords.AsNoTracking().AnyAsync(record =>
            record.ClientId == _currentUser.ClientId &&
            record.ReferenceNo == normalizedReference &&
            (!excludedId.HasValue || record.Id != excludedId.Value));
    }
    public async Task<IReadOnlyList<AmlRecord>> GetAllAsync()
    {
        return await ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking()).OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id).ToListAsync();
    }


    public async Task<AmlRecord?> GetByIdAsync(int id)
    {
        return await _dbContext.AmlRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> CreateAsync(AmlRecord record)
    {
        record.ClientId = _currentUser.ClientId;
        record.OwnerUserId = _currentUser.UserId;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.AmlRecords.Add(record);
            await _dbContext.SaveChangesAsync();
            AddAudit("Create", record.Id, $"Created AML record {record.ReferenceNo}.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return record.Id;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> UpdateAsync(AmlRecord record, string updateby)
    {
        if (!CanAccessRecord(record))
            return false;

        record.UpdatedDate = DateTime.UtcNow;
        record.UpdatedBy = updateby;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.AmlRecords.Update(record);
            _dbContext.Entry(record).Property(x => x.RowVersion).OriginalValue = record.RowVersion;
            var updated = await _dbContext.SaveChangesAsync() > 0;
            if (!updated)
            {
                await transaction.RollbackAsync();
                return false;
            }

            AddAudit("Update", record.Id, $"Updated AML record {record.ReferenceNo}.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return false;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }


    }
    public async Task<bool> DeleteAsync(int id, string deletedBy)
    {
        var record = await ApplyOwnershipScope(_dbContext.AmlRecords)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (record == null) return false;

        record.DeleteFg = "Y";
        record.DeleteDate = DateTime.UtcNow;
        record.DeleteBy = deletedBy;
        record.IsActive = false;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var deleted = await _dbContext.SaveChangesAsync() > 0;
            if (!deleted)
            {
                await transaction.RollbackAsync();
                return false;
            }

            AddAudit("Delete", record.Id, $"Soft deleted AML record {record.ReferenceNo}.");
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    private IQueryable<AmlRecord> ApplyOwnershipScope(IQueryable<AmlRecord> query)
    {
        if (_currentUser.HasPermission(AppPermissions.AmlRecordsViewAll) ||
            _currentUser.HasPermission(AppPermissions.AmlRecordsViewClient))
        {
            return query;
        }

        return query.Where(record => record.OwnerUserId == _currentUser.UserId);
    }

    private bool CanAccessRecord(AmlRecord record) =>
        _currentUser.HasPermission(AppPermissions.AmlRecordsViewAll) ||
        (record.ClientId == _currentUser.ClientId &&
         (_currentUser.HasPermission(AppPermissions.AmlRecordsViewClient) ||
          record.OwnerUserId == _currentUser.UserId));

    private void AddAudit(string action, int entityId, string details)
    {
        _dbContext.AuditLogs.Add(new AuditLog
        {
            ClientId = _currentUser.ClientId,
            UserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            EntityType = "AmlRecord",
            EntityId = entityId.ToString(),
            Details = details,
            CreatedDate = DateTime.UtcNow
        });
    }
}
