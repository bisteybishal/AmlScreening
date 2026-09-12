using System.Data;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Persistence;
using Dapper;

namespace AmlScreening.Infrastructure.Repositories;

// Account access is independent of role names. Neither operation changes UserId or ClientId.
public sealed class AccountAccessRepository(IDBConnectionFactory factory, ICurrentUserContext currentUser)
    : IAccountAccessRepository
{
    private async Task RequireAdministratorAsync(IDbConnection connection, IDbTransaction? transaction = null)
    {
        if (!currentUser.IsAuthenticated || !currentUser.IsAdministrator ||
            !currentUser.HasPermission(AppPermissions.RolesManage) ||
            !currentUser.HasPermission(AppPermissions.AmlRecordsViewAll))
            throw new UnauthorizedAccessException("System administrator access is required.");

        // Recheck live database permissions inside the same transaction as every change.
        var allowed = await connection.ExecuteScalarAsync<bool>("""
            SELECT CAST(CASE WHEN dbo.fn_IsSystemAdmin(@UserId)=1
                AND dbo.fn_HasPermission(@UserId,N'Roles.Manage')=1 THEN 1 ELSE 0 END AS BIT);
            """, new { UserId = currentUser.UserId }, transaction);
        if (!allowed) throw new UnauthorizedAccessException("System administrator access is required.");
    }

    private static async Task RequireActiveOwnerAsync(IDbConnection connection, IDbTransaction transaction, long userId)
    {
        var exists = await connection.ExecuteScalarAsync<int>("""
            SELECT COUNT(*) FROM dbo.Users u WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.Clients c ON c.Id=u.ClientId
            WHERE u.UserId=@UserId AND u.IsActive=1 AND c.IsActive=1 AND c.DeleteFg='N';
            """, new { UserId = userId }, transaction);
        if (exists != 1) throw new InvalidOperationException("Choose an active existing account with an active client.");
    }

    public async Task<AccountAccessInfo?> GetAsync(long userId)
    {
        using var connection = factory.CreateConnection();
        await RequireAdministratorAsync(connection);
        return await connection.QuerySingleOrDefaultAsync<AccountAccessInfo>("""
            SELECT UserId,ClientId,Username,FullName,IsActive,CanAccessPortal FROM dbo.Users WHERE UserId=@UserId;
            """, new { UserId = userId });
    }

    public async Task<IReadOnlyList<ApiApplicationInfo>> GetApplicationsAsync(long userId)
    {
        using var connection = factory.CreateConnection();
        await RequireAdministratorAsync(connection);
        return (await connection.QueryAsync<ApiApplicationInfo>("""
            SELECT Id,Issuer,Subject,IsActive FROM dbo.ApiApplications WHERE OwnerUserId=@UserId ORDER BY Id;
            """, new { UserId = userId })).ToList();
    }

    public async Task SetPortalAccessAsync(long userId, bool enabled)
    {
        using var connection = factory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await RequireAdministratorAsync(connection, transaction);
        await RequireActiveOwnerAsync(connection, transaction, userId);
        if (userId == currentUser.UserId && !enabled)
            throw new InvalidOperationException("You cannot disable your own portal access.");

        await connection.ExecuteAsync("""
            UPDATE dbo.Users SET CanAccessPortal=@Enabled,
                ActiveSessionId=CASE WHEN @Enabled=0 THEN NULL ELSE ActiveSessionId END,
                SessionStartedAtUtc=CASE WHEN @Enabled=0 THEN NULL ELSE SessionStartedAtUtc END,
                SessionExpiresAtUtc=CASE WHEN @Enabled=0 THEN NULL ELSE SessionExpiresAtUtc END
            WHERE UserId=@UserId;
            """,
            new { UserId = userId, Enabled = enabled }, transaction);
        await AuditAsync(connection, transaction, userId, "Portal access", enabled ? "Enabled portal sign-in." : "Disabled portal sign-in.");
        transaction.Commit();
    }

    public async Task LinkApplicationAsync(long userId, string issuer, string subject)
    {
        issuer = issuer?.Trim() ?? "";
        subject = subject?.Trim() ?? "";
        if (issuer.Length > 300 || !Uri.TryCreate(issuer, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || string.IsNullOrWhiteSpace(subject) || subject.Length > 300)
            throw new InvalidOperationException("Supply the verified HTTPS issuer and application subject, each at most 300 characters.");

        using var connection = factory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await RequireAdministratorAsync(connection, transaction);
        await RequireActiveOwnerAsync(connection, transaction, userId);
        var existingOwner = await connection.QuerySingleOrDefaultAsync<long?>("""
            SELECT OwnerUserId FROM dbo.ApiApplications WITH(UPDLOCK,HOLDLOCK)
            WHERE Issuer=@Issuer COLLATE Latin1_General_100_BIN2 AND Subject=@Subject COLLATE Latin1_General_100_BIN2;
            """, new { Issuer = issuer, Subject = subject }, transaction);
        if (existingOwner.HasValue)
            throw new InvalidOperationException(existingOwner == userId
                ? "This application is already linked. Use its enable/disable button below."
                : "This application belongs to another account. It cannot be reassigned here.");

        var applicationId = await connection.ExecuteScalarAsync<int>("""
            INSERT dbo.ApiApplications(Issuer,Subject,OwnerUserId,IsActive)
            OUTPUT INSERTED.Id VALUES(@Issuer,@Subject,@UserId,1);
            """, new { Issuer = issuer, Subject = subject, UserId = userId }, transaction);
        await AuditAsync(connection, transaction, userId, "Map API application", $"Linked verified application {applicationId} to existing account.");
        transaction.Commit();
    }

    public async Task SetApplicationActiveAsync(long userId, int applicationId, bool enabled)
    {
        using var connection = factory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await RequireAdministratorAsync(connection, transaction);
        // Disabling an application must remain possible even when its owner is inactive.
        if (enabled) await RequireActiveOwnerAsync(connection, transaction, userId);
        var changed = await connection.ExecuteAsync("""
            UPDATE dbo.ApiApplications SET IsActive=@Enabled WHERE Id=@Id AND OwnerUserId=@UserId;
            """, new { Id = applicationId, UserId = userId, Enabled = enabled }, transaction);
        if (changed != 1) throw new InvalidOperationException("The application is not linked to this account.");
        await AuditAsync(connection, transaction, userId, "API application access", $"Application {applicationId}: enabled={enabled}.");
        transaction.Commit();
    }

    private Task<int> AuditAsync(IDbConnection connection, IDbTransaction transaction, long owner, string action, string details) =>
        connection.ExecuteAsync("""
            INSERT dbo.OwnershipConfigurationAudit(ChangedByUserId,ChangeType,TargetUserId,Details)
            VALUES(@Actor,@Action,@Owner,@Details);
            """, new { Actor = currentUser.UserId, Action = action, Owner = owner, Details = details }, transaction);
}
