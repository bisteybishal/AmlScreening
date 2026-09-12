using Microsoft.Data.SqlClient;
using System.Data;

namespace AmlScreening.Infrastructure.Persistence
{
    public  interface IDBConnectionFactory
    {
        SqlConnection CreateConnection();
    }
}
