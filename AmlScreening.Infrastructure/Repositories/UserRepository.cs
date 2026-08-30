using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDBConnectionFactory _dbConnectionFactory;

    public UserRepository(IDBConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<User?> GetUserAsync(string username)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT CAST(u.UserId AS INT) AS Id,
                   u.Username,
                   u.FullName,
                   u.PasswordHash,
                   u.IsActive,
                   r.RoleName AS Role
            FROM dbo.Users u
            INNER JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
            INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE u.Username = @Username
              AND u.IsActive = 1
              AND r.IsActive = 1;
            """;

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<IReadOnlyList<UserListItem>> GetAllAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT u.UserId,
                   u.Username,
                   u.FullName,
                   r.RoleId,
                   r.RoleName,
                   u.IsActive,
                   u.LastLoginDate,
                   u.CreatedDate
            FROM dbo.Users u
            LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
            LEFT JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            ORDER BY u.CreatedDate DESC, u.UserId DESC;
            """;

        var users = await connection.QueryAsync<UserListItem>(sql);
        return users.AsList();
    }

    public async Task<UserListItem?> GetByIdAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT u.UserId,
                   u.Username,
                   u.FullName,
                   r.RoleId,
                   r.RoleName,
                   u.IsActive,
                   u.LastLoginDate,
                   u.CreatedDate
            FROM dbo.Users u
            LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
            LEFT JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE u.UserId = @UserId;
            """;

        return await connection.QuerySingleOrDefaultAsync<UserListItem>(sql, new { UserId = userId });
    }

    public async Task<IReadOnlyList<RoleOption>> GetActiveRolesAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT RoleId, RoleName
            FROM dbo.Roles
            WHERE IsActive = 1
            ORDER BY RoleName;
            """;

        var roles = await connection.QueryAsync<RoleOption>(sql);
        return roles.AsList();
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(int userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT DISTINCT p.PermissionName
            FROM dbo.UserPermissions up
            INNER JOIN dbo.Permissions p ON p.PermissionId = up.PermissionId
            WHERE up.UserId = @UserId
              AND p.IsActive = 1
            ORDER BY p.PermissionName;
            """;

        var permissions = await connection.QueryAsync<string>(sql, new { UserId = userId });
        return permissions.AsList();
    }

    public async Task<IReadOnlyList<PermissionListItem>> GetPermissionOptionsAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT p.PermissionId,
                   p.PermissionName,
                   p.Description,
                   CAST(CASE WHEN up.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT) AS IsSelected
            FROM dbo.Permissions p
            LEFT JOIN dbo.UserPermissions up
                ON up.PermissionId = p.PermissionId AND up.UserId = @UserId
            WHERE p.IsActive = 1
            ORDER BY p.PermissionName;
            """;

        var permissions = await connection.QueryAsync<PermissionListItem>(sql, new { UserId = userId });
        return permissions.AsList();
    }

    public async Task<bool> UpdatePermissionsAsync(long userId, IReadOnlyCollection<int> permissionIds)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var userExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId;",
                new { UserId = userId }, transaction) > 0;

            if (!userExists)
            {
                transaction.Rollback();
                return false;
            }

            await connection.ExecuteAsync(
                "DELETE FROM dbo.UserPermissions WHERE UserId = @UserId;",
                new { UserId = userId }, transaction);

            if (permissionIds.Count > 0)
            {
                const string insert = """
                    INSERT INTO dbo.UserPermissions (UserId, PermissionId)
                    SELECT @UserId, p.PermissionId
                    FROM dbo.Permissions p
                    WHERE p.PermissionId = @PermissionId
                      AND p.IsActive = 1;
                    """;

                await connection.ExecuteAsync(
                    insert,
                    permissionIds.Distinct().Select(id => new { UserId = userId, PermissionId = id }),
                    transaction);
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

    public async Task<bool> UsernameExistsAsync(string username)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM dbo.Users WHERE Username = @Username;";
        return await connection.ExecuteScalarAsync<int>(sql, new { Username = username }) > 0;
    }

    public async Task<long> CreateAsync(User user, int roleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            const string insertUser = """
                INSERT INTO dbo.Users
                    (Username, FullName, PasswordHash, IsActive, CreatedDate)
                OUTPUT INSERTED.UserId
                VALUES
                    (@Username, @FullName, @PasswordHash, @IsActive, @CreatedDate);
                """;

            var userId = await connection.ExecuteScalarAsync<long>(insertUser, user, transaction);

            const string insertRole = """
                INSERT INTO dbo.UserRoles (UserId, RoleId)
                VALUES (@UserId, @RoleId);
                """;

            await connection.ExecuteAsync(insertRole, new { UserId = userId, RoleId = roleId }, transaction);

            const string copyRolePermissions = """
                INSERT INTO dbo.UserPermissions (UserId, PermissionId)
                SELECT @UserId, rp.PermissionId
                FROM dbo.RolePermissions rp
                WHERE rp.RoleId = @RoleId;
                """;

            await connection.ExecuteAsync(
                copyRolePermissions,
                new { UserId = userId, RoleId = roleId },
                transaction);

            transaction.Commit();
            return userId;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> UpdateRoleAsync(long userId, int roleId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try
        {
            var userExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId;",
                new { UserId = userId }, transaction) > 0;

            var roleExists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Roles WHERE RoleId = @RoleId AND IsActive = 1;",
                new { RoleId = roleId }, transaction) > 0;

            if (!userExists || !roleExists)
            {
                transaction.Rollback();
                return false;
            }

            await connection.ExecuteAsync(
                "DELETE FROM dbo.UserRoles WHERE UserId = @UserId;",
                new { UserId = userId }, transaction);

            await connection.ExecuteAsync(
                "INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);",
                new { UserId = userId, RoleId = roleId }, transaction);

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
