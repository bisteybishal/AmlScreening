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
        return await PartnerQuery(tracking: false)
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
            .Take(51)
            .Select(
                x =>
                    new AmlRecord
                    {
                        Id = x.Id,
                        PartnerId = x.PartnerId,
                        BranchId = x.BranchId,
                        CreatedByUserId = x.CreatedByUserId,
                        IsGlobal = x.IsGlobal,
                        IsActive = x.IsActive,
                        FullName = x.FullName,
                        RecordType = x.RecordType,
                        SourceName = x.SourceName,
                        DateOfBirth = x.DateOfBirth,
                        Country = x.Country,
                        Nationality = x.Nationality,
                        Remarks = x.Remarks,
                        ReferenceNo = x.ReferenceNo,
                        HasPhoto = x.Photo != null
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
        // Active / inactive
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }


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
        var query = await PartnerQuery(tracking: false)
        .GroupBy(x => 1)
        .Select(g => new AmlRecordSummary
        {
            TotalRecords = g.Count(),
            ActiveRecords = g.Count(x => x.IsActive),
            IndividualRecords = g.Count(x => x.RecordType == "Individual"),
            EntityRecords = g.Count(x => x.RecordType == "Entity"),
            CountriesCovered = g.Where(x => x.Country != null && x.Country != "").Select(x => x.Country).Distinct().Count()

        }).SingleOrDefaultAsync(cancellationToken);
        return query ?? new AmlRecordSummary();

    }

    public async Task<IReadOnlyList<AmlRecord>> GetRecentAsync(int maximumRecords, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(maximumRecords, 1, 50);
        return await PartnerQuery(tracking: false)
            .OrderByDescending(record => record.CreatedDate)
            .ThenByDescending(record => record.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReferenceExistsAsync(
     string referenceNo,
     string sourceName,
     int? excludedId = null,
     CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();

        var reference = referenceNo.Trim();
        var source = sourceName.Trim();

        // New admin records are global.
        // New ordinary-user records are private.
        var isGlobal = _currentUser.IsAdmin;
        var partnerId = _currentUser.PartnerId;

        // Editing keeps the existing record's scope.
        if (excludedId.HasValue)
        {
            var existing = await PartnerQuery(tracking: false)
                .Where(x => x.Id == excludedId.Value)
                .Select(x => new
                {
                    x.IsGlobal,
                    x.PartnerId
                })
                .SingleOrDefaultAsync(cancellationToken);

            if (existing == null)
            {
                throw new ArgumentException(
                    "The AML record was not found.");
            }

            isGlobal = existing.IsGlobal;
            partnerId = existing.PartnerId;
        }

        return await PartnerQuery(tracking: false)
            .AnyAsync(
                x => x.IsGlobal == isGlobal
                    && (isGlobal || x.PartnerId == partnerId)
                    && x.ReferenceNo == reference
                    && x.SourceName == source
                    && (!excludedId.HasValue
                        || x.Id != excludedId.Value),
                cancellationToken);
    }
    public async Task<AmlRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await PartnerQuery(tracking: false).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);


    }

    public async Task<int> CreateAsync(
        AmlRecord record,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        ArgumentNullException.ThrowIfNull(record);

        if (record.Id != 0)
        {
            throw new ArgumentException(
                "A new AML record must not have an existing ID.",
                nameof(record));
        }

        record.FullName = record.FullName?.Trim() ?? string.Empty;
        record.ReferenceNo = record.ReferenceNo?.Trim() ?? string.Empty;
        record.SourceName = record.SourceName?.Trim() ?? string.Empty;

        record.NormalizedName =
            AmlNameNormalizer.Normalize(record.FullName);

        record.NamePrefix =
            AmlNameNormalizer.GetPrefix(record.FullName);

        if (string.IsNullOrWhiteSpace(record.NormalizedName))
        {
            throw new ArgumentException("AML record name is required.");
        }

        if (string.IsNullOrWhiteSpace(record.ReferenceNo) ||
            string.IsNullOrWhiteSpace(record.SourceName))
        {
            throw new ArgumentException(
                "Source and reference number are required.");
        }

        // Ownership comes from the authenticated user.
        record.PartnerId = _currentUser.PartnerId;
        record.BranchId = _currentUser.BranchId;
        record.CreatedByUserId = _currentUser.UserId;

        record.IsGlobal = _currentUser.IsAdmin;

        // This method handles ordinary manual partner records.
        // Global feed records are created by AmlImportRepository.

        record.CreatedBy = _currentUser.Username;
        record.CreatedDate = DateTime.UtcNow;
        record.UpdatedBy = null;
        record.UpdatedDate = null;
        record.DeleteFg = "N";
        record.DeleteBy = null;
        record.DeleteDate = null;

        _dbContext.AmlRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return record.Id;
    }

    public async Task<bool> UpdateAsync(
        AmlRecord record,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();
        ArgumentNullException.ThrowIfNull(record);

        // Being allowed to view a global record does not grant permission
        // to edit it through the ordinary manual-record form.
        var existing = await PrivatePartnerRecords(tracking: true)
            .FirstOrDefaultAsync(
                x => x.Id == record.Id,
                cancellationToken);

        if (existing == null)
        {
            return false;
        }

        if (record.RowVersion is not { Length: 8 })
        {
            throw new ArgumentException(
                "The record version is missing or invalid. Reload the page.");
        }

        var fullName = record.FullName?.Trim() ?? string.Empty;
        var normalizedName = AmlNameNormalizer.Normalize(fullName);

        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ArgumentException("AML record name is required.");
        }

        _dbContext.Entry(existing)
            .Property(x => x.RowVersion)
            .OriginalValue = record.RowVersion;

        existing.RecordType = record.RecordType;
        existing.FullName = fullName;
        existing.DateOfBirth = record.DateOfBirth;
        existing.Gender = record.Gender;
        existing.Country = record.Country;
        existing.Nationality = record.Nationality;
        existing.ReferenceNo = record.ReferenceNo.Trim();
        existing.SourceName = record.SourceName.Trim();
        existing.Remarks = record.Remarks;
        existing.IsActive = record.IsActive;
        existing.NormalizedName = normalizedName;
        existing.NamePrefix = AmlNameNormalizer.GetPrefix(fullName);
        existing.UpdatedDate = DateTime.UtcNow;
        existing.UpdatedBy = _currentUser.Username;
        existing.Aliases = record.Aliases;
        existing.Occupation = record.Occupation;
        existing.Position = record.Position;
        existing.PoliticalParties = record.PoliticalParties;
        existing.SocialMediaLinks = record.SocialMediaLinks;
        existing.Description = record.Description;

        // Do not change PartnerId, BranchId, CreatedByUserId or IsGlobal.

        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage();

        var record = await PrivatePartnerRecords(tracking: true)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (record == null)
        {
            return false;
        }

        record.DeleteFg = "Y";
        record.DeleteDate = DateTime.UtcNow;
        record.DeleteBy = _currentUser.Username;
        record.IsActive = false;

        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken) > 0;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public Task<AmlRecord?> GetPhotoAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        return PartnerQuery(tracking: false)
            .Where(x => x.Id == id && x.IsActive)
            .Select(x => new AmlRecord
            {
                Id = x.Id,
                PartnerId = x.PartnerId,
                BranchId = x.BranchId,
                IsGlobal = x.IsGlobal,
                Photo = x.Photo,
                PhotoContentType = x.PhotoContentType
            })
            .FirstOrDefaultAsync(cancellationToken);
    }



    public async Task<IReadOnlyList<AmlRecord>> FindKycCandidatesAsync(
    string name,
    string screeningType,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(screeningType))
        {
            return [];
        }
        var normalizeName = AmlNameNormalizer.Normalize(name);
        if (string.IsNullOrWhiteSpace(
        normalizeName))
        {
            return [];
        }

        if (normalizeName.Length > 250)
        {
            throw new ArgumentException(
                "Search name is too long.");
        }
        var source = screeningType.Trim();
        var originalName = name.Trim();
        return await PartnerQuery(false)
            // ScreeningType is only for filtering.
            .Where(x => x.IsActive && x.SourceName == source)
              // Find possible name candidates.
              .Where(x => (x.NormalizedName != null && x.NormalizedName.Contains(normalizeName)) || (x.Aliases != null && x.Aliases.Contains(originalName)))

            .OrderBy(x => x.FullName)
            .ThenBy(x => x.Id)
            .Take(51)
            .Select(x => new AmlRecord
            {
                Id = x.Id,

                FullName = x.FullName,

                Aliases = x.Aliases,

                DateOfBirth = x.DateOfBirth,

                Country = x.Country,

                Gender = x.Gender,

                SourceName = x.SourceName,

                ReferenceNo = x.ReferenceNo
            }).ToListAsync(cancellationToken);
    }
    private IQueryable<AmlRecord> PartnerQuery(bool tracking)
    {
        EnsureSignedIn();


        IQueryable<AmlRecord> query =
            tracking
                ? _dbContext.AmlRecords
                : _dbContext.AmlRecords.AsNoTracking();
        var isAdmin = _currentUser.IsAdmin;
        var partnerId = _currentUser.PartnerId;

        return query.Where(x =>
                    x.DeleteFg == "N" &&
                    (
                    isAdmin ||
                        x.IsGlobal ||
                        x.PartnerId == _currentUser.PartnerId
                    ));
    }

    private IQueryable<AmlRecord> PrivatePartnerRecords(bool tracking)
    {
        EnsureSignedIn();

        IQueryable<AmlRecord> query = tracking
            ? _dbContext.AmlRecords
            : _dbContext.AmlRecords.AsNoTracking();

        return query.Where(x =>
            x.DeleteFg == "N" &&
            !x.IsGlobal &&
            x.PartnerId == _currentUser.PartnerId);
    }
    private void EnsureSignedIn()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId <= 0 || _currentUser.PartnerId <= 0 || _currentUser.BranchId <= 0)
        {
            throw new UnauthorizedAccessException(
                "A valid login is required.");
        }
    }

    private void EnsureCanManage()
    {
        EnsureSignedIn();


        if (!_currentUser.HasPermission(AppPermissions.Aml.Manage))
        {
            throw new UnauthorizedAccessException(
                "AML management permission is required.");
        }
    }


}
