using Microsoft.Extensions.Configuration;
using System.Data;
using System.Data.SqlClient;

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
                    "DefaultConnection was not found.");
        }
        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
