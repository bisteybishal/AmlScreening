using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AmlScreening.Infrastructure.Repositories
{
    public class AmlImportRepository : IAmlImportRepository
    {
        private readonly IDBConnectionFactory _dbConnectionFactory;

        public AmlImportRepository(IDBConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
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
                foreach(var record in records)
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
                            record.Country
                        },
                        transaction:transaction,commandType: CommandType.StoredProcedure);
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
