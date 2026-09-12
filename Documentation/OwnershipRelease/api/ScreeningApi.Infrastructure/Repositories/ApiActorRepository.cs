using Dapper;
using ScreeningApi.Application.Interface.Repositories;
using ScreeningApi.Application.Models;
using ScreeningApi.Infrastructure.Persistence;

namespace ScreeningApi.Infrastructure.Repositories;

public sealed class ApiActorRepository(IDBConnectionFactory factory) : IApiActorRepository
{
    public async Task<ApiActor?> FindAsync(string issuer, string subject, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(subject) ||
            issuer.Length > 300 || subject.Length > 300)
            return null;

        using var connection = factory.CreateConnection();
        const string sql = """
            SELECT a.Id AS ApiApplicationId, u.UserId, u.ClientId,
                dbo.fn_HasPermission(u.UserId,N'Customers.Manage') AS CanManageCustomers,
                dbo.fn_HasPermission(u.UserId,N'Screening.View') AS CanScreen,
                dbo.fn_HasPermission(u.UserId,N'Customers.View') AS CanViewCustomers,
                dbo.fn_IsSystemAdmin(u.UserId) AS IsSystemAdministrator
            FROM dbo.ApiApplications a
            INNER JOIN dbo.Users u ON u.UserId=a.OwnerUserId AND u.IsActive=1
            INNER JOIN dbo.Clients c ON c.Id=u.ClientId AND c.IsActive=1 AND c.DeleteFg='N'
            WHERE a.IsActive=1
              AND a.Issuer COLLATE Latin1_General_100_BIN2=@Issuer COLLATE Latin1_General_100_BIN2
              AND a.Subject COLLATE Latin1_General_100_BIN2=@Subject COLLATE Latin1_General_100_BIN2;
            """;
        // Multiple mappings are a configuration error, never pick the first account.
        return await connection.QuerySingleOrDefaultAsync<ApiActor>(
            new CommandDefinition(sql, new { Issuer=issuer, Subject=subject },
                cancellationToken:ct));
    }
}

