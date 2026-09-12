using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories
{
    public class UserSessionRepository:IUserSessionRepository
    {
        private readonly IDBConnectionFactory _dBConnectionFactory;

        public UserSessionRepository(IDBConnectionFactory dBConnectionFactory)
        {
            _dBConnectionFactory = dBConnectionFactory;
        }
        public Task<UserSessionResult> StartAsync(long UserId, int clientId, Guid sessionId)
        {
            return ExecuteAsync(
           "START", UserId, clientId, sessionId);
        }
        public Task<UserSessionResult> ValidateAsync(long UserId, int clientId, Guid sessionId)
        {
            return ExecuteAsync(
           "VALIDATE", UserId, clientId, sessionId);
        }
        public Task<UserSessionResult> EndAsync(long UserId, int clientId, Guid sessionId)
        {
            return ExecuteAsync(
           "END", UserId, clientId, sessionId);
        }
        private async Task<UserSessionResult> ExecuteAsync(string action, long UserId, int clientId, Guid sessionId)
        {
            using var connection = _dBConnectionFactory.CreateConnection();
             var results=await connection.QuerySingleAsync<UserSessionResult>("dbo.spa_UserSession", new
            {
                Action = action,
                UserId=UserId,
                clientId=clientId,
                SessionId=sessionId

            },
            commandType:CommandType.StoredProcedure);    
            return results;
        }
    }
}
