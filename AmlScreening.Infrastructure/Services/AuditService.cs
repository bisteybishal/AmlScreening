using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Services;

public sealed class AuditService : IAuditService
{
    private readonly IDBConnectionFactory _connectionFactory;
    private readonly ICurrentUserContext _currentUser;

    public AuditService(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser)
    {
        _connectionFactory = connectionFactory;
        _currentUser = currentUser;
    }

    public async Task WriteAsync(
        AuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
          "dbo.usp_AuditLog",
          new
          {
              _currentUser.PartnerId,
              _currentUser.UserId,
              _currentUser.Username,
              auditEvent.Action,
              auditEvent.EntityType,
              auditEvent.EntityId,
              Details = Truncate(auditEvent.Details, 2000)
          },
          commandType: CommandType.StoredProcedure,
          cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditLogItem>> GetRecentAsync(
        int maximumRecords = 200,
        CancellationToken cancellationToken = default)
    {
        

        using var connection = _connectionFactory.CreateConnection();
       
        var records = await connection.QueryAsync<AuditLogItem>(new CommandDefinition(
             "dbo.usp_GetRecentAuditLogs",
            new
            {
                MaximumRecords = Math.Clamp(maximumRecords, 1, 1000),
                _currentUser.PartnerId,
                CanViewAll = _currentUser.IsAdmin

            },
             commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return records.AsList();
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
