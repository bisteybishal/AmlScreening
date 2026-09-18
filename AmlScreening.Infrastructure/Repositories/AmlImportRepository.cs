using AmlScreening.Application.Common;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;


public sealed class AmlImportRepository
    : IAmlImportRepository
{
    private readonly IDBConnectionFactory
        _dbConnectionFactory;

    private readonly ICurrentUserContext
        _currentUser;


    public AmlImportRepository(
        IDBConnectionFactory dbConnectionFactory,
        ICurrentUserContext currentUser)
    {
        _dbConnectionFactory =
            dbConnectionFactory;

        _currentUser =
            currentUser;
    }


    public async Task SaveAsync(
        IEnumerable<AmlRecord> records)
    {
        // Defense in depth:
        // imports are platform operations.
        if (_currentUser.ClientId <= 0 || _currentUser.UserId <= 0 || !_currentUser.IsAuthenticated
            ||
            !_currentUser.HasPermission(
                AppPermissions.AmlSourceSync)
            ||
            !_currentUser.HasPermission(
                AppPermissions.PlatformCrossClientAccess))
        {
            throw new UnauthorizedAccessException(
                "You are not authorized to import AML source data.");
        }


        using var connection =
            _dbConnectionFactory
                .CreateConnection();


        if (connection.State !=
            ConnectionState.Open)
        {
            connection.Open();
        }


        using var transaction =
            connection
                .BeginTransaction();


        try
        {
            foreach (var record
                in records)
            {
                var normalizedName =
                    AmlNameNormalizer.Normalize(
                        record.FullName);


                var namePrefix =
                    AmlNameNormalizer.GetPrefix(
                        record.FullName);


                await connection.ExecuteAsync(
                    // SQL validates GLOBAL scope and preserves existing record ownership.
                    "dbo.spa_AML_Record_BulkInsert",
                    new
                    {
                        record.RecordType,
                        record.FullName,
                        record.SourceName,
                        record.DateOfBirth,
                        record.Nationality,
                        record.Remarks,
                        record.ReferenceNo,
                        record.Country,

                        ClientId =
                            _currentUser.ClientId,

                        OwnerUserId =
                            _currentUser.UserId,

                        IsShared =
                            true,

                        RecordScope =
                            "GLOBAL",

                        NormalizedName =
                            normalizedName,

                        NamePrefix =
                            namePrefix,

                        CreatedBy =
                            _currentUser.Username
                    },
                    transaction:
                        transaction,
                    commandType:
                        CommandType.StoredProcedure);
            }


            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
