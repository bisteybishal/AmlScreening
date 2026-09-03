using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Provider;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories
{
    public class AmlImportRepository : IAmlImportRepository
    {
        private readonly IDBConnectionFactory _dbConnectionFactory;
        private readonly ICurrentUserContext _currentUser;
        public AmlImportRepository(IDBConnectionFactory dbConnectionFactory, ICurrentUserContext currentUser)
        {
            _dbConnectionFactory = dbConnectionFactory;
            _currentUser = currentUser;
        }

        public async Task SaveAsync(IEnumerable<AmlRecord> records)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                connection.Open();
            }
            var transaction = connection.BeginTransaction();
            try
            {
                foreach (var record in records)
                {
                    await connection.ExecuteAsync("dbo.spa_AML_Record_BulkInsert",
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
                            ClientId=_currentUser.ClientId,
                            OwnerUserId= _currentUser.UserId,
                            CreatedBy = _currentUser.Username

                        },
                        transaction: transaction, commandType: CommandType.StoredProcedure);
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
}
