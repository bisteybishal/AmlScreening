using System.Data;

namespace AmlScreening.Infrastructure.Persistence
{
    public  interface IDBConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}
