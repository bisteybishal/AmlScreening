using AmlScreening.Application.Common;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AmlScreening.Infrastructure.Repositories;


public sealed class AmlImportRepository
    : IAmlImportRepository
{
    private readonly AmlScreeningDbContext
        _db;

    private readonly ICurrentUserContext
        _currentUser;


    public AmlImportRepository(
        AmlScreeningDbContext db,
        ICurrentUserContext currentUser)
    {
        _db =
            db;

        _currentUser =
            currentUser;
    }


    // =========================================================
    // SAVE EXTERNAL AML SOURCE
    // =========================================================

    public async Task<int> SaveAsync(
        string sourceCode,
        IReadOnlyList<AmlRecord> records,
        CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        sourceCode =
            sourceCode?
                .Trim()
                .ToUpperInvariant()
            ?? string.Empty;


        // -----------------------------------------------------
        // SOURCE VALIDATION
        // -----------------------------------------------------

        if (sourceCode is not ("UN" or "EU"))
        {
            throw new ArgumentException(
                "Unsupported AML source.",
                nameof(sourceCode));
        }


        if (records == null)
        {
            throw new ArgumentNullException(
                nameof(records));
        }


        var now =
            DateTime.UtcNow;


        // -----------------------------------------------------
        // CLEAN INCOMING DATA
        // -----------------------------------------------------

        var incoming =
            records

                .Where(
                    record =>
                        !string.IsNullOrWhiteSpace(
                            record.FullName)

                        &&

                        !string.IsNullOrWhiteSpace(
                            record.ReferenceNo))

                .GroupBy(
                    record =>
                        record.ReferenceNo.Trim(),

                    StringComparer
                        .OrdinalIgnoreCase)

                .Select(
                    group =>
                        group.First())

                .ToList();


        if (incoming.Count == 0)
        {
            return 0;
        }


        // -----------------------------------------------------
        // GET EXISTING GLOBAL RECORDS
        // -----------------------------------------------------

        /*
         * IgnoreQueryFilters is intentional.
         *
         * Normal AML query:
         *
         * PartnerId = CurrentPartner
         * OR
         * PartnerId IS NULL
         *
         * But during synchronization we need
         * only the GLOBAL records belonging
         * to this source.
         */

        var existing =
            await _db
                .AmlRecords

                .IgnoreQueryFilters()

                .Where(
                    record =>
                        record.IsGlobal 

                        &&

                        record.SourceName ==
                            sourceCode

                        &&

                        record.DeleteFg ==
                            "N")

                .ToListAsync(
                    cancellationToken);


        // -----------------------------------------------------
        // EXISTING LOOKUP
        // -----------------------------------------------------

        var existingByReference =
            existing

                .GroupBy(
                    record =>
                        record.ReferenceNo,

                    StringComparer
                        .OrdinalIgnoreCase)

                .ToDictionary(
                    group =>
                        group.Key,

                    group =>
                        group.First(),

                    StringComparer
                        .OrdinalIgnoreCase);


        var incomingReferences =
            new HashSet<string>(
                StringComparer
                    .OrdinalIgnoreCase);


        // -----------------------------------------------------
        // INSERT / UPDATE
        // -----------------------------------------------------

        foreach (var sourceRecord in incoming)
        {
            cancellationToken
                .ThrowIfCancellationRequested();


            var referenceNo =
                sourceRecord
                    .ReferenceNo
                    .Trim();


            incomingReferences.Add(
                referenceNo);


            var fullName =
                sourceRecord
                    .FullName
                    .Trim();


            var normalizedName =
                AmlNameNormalizer
                    .Normalize(
                        fullName);


            var namePrefix =
                AmlNameNormalizer
                    .GetPrefix(
                        fullName);



            // =================================================
            // UPDATE EXISTING GLOBAL RECORD
            // =================================================

            if (existingByReference
                .TryGetValue(
                    referenceNo,
                    out var existingRecord))
            {
                existingRecord.RecordType =
                    NormalizeRequired(
                        sourceRecord.RecordType,
                        "UNKNOWN");


                existingRecord.FullName =
                    fullName;


                existingRecord.DateOfBirth =
                    sourceRecord.DateOfBirth;


                existingRecord.Country =
                    NullIfWhiteSpace(
                        sourceRecord.Country);


                existingRecord.Nationality =
                    NullIfWhiteSpace(
                        sourceRecord.Nationality);


                /*
                 * Always use the selected provider code.
                 *
                 * Do not trust SourceName coming
                 * from XML/provider mapper.
                 */

                existingRecord.SourceName =
                    sourceCode;


                existingRecord.Remarks =
                    NullIfWhiteSpace(
                        sourceRecord.Remarks);


                existingRecord.NormalizedName =
                    normalizedName;


                existingRecord.NamePrefix =
                    namePrefix;


                existingRecord.SourceUpdatedDate =
                    sourceRecord
                        .SourceUpdatedDate;


                existingRecord.LastSyncDate =
                    now;


                existingRecord.IsActive =
                    true;


                existingRecord.UpdatedDate =
                    now;


                existingRecord.UpdatedBy =
                    _currentUser.Username;


                continue;
            }


            // =================================================
            // INSERT NEW GLOBAL RECORD
            // =================================================

            var newRecord =
                new AmlRecord
                {
                    /*
                     * NULL means:
                     * global UN/EU record.
                     */

                    PartnerId =_currentUser.PartnerId,

                    BranchId =_currentUser.BranchId,
                       


                    /*
                     * This is only audit information.
                     *
                     * It does NOT make the record
                     * Partner-owned.
                     */

                    CreatedByUserId =
                        _currentUser.UserId,


                    RecordType =
                        NormalizeRequired(
                            sourceRecord.RecordType,
                            "UNKNOWN"),


                    FullName =
                        fullName,


                    DateOfBirth =
                        sourceRecord.DateOfBirth,


                    Country =
                        NullIfWhiteSpace(
                            sourceRecord.Country),


                    Nationality =
                        NullIfWhiteSpace(
                            sourceRecord.Nationality),


                    ReferenceNo =
                        referenceNo,


                    SourceName =
                        sourceCode,


                    Remarks =
                        NullIfWhiteSpace(
                            sourceRecord.Remarks),


                    NormalizedName =
                        normalizedName,


                    NamePrefix =
                        namePrefix,


                    LastSyncDate =
                        now,


                    SourceUpdatedDate =
                        sourceRecord
                            .SourceUpdatedDate,


                    IsActive =
                        true,


                    CreatedDate =
                        now,


                    CreatedBy =
                        _currentUser.Username,
                    IsGlobal=true,


                    DeleteFg =
                        "N"
                };


            _db.AmlRecords.Add(
                newRecord);
        }


        // -----------------------------------------------------
        // DEACTIVATE RECORDS REMOVED FROM SOURCE
        // -----------------------------------------------------

        /*
         * Example:
         *
         * yesterday UN had:
         *
         * REF001
         * REF002
         * REF003
         *
         * today:
         *
         * REF001
         * REF003
         *
         * REF002 is not physically deleted.
         * It becomes inactive.
         */

        foreach (var existingRecord in existing)
        {
            if (incomingReferences.Contains(
                    existingRecord.ReferenceNo))
            {
                continue;
            }


            existingRecord.IsActive =
                false;


            existingRecord.LastSyncDate =
                now;


            existingRecord.UpdatedDate =
                now;


            existingRecord.UpdatedBy =
                _currentUser.Username;
        }


        // -----------------------------------------------------
        // SAVE
        // -----------------------------------------------------

        await _db.SaveChangesAsync(
            cancellationToken);


        return incoming.Count;
    }


    // =========================================================
    // AUTHENTICATION
    // =========================================================

    private void EnsureSignedIn()
    {
        if (!_currentUser.IsAuthenticated
            ||
            _currentUser.UserId <= 0)
        {
            throw new UnauthorizedAccessException(
                "Login required.");
        }
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private static string? NullIfWhiteSpace(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)

            ? null

            : value.Trim();
    }


    private static string NormalizeRequired(
        string? value,
        string defaultValue)
    {
        return string.IsNullOrWhiteSpace(
                value)

            ? defaultValue

            : value.Trim();
    }
}