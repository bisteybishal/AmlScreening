using AmlScreening.Application.Interface;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using AmlScreening.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using System.Data;
using Xunit;

namespace AmlScreening.IntegrationTests;

public sealed class SqlServerPlatformTests
{
    private const string ConnectionVariable = "AML_SQLSERVER_TEST_CONNECTION";

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task Hardening_schema_exists_when_sql_server_tests_are_configured()
    {
        var connectionString = RequireConnectionString();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = """
            SELECT
                CASE WHEN OBJECT_ID(N'dbo.MenuItems', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.MenuItems', N'ParentMenuId') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.MenuItems', N'PermissionCode') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.UserPermissions', N'IsGranted') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.AmlRecords', N'RowVersion') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.ComplianceRules', N'RowVersion') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN COL_LENGTH(N'dbo.AML_Subscription_plan', N'RowVersion') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.AmlTransactions', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.ScreeningMatches', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.RiskAssessments', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.AmlDecisions', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.ComplianceAlerts', N'U') IS NOT NULL THEN 1 ELSE 0 END,
                CASE WHEN OBJECT_ID(N'dbo.ComplianceCases', N'U') IS NOT NULL THEN 1 ELSE 0 END;
            """;
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        for (var column = 0; column < reader.FieldCount; column++)
            Assert.Equal(1, reader.GetInt32(column));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task Subscription_repository_soft_deletes_and_writes_audit_atomically()
    {
        var connectionString = RequireConnectionString();

        var identity = await LoadExistingIdentityAsync(connectionString);
        var currentUser = new FakeCurrentUserContext(identity.UserId, identity.ClientId);
        var repository = new SubscriptionRepository(
            new TestConnectionFactory(connectionString),
            currentUser);
        var sourceCode = $"TEST-{Guid.NewGuid():N}"[..20];
        var planId = 0;

        try
        {
            planId = await repository.CreateSubscriptionAsync(new SubscriptionPlan
            {
                SourceCode = sourceCode,
                SourceName = "SQL integration test",
                DurationMonths = 1,
                Rate = 1,
                IsActive = true
            });
            var plan = await repository.GetByIdAsync(planId);
            Assert.NotNull(plan);

            Assert.True(await repository.UpdateAsync(new SubscriptionPlan
            {
                PlanId = plan.PlanId,
                RowVersion = plan.RowVersion,
                SourceCode = plan.SourceCode,
                SourceName = "SQL integration test updated",
                DurationMonths = plan.DurationMonths,
                Rate = plan.Rate,
                IsActive = plan.IsActive
            }));
            Assert.False(await repository.UpdateAsync(new SubscriptionPlan
            {
                PlanId = plan.PlanId,
                RowVersion = plan.RowVersion,
                SourceCode = plan.SourceCode,
                SourceName = "Stale update must fail",
                DurationMonths = plan.DurationMonths,
                Rate = plan.Rate,
                IsActive = plan.IsActive
            }));

            var refreshedPlan = await repository.GetByIdAsync(planId);
            Assert.NotNull(refreshedPlan);
            Assert.True(await repository.DeleteAsync(planId, refreshedPlan.RowVersion));
            Assert.Null(await repository.GetByIdAsync(planId));
            Assert.True(await AuditExistsAsync(connectionString, planId));
        }
        finally
        {
            if (planId > 0)
                await RemoveTestRowsAsync(connectionString, planId);
        }
    }

    private static async Task<(long UserId, int ClientId)> LoadExistingIdentityAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(
            "SELECT TOP (1) UserId, ClientId FROM dbo.Users ORDER BY UserId;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The SQL integration database needs at least one user.");
        return (Convert.ToInt64(reader.GetValue(0)), Convert.ToInt32(reader.GetValue(1)));
    }

    private static async Task<bool> AuditExistsAsync(string connectionString, int planId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            SELECT COUNT(1) FROM dbo.AuditLogs
            WHERE EntityType = 'SubscriptionPlan' AND EntityId = @EntityId AND Action = 'Delete';
            """, connection);
        command.Parameters.AddWithValue("@EntityId", planId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task RemoveTestRowsAsync(string connectionString, int planId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("""
            DELETE FROM dbo.AuditLogs WHERE EntityType = 'SubscriptionPlan' AND EntityId = @EntityId;
            DELETE FROM dbo.AML_Subscription_plan WHERE PlanId = @PlanId;
            """, connection);
        command.Parameters.AddWithValue("@EntityId", planId.ToString());
        command.Parameters.AddWithValue("@PlanId", planId);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestConnectionFactory : IDBConnectionFactory
    {
        private readonly string _connectionString;
        public TestConnectionFactory(string connectionString) => _connectionString = connectionString;
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }

    private static string RequireConnectionString() =>
        Environment.GetEnvironmentVariable(ConnectionVariable) is { Length: > 0 } connectionString
            ? connectionString
            : throw new InvalidOperationException(
                $"Set {ConnectionVariable} to run SQL Server integration tests.");

    private sealed class FakeCurrentUserContext : ICurrentUserContext
    {
        public FakeCurrentUserContext(long userId, int clientId)
        {
            UserId = userId;
            ClientId = clientId;
        }

        public bool IsAuthenticated => true;
        public long UserId { get; }
        public int ClientId { get; }
        public string Username => "sql-integration-test";
        public bool HasPermission(string permissionCode) => false;
    }
}

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("AML_SQLSERVER_TEST_CONNECTION")))
        {
            Skip = "Set AML_SQLSERVER_TEST_CONNECTION to run SQL Server integration tests.";
        }
    }
}
