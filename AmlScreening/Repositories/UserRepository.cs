using AmlScreening.Portal.Data;
using AmlScreening.Portal.Models;
using Dapper;
using System.Data;

namespace AmlScreening.Portal.Repositories
{
    public class UserRepository
    {
        private readonly DBConnectionFactory _dbConnectionFactory;
        public UserRepository(DBConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }
        public async Task<UserModel?> GetUserForLoginAsync(string username)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();

            return await connection.QueryFirstOrDefaultAsync<UserModel>(
                "usp_User_GetForLogin",
                new
                {
                    Username = username
                },
                commandType: CommandType.StoredProcedure);
        }
        public async Task UpdateLastLoginAsync(long userid)
        {
            using var connection =
                _dbConnectionFactory.CreateConnection();
            await connection.ExecuteAsync(
                "usp_User_UpdateLastLogin",
                new
                {
                    UserId = userid
                },
                commandType: CommandType.StoredProcedure);
        }
    }
}
