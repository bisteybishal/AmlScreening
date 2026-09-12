using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScreeningApi.Infrastructure.Persistence
{
    public class DBConnectionFactory:IDBConnectionFactory
    {
        private string _dbConnectionString;
        public DBConnectionFactory(IConfiguration configuration)
        {
            _dbConnectionString =configuration.GetConnectionString("AMLDatabase")?? throw new InvalidOperationException("Connection string 'AMLDatabase' not found in configuration.");
        }
        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_dbConnectionString);
        }
    }
}
