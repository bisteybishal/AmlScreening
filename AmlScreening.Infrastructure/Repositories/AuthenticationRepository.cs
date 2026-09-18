using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace AmlScreening.Infrastructure.Repositories
{
    public sealed class AuthenticationRepository : IAuthenticationRepository
    {
        private readonly IDBConnectionFactory _dBConnection;

        public AuthenticationRepository(IDBConnectionFactory dBConnection)
        {
            _dBConnection = dBConnection;
        }
        public async Task<loginuserData> GetByUsernameAsync(string username)
        {
            using var connection = _dBConnection.CreateConnection();
            using var multi = await connection.QueryMultipleAsync("dbo.spa_Auth_Getloginuser", new
            {
                username = username
            }, commandType: CommandType.StoredProcedure);
            var user = await multi.ReadSingleOrDefaultAsync<loginuserData>();
            if (user == null)
            {
                return null;

            }
            user.Roles = (await multi.ReadAsync<string>()).ToList();
            user.Permissions = (await multi.ReadAsync<string>()).ToList();
            return user;
        }
        public async Task RecordFailedLoginAsync(
        long userId)
        {
            using var connection =
                _dBConnection.CreateConnection();

            await connection.ExecuteAsync(
                "dbo.spa_User_LoginFailed",
                new
                {
                    UserId = userId
                },
                commandType:
                    CommandType.StoredProcedure);
        }
    }
}
