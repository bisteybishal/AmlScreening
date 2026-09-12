using Microsoft.Data.SqlClient;

namespace ScreeningApi.Infrastructure.Persistence
{
    public  interface IDBConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
