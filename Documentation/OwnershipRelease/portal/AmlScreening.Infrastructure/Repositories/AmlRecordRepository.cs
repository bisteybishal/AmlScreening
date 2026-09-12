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
    public async Task<IReadOnlyList<AmlRecord>> FindMatchNameAsync(
        string name, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        if (!_currentUser.HasPermission(AppPermissions.Screening) &&
            !_currentUser.HasPermission(AppPermissions.AmlRecordsView))
            throw new UnauthorizedAccessException("Screening permission is required.");

        var term = name?.Trim() ?? string.Empty;
        if (term.Length == 0) return Array.Empty<AmlRecord>();
        if (term.Length > 200)
            throw new ArgumentException("A name cannot exceed 200 characters.", nameof(name));

        // Visibility is enforced by the DbContext SQL function, including source grants.
        // Let database failures reach the controller: a failed search is NOT a clear result.
        return await ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking())
            .Where(x => x.IsActive && x.FullName.Contains(term))
            .OrderBy(x => x.FullName).ThenBy(x => x.Id).Take(51)
            .Select(x => new AmlRecord
            {
                Id = x.Id, FullName = x.FullName, RecordType = x.RecordType,
                SourceName = x.SourceName, SourceId = x.SourceId, IsShared = x.IsShared,
                DateOfBirth = x.DateOfBirth, Country = x.Country,
                Nationality = x.Nationality, Remarks = x.Remarks,
                ReferenceNo = x.ReferenceNo, HasPhoto = x.Photo != null
            }).ToArrayAsync(cancellationToken);
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
        pageSize = Math.Clamp(pageSize, 1, 50);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);
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

    public async Task<AmlRecordSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var records = ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking());

        var summary = await records.GroupBy(_ => 1).Select(g => new AmlRecordSummary
        {
            TotalRecords = g.Count(),
            ActiveRecords = g.Count(x => x.IsActive),
            IndividualRecords = g.Count(x => x.RecordType == "Individual"),
            EntityRecords = g.Count(x => x.RecordType == "Entity"),
            CountriesCovered = g.Where(x => x.Country != null && x.Country != "").Select(x => x.Country).Distinct().Count()

        }).SingleOrDefaultAsync(cancellationToken);
        return summary ?? new AmlRecordSummary();

    }

    public async Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords)
    {
        var take = Math.Clamp(maximumRecords, 1, 50);
        return await ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking())
            .OrderByDescending(record => record.CreatedDate)
            .ThenByDescending(record => record.Id)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> ReferenceExistsAsync(
        string referenceNo,
        string sourceName,
        int? excludedId = null)
    {
        EnsureSignedIn();
        var normalizedReference = referenceNo.Trim();
        var normalizedSource = sourceName.Trim();
        var stored = excludedId.HasValue
            ? await _dbContext.AmlRecords.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == excludedId.Value)
            : null;
        if (excludedId.HasValue && stored is null)
            throw new UnauthorizedAccessException("The record is unavailable.");

        var clientId = stored?.ClientId ?? _currentUser.ClientId;
        var ownerId = stored?.OwnerUserId ?? _currentUser.UserId;
        var sourceId = stored?.SourceId;
        var isShared = stored?.IsShared ?? false;

        // Ignore only soft deletion; never disable the visibility/security filter.
        return await _dbContext.AmlRecords
            .IgnoreQueryFilters(["SoftDeleteFilter"]).AsNoTracking()
            .AnyAsync(record =>
                record.IsShared == isShared &&
                (isShared
                    ? record.SourceId == sourceId
                    : record.ClientId == clientId && record.OwnerUserId == ownerId &&
                      record.SourceName == normalizedSource) &&
                record.ReferenceNo == normalizedReference &&
                (!excludedId.HasValue || record.Id != excludedId.Value));
    }

    public async Task<AmlRecord?> GetByIdAsync(int id)
    {
        return await ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking())
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<int> CreateAsync(AmlRecord record)
    {
        EnsureSignedIn();
        if (record.Id != 0)
            throw new ArgumentException("A new AML record must not have an existing ID.", nameof(record));
        record.ClientId = _currentUser.ClientId;
        record.OwnerUserId = _currentUser.UserId;
        EnsurePermission(AppPermissions.AmlRecordsCreate);
        // Manual entries are always private, including entries created by an administrator.
        record.IsShared = false;
        record.SourceId = null;
        record.CreatedDate = DateTime.UtcNow;
        record.CreatedBy = _currentUser.Username;
        record.UpdatedDate = null;
        record.UpdatedBy = null;
        record.DeleteFg = "N";
        record.DeleteDate = null;
        record.DeleteBy = null;
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
        EnsureSignedIn();
        EnsurePermission(AppPermissions.AmlRecordsEdit);
        // Authorize the stored row, never ownership values supplied by a form.
        var existing = await ApplyOwnershipScope(_dbContext.AmlRecords)
            .FirstOrDefaultAsync(x => x.Id == record.Id);
        if (existing == null || !CanAccessRecord(existing) || record.RowVersion is not { Length: 8 })
            return false;

        // Only business fields are editable. Ownership and creation details stay unchanged.
        existing.RecordType = record.RecordType;
        existing.FullName = record.FullName;
        existing.DateOfBirth = record.DateOfBirth;
        existing.Country = record.Country;
        existing.Nationality = record.Nationality;
        existing.ReferenceNo = record.ReferenceNo;
        existing.SourceName = record.SourceName;
        existing.Remarks = record.Remarks;
        existing.IsActive = record.IsActive;
        existing.UpdatedDate = DateTime.UtcNow;
        existing.UpdatedBy = _currentUser.Username;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            _dbContext.Entry(existing).Property(x => x.RowVersion).OriginalValue = record.RowVersion;
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
        EnsureSignedIn();
        EnsurePermission(AppPermissions.AmlRecordsDelete);
        var record = await ApplyOwnershipScope(_dbContext.AmlRecords)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (record == null || !CanAccessRecord(record)) return false;

        record.DeleteFg = "Y";
        record.DeleteDate = DateTime.UtcNow;
        record.DeleteBy = _currentUser.Username;
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
    public Task<AmlRecord?> GetPhotoAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        if (!_currentUser.HasPermission(AppPermissions.Screening) &&
            !_currentUser.HasPermission(AppPermissions.AmlRecordsView))
            throw new UnauthorizedAccessException("Screening permission is required.");
        return ApplyOwnershipScope(_dbContext.AmlRecords.AsNoTracking())
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new AmlRecord { Id = x.Id, Photo = x.Photo, PhotoContentType = x.PhotoContentType })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<AmlRecord> ApplyOwnershipScope(IQueryable<AmlRecord> query)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.ClientId <= 0 || _currentUser.UserId <= 0)
            return query.Where(record => false);

        // The global VisibilityFilter applies the shared database rule.
        // AMLRecords.ViewClient is deliberately NOT an ownership bypass.
        return query;
    }

    private bool IsSystemAdministrator =>
        _currentUser.IsAdministrator &&
        _currentUser.HasPermission(AppPermissions.AmlRecordsViewAll);

    private bool CanAccessRecord(AmlRecord record) =>
        IsSystemAdministrator ||
        (!record.IsShared && record.ClientId == _currentUser.ClientId &&
         record.OwnerUserId == _currentUser.UserId);

    private void EnsurePermission(string permission)
    {
        if (!_currentUser.HasPermission(permission))
            throw new UnauthorizedAccessException("The requested operation is not permitted.");
    }

    private void EnsureSignedIn()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.ClientId <= 0 || _currentUser.UserId <= 0)
            throw new UnauthorizedAccessException("A signed-in user with a client is required.");
    }

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
