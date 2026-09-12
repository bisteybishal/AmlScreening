using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using AmlScreening.Application.Security;

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
        const string sql = """
            INSERT INTO dbo.AuditLogs
                (ClientId, UserId, Username, Action, EntityType, EntityId, Details, CreatedDate)
            VALUES
                (@ClientId, @UserId, @Username, @Action, @EntityType, @EntityId, @Details, SYSUTCDATETIME());
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                ClientId = _currentUser.ClientId,
                UserId = _currentUser.UserId,
                Username = _currentUser.Username,
                auditEvent.Action,
                auditEvent.EntityType,
                auditEvent.EntityId,
                Details = Truncate(auditEvent.Details, 2000)
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditLogItem>> GetRecentAsync(
        int maximumRecords = 200,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT TOP (@MaximumRecords)
                   AuditLogId, ClientId, UserId, Username, Action,
                   EntityType, EntityId, Details, CreatedDate
            FROM dbo.AuditLogs
            WHERE dbo.fn_HasPermission(@UserId,N'AuditLogs.View')=1
              AND (dbo.fn_IsSystemAdmin(@UserId)=1 OR (ClientId=@ClientId AND UserId=@UserId))
            ORDER BY CreatedDate DESC, AuditLogId DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var records = await connection.QueryAsync<AuditLogItem>(new CommandDefinition(
            sql,
            new
            {
                MaximumRecords = Math.Clamp(maximumRecords, 1, 1000),
                ClientId = _currentUser.ClientId,
                UserId = _currentUser.IsAuthenticated ? _currentUser.UserId : 0
            },
            cancellationToken: cancellationToken));
        return records.AsList();
    }

    private static string? Truncate(string? value, int maximumLength) =>
        value is null || value.Length <= maximumLength
            ? value
            : value[..maximumLength];
}
