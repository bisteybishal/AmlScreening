using Microsoft.Extensions.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;

namespace AmlScreening.Infrastructure.Persistence
{
    public  class DBConnectionFactory : IDBConnectionFactory
    {
        private readonly string _connectionString;
        public DBConnectionFactory(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("AMLDatabase")
                ?? throw new InvalidOperationException(
                    "The Screening connection string was not found.");
        }
        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
