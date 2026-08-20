using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IDBConnectionFactory _dbConnectionFactory;

        public UserRepository(IDBConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }
        public async Task<User?> GetUserAsync(string username)
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var user =
                    await connection.QueryFirstOrDefaultAsync<User>(
                        "usp_User_GetForLogin",
                        new
                        {
                            Username = username
                        },
                        commandType: CommandType.StoredProcedure
                    );
            return user;
        }
    }
}
