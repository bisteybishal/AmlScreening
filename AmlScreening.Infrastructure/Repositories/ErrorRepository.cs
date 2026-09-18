using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;
using System.Data.Entity.Infrastructure;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class ErrorLogRepository
    : IErrorLogRepository
{
    private readonly IDBConnectionFactory
        _connectionFactory;

    public ErrorLogRepository(
        IDBConnectionFactory connectionFactory)
    {
        _connectionFactory =
            connectionFactory;
    }

    public async Task<long> LogAsync(
        ErrorLogRequest request)
    {
        using var connection =
            _connectionFactory.CreateConnection();

        return await connection.QuerySingleAsync<long>(
            "dbo.spa_ErrorLog",
            new
            {
                request.ErrorDesc,
                request.PartnerCode,
                request.ErrorNumber,
                request.Script,
                request.ErrorScript,
                request.QueryString,
                request.IpAddress,
                request.UserId,
                request.Username
            },
            commandType:
                CommandType.StoredProcedure);
    }
}