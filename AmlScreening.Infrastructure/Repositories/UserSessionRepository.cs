using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;
using System.Data.Entity.Infrastructure;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class UserSessionRepository
    : IUserSessionRepository
{
    private readonly IDBConnectionFactory
        _connectionFactory;

    public UserSessionRepository(
        IDBConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }


    public async Task<int> StartAsync(
        Guid sessionId,
        long userId,
        int partnerId,
        int branchId,
        string ipAddress,
        string? userAgent)
    {
        using var connection =
            _connectionFactory.CreateConnection();

        return await connection
            .QuerySingleAsync<int>(
                "dbo.spa_UserSession",
                new
                {
                    Action = "START",

                    SessionId = sessionId,

                    UserId = userId,

                    PartnerId = partnerId,

                    BranchId = branchId,

                    IpAddress = ipAddress,

                    UserAgent = userAgent
                },
                commandType:
                    CommandType.StoredProcedure);
    }


    public async Task<bool> ValidateAsync(
        Guid sessionId,
        long userId,
        int partnerId,
        int branchId,
        string ipAddress)
    {
        using var connection =
            _connectionFactory.CreateConnection();

        var result =
            await connection
                .QuerySingleAsync<int>(
                    "dbo.spa_UserSession",
                    new
                    {
                        Action = "VALIDATE",

                        SessionId = sessionId,

                        UserId = userId,

                        PartnerId = partnerId,

                        BranchId = branchId,

                        IpAddress = ipAddress
                    },
                    commandType:
                        CommandType.StoredProcedure);

        return result == 1;
    }


    public async Task EndAsync(
        Guid sessionId,
        long userId)
    {
        using var connection =
            _connectionFactory.CreateConnection();

        await connection.ExecuteAsync(
            "dbo.spa_UserSession",
            new
            {
                Action = "END",

                SessionId = sessionId,

                UserId = userId
            },
            commandType:
                CommandType.StoredProcedure);
    }
}