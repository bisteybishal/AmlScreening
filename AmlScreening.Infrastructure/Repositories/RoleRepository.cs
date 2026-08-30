using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Infrastructure.Persistence;
using Dapper;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IDBConnectionFactory _connectionFactory;

    public RoleRepository(IDBConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<RoleListItem>> GetAllAsync()
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT r.RoleId,
                   r.RoleName,
                   r.IsActive,
                   r.CreatedDate,
                   COUNT(DISTINCT ur.UserId) AS UserCount,
                   COUNT(DISTINCT rp.PermissionId) AS PermissionCount
            FROM dbo.Roles r
            LEFT JOIN dbo.UserRoles ur ON ur.RoleId = r.RoleId
            LEFT JOIN dbo.RolePermissions rp ON rp.RoleId = r.RoleId
            GROUP BY r.RoleId, r.RoleName, r.IsActive, r.CreatedDate
            ORDER BY CASE WHEN r.RoleName = 'Admin' THEN 0 ELSE 1 END, r.RoleName;
            """;

        var roles = await connection.QueryAsync<RoleListItem>(sql);
        return roles.AsList();
    }

    public async Task<RoleListItem?> GetByIdAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT r.RoleId, r.RoleName, r.IsActive, r.CreatedDate,
                   (SELECT COUNT(*) FROM dbo.UserRoles ur WHERE ur.RoleId = r.RoleId) AS UserCount,
                   (SELECT COUNT(*) FROM dbo.RolePermissions rp WHERE rp.RoleId = r.RoleId) AS PermissionCount
            FROM dbo.Roles r
            WHERE r.RoleId = @RoleId;
            """;

        return await connection.QuerySingleOrDefaultAsync<RoleListItem>(sql, new { RoleId = roleId });
    }

    public async Task<IReadOnlyList<PermissionListItem>> GetPermissionsAsync(int roleId)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            SELECT p.PermissionId,
                   p.PermissionName,
                   p.Description,
                   CAST(CASE WHEN rp.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT) AS IsSelected
            FROM dbo.Permissions p
            LEFT JOIN dbo.RolePermissions rp
                ON rp.PermissionId = p.PermissionId AND rp.RoleId = @RoleId
            WHERE p.IsActive = 1
            ORDER BY p.PermissionName;
            """;

        var permissions = await connection.QueryAsync<PermissionListItem>(sql, new { RoleId = roleId });
        return permissions.AsList();
    }

    public async Task<bool> RoleNameExistsAsync(string roleName)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Roles WHERE RoleName = @RoleName;";
        return await connection.ExecuteScalarAsync<int>(sql, new { RoleName = roleName }) > 0;
    }

    public async Task<int> CreateAsync(string roleName, bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();
        const string sql = """
            INSERT INTO dbo.Roles (RoleName, IsActive, CreatedDate)
            OUTPUT INSERTED.RoleId
            VALUES (@RoleName, @IsActive, SYSUTCDATETIME());
            """;

        return await connection.ExecuteScalarAsync<int>(sql, new { RoleName = roleName, IsActive = isActive });
    }

    public async Task<bool> UpdatePermissionsAsync(int roleId, IReadOnlyCollection<int> permissionIds)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            await connection.ExecuteAsync(
                "DELETE FROM dbo.RolePermissions WHERE RoleId = @RoleId;",
                new { RoleId = roleId }, transaction);

            if (permissionIds.Count > 0)
            {
                const string insert = """
                    INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
                    SELECT @RoleId, p.PermissionId
                    FROM dbo.Permissions p
                    WHERE p.PermissionId = @PermissionId AND p.IsActive = 1;
                    """;

                await connection.ExecuteAsync(insert,
                    permissionIds.Select(id => new { RoleId = roleId, PermissionId = id }), transaction);
            }

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
