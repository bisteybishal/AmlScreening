using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Client;
using Microsoft.AspNetCore.Mvc;
using AmlScreening.Application.Common;

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
    public async Task<IReadOnlyList<AmlRecord>> FindMatchNameAsync(string name, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Array.Empty<AmlRecord>();
        }
        var normalizedName = AmlNameNormalizer.Normalize(name);
        if (normalizedName.Length == 0)
        {
            return Array.Empty<AmlRecord>();
        }

        if (normalizedName.Length > 250)
            throw new ArgumentException("Search names must not exceed 250 characters.", nameof(name));


        var originalName = name.Trim();
        var query = PartnerQuery(tracking: false);

        return await query
            .Where(
                x =>
                    x.IsActive
                    &&
                    ((x.NormalizedName != null && x.NormalizedName.Contains(normalizedName)) ||
                     (x.NormalizedName == null && x.FullName.Contains(originalName))))
            .OrderBy(
                x => x.FullName)
            .ThenBy(
                x => x.Id)
            .Take(50)
            .Select(
                x =>
                    new AmlRecord
                    {
                        Id =
                            x.Id,
                        PartnerId = x.PartnerId,
                        BranchId = x.BranchId,
                        CreatedByUserId = x.CreatedByUserId,

                        FullName =
                            x.FullName,

                        RecordType =
                            x.RecordType,

                        SourceName =
                            x.SourceName,

                        DateOfBirth =
                            x.DateOfBirth,

                        Country =
                            x.Country,

                        Nationality =
                            x.Nationality,

                        Remarks =
                            x.Remarks,

                        ReferenceNo =
                            x.ReferenceNo,

                        HasPhoto =
                            x.Photo != null
                    })
            .ToArrayAsync(
                cancellationToken);
    }

    public async Task<PagedResult<AmlRecord>> GetPageAsync(
        string? search,
        string? recordType,
        bool? isActive,
        int pageNumber,
        int pageSize, CancellationToken cancellationToken = default)
    {

        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = PartnerQuery(tracking: false);
        // Search
        if (!string.IsNullOrWhiteSpace(
                search))
        {
            var term =
                search.Trim();

            query =
                query.Where(x =>
                    x.FullName.Contains(term)
                    ||
                    x.ReferenceNo.Contains(term)
                    ||
                    x.SourceName.Contains(term));
        }
        // Record type
        if (!string.IsNullOrWhiteSpace(recordType))
        {
            var type = recordType.Trim();
            query = query.Where(x => x.RecordType == type);
        }
        pageSize = Math.Clamp(pageSize, 1, 50);
        // Active / inactive
        if (isActive.HasValue)
            query = query.Where(record => record.IsActive == isActive.Value);

        var totalRecords = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        pageNumber = Math.Clamp(pageNumber, 1, totalPages);
        var records = await query.OrderByDescending(r => r.CreatedDate)
            .ThenByDescending(r => r.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

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
        EnsureSignedIn();
        var query =
            PartnerQuery(
                tracking: false);

        var summary = await query.GroupBy(x => 1).Select(g => new AmlRecordSummary
        {
            TotalRecords = g.Count(),
            ActiveRecords = g.Count(x => x.IsActive),
            IndividualRecords = g.Count(x => x.RecordType == "Individual"),
            EntityRecords = g.Count(x => x.RecordType == "Entity"),
            CountriesCovered = g.Where(x => x.Country != null && x.Country != "").Select(x => x.Country).Distinct().Count()

        }).SingleOrDefaultAsync(cancellationToken);
        return summary ?? new AmlRecordSummary();

    }

    public async Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        var take = Math.Clamp(maximumRecords, 1, 50);
        return await PartnerQuery(tracking: false)
            .OrderByDescending(record => record.CreatedDate)
            .ThenByDescending(record => record.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ReferenceExistsAsync(
        string referenceNo,
        string sourceName,
        int? excludedId = null, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        var reference = referenceNo.Trim();
        var source = sourceName.Trim();
        // The unique reference index also includes soft-deleted records.
        return _dbContext.AmlRecords.AsNoTracking()
            .AnyAsync(x => x.PartnerId == _currentUser.PartnerId
            && x.ReferenceNo == reference && x.SourceName == source
             &&
                    (
                        !excludedId.HasValue

                        ||

                        x.Id !=
                        excludedId.Value
                    ),
                cancellationToken);
    }
    public async Task<AmlRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        return await PartnerQuery(tracking: false).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);


    }

    public async Task<int> CreateAsync(AmlRecord record, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();

        if (record.Id != 0)
        {
            throw new ArgumentException(
                "A new AML record must not have an existing ID.",
                nameof(record));
        }
        record.PartnerId =
           _currentUser.PartnerId;

        record.BranchId =
            _currentUser.BranchId;

        record.CreatedByUserId =
            _currentUser.UserId;


        record.CreatedBy =
            _currentUser.Username;

        record.CreatedDate =
            DateTime.UtcNow;


        record.UpdatedBy =
            null;

        record.UpdatedDate =
            null;


        record.DeleteFg =
            "N";

        record.DeleteBy =
            null;

        record.DeleteDate =
            null;
        // Prepare data for future screening.
        record.NormalizedName =
            AmlNameNormalizer
                .Normalize(
                    record.FullName);

        record.NamePrefix =
            AmlNameNormalizer
                .GetPrefix(
                    record.FullName);


        if (string.IsNullOrWhiteSpace(
                record.NormalizedName))
        {
            throw new ArgumentException(
                "AML record name is required.");
        }
        _dbContext.AmlRecords
           .Add(record);


        await _dbContext
            .SaveChangesAsync(
                cancellationToken);


        return record.Id;


    }

    public async Task<bool> UpdateAsync(AmlRecord record, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var existing = await _dbContext.AmlRecords.FirstOrDefaultAsync(x => x.Id == record.Id && x.PartnerId == _currentUser.PartnerId , cancellationToken);
        if (existing == null)
        {
            return false;
        }

        if (record.RowVersion is
            { Length: > 0 })
        {                                                       
            _dbContext
                .Entry(existing)

                .Property(
                    x => x.RowVersion)

                .OriginalValue =
                    record.RowVersion;
        }


        existing.RecordType =
            record.RecordType;

        existing.FullName =
            record.FullName;

        existing.DateOfBirth =
            record.DateOfBirth;

        existing.Country =
            record.Country;

        existing.Nationality =
            record.Nationality;

        existing.ReferenceNo =
            record.ReferenceNo;

        existing.SourceName =
            record.SourceName;

        existing.Remarks =
            record.Remarks;

        existing.IsActive =
            record.IsActive;


        existing.NormalizedName =
            AmlNameNormalizer
                .Normalize(
                    record.FullName);

        existing.NamePrefix =
            AmlNameNormalizer
                .GetPrefix(
                    record.FullName);


        existing.UpdatedDate =
            DateTime.UtcNow;

        existing.UpdatedBy =
            _currentUser.Username;


        try
        {
            return await _dbContext
                .SaveChangesAsync(
                    cancellationToken) > 0;
        }
        catch (
            DbUpdateConcurrencyException)
        {
            return false;
        }

    }
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureCanManage();


        var record =
            await _dbContext
                .AmlRecords

                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id

                        &&

                        x.PartnerId ==
                        _currentUser.PartnerId

                        &&

                        x.DeleteFg != "Y",
                    cancellationToken);


        if (record == null)
        {
            return false;
        }

        record.DeleteFg =
            "Y";

        record.DeleteDate =
            DateTime.UtcNow;

        record.DeleteBy =
            _currentUser.Username;

        record.IsActive =
            false;


        return await _dbContext
            .SaveChangesAsync(
                cancellationToken) > 0;
    }

    public async Task<AmlRecord?> GetPhotoAsync(int id, CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();
        return await PartnerQuery(tracking: false).Where(x => x.Id == id && x.IsActive)
            .Select(x => new AmlRecord
            {
                Id = x.Id,
                Photo = x.Photo,
                PhotoContentType = x.PhotoContentType,

            }).FirstOrDefaultAsync(cancellationToken);
    }

    private IQueryable<AmlRecord> PartnerQuery(bool tracking)
    {
        EnsureSignedIn();


        IQueryable<AmlRecord> query =
            tracking
                ? _dbContext.AmlRecords
                : _dbContext
                    .AmlRecords
                    .AsNoTracking();


        /*
         * THIS IS YOUR MAIN DATA RULE:
         *
         * All users belonging to the
         * same PartnerId see the same AML records.
         */

        return query.Where(x =>
            x.PartnerId ==
            _currentUser.PartnerId

            &&

            x.DeleteFg != "Y");
    }
    private void EnsureSignedIn()
    {
        if (!_currentUser.IsAuthenticated

            ||

            _currentUser.UserId <= 0

            ||

            _currentUser.PartnerId <= 0

            ||

            _currentUser.BranchId <= 0)
        {
            throw new UnauthorizedAccessException(
                "A valid login is required.");
        }
    }

    private void EnsureCanManage()
    {
        EnsureSignedIn();


        if (!_currentUser.HasPermission(
                AppPermissions.Aml.Manage))
        {
            throw new UnauthorizedAccessException(
                "AML management permission is required.");
        }
    }
}
