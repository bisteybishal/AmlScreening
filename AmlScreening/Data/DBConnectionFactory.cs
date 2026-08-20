using System.Data;
using System.Data.SqlClient;

namespace AmlScreening.Portal.Data
{
    public class DBConnectionFactory
    {
        private readonly IConfiguration _configuration;
        public DBConnectionFactory(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public IDbConnection CreateConnection()
        {
           var connectionString=_configuration.GetConnectionString("AMLDatabase");
            if(string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string 'AMLDatabase' not found.");
            }
                  return new SqlConnection(connectionString); 
        }
    }
}
