using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Interface;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly IDBConnectionFactory _dbConnectionFactory;
    private readonly ICurrentUserContext _currentUser;

    public UserRepository(
        IDBConnectionFactory dbConnectionFactory,
        ICurrentUserContext currentUser)
    {
        _dbConnectionFactory = dbConnectionFactory;
        _currentUser = currentUser;
    }

    public async Task<User?> GetUserAsync(string username)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT u.UserId AS Id,
                   u.ClientId,
                   u.Username,
                   u.FullName,
                   u.PasswordHash,
                   u.CanAccessPortal,
                   u.IsActive,
                   r.RoleName AS Role
            FROM dbo.Users u
            INNER JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
            INNER JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            INNER JOIN dbo.Clients c ON c.Id=u.ClientId AND c.IsActive=1 AND c.DeleteFg='N'
            WHERE u.Username = @Username
              AND r.IsActive = 1;
            """;

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<UserPageResult> GetPageAsync(int pageNumber, int pageSize)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT COUNT(1) AS TotalUsers,
                   COALESCE(SUM(CASE WHEN u.IsActive = 1 THEN 1 ELSE 0 END), 0) AS ActiveUsers,
                   COALESCE(SUM(CASE WHEN u.IsActive = 0 THEN 1 ELSE 0 END), 0) AS InactiveUsers,
                   COALESCE(SUM(CASE WHEN r.RoleName = 'Admin' THEN 1 ELSE 0 END), 0) AS Administrators
            FROM dbo.Users u
            LEFT JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
            LEFT JOIN dbo.Roles r ON r.RoleId = ur.RoleId
            WHERE (@CanViewAll = 1 OR u.ClientId = @ClientId);

            SELECT u.UserId,
                   u.ClientId,
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
            WHERE (@CanViewAll = 1 OR u.ClientId = @ClientId)
            ORDER BY u.CreatedDate DESC, u.UserId DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        using var result = await connection.QueryMultipleAsync(sql, new
        {
            ClientId = _currentUser.ClientId,
            CanViewAll = CanViewAllClients(),
            Offset = ((long)pageNumber - 1) * pageSize,
            PageSize = pageSize
        });
        var summary = await result.ReadSingleAsync<UserSummaryRow>();
        return new UserPageResult
        {
            Items = (await result.ReadAsync<UserListItem>()).AsList(),
            TotalUsers = summary.TotalUsers,
            ActiveUsers = summary.ActiveUsers,
            InactiveUsers = summary.InactiveUsers,
            Administrators = summary.Administrators,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<UserListItem?> GetByIdAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT u.UserId,
                   u.ClientId,
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
            WHERE u.UserId = @UserId
              AND (@CanViewAll = 1 OR u.ClientId = @ClientId);
            """;

        return await connection.QuerySingleOrDefaultAsync<UserListItem>(sql, new
        {
            UserId = userId,
            ClientId = _currentUser.ClientId,
            CanViewAll = CanViewAllClients()
        });
    }

    public async Task<IReadOnlyList<RoleOption>> GetActiveRolesAsync()
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT RoleId, RoleName
            FROM dbo.Roles
            WHERE IsActive = 1
              AND (@CanAssignPlatformRole = 1 OR RoleName <> 'Admin')
            ORDER BY RoleName;
            """;

        var roles = await connection.QueryAsync<RoleOption>(sql, new
        {
            CanAssignPlatformRole = CanViewAllClients()
        });
        return roles.AsList();
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT p.PermissionName
            FROM dbo.Permissions p
            LEFT JOIN dbo.UserPermissions userOverride
                ON userOverride.UserId = @UserId
               AND userOverride.PermissionId = p.PermissionId
            WHERE p.IsActive = 1
              AND COALESCE(
                    userOverride.IsGranted,
                    CASE WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.UserRoles userRole
                        INNER JOIN dbo.Roles activeRole ON activeRole.RoleId = userRole.RoleId AND activeRole.IsActive = 1
                        INNER JOIN dbo.RolePermissions rolePermission
                            ON rolePermission.RoleId = userRole.RoleId
                        WHERE userRole.UserId = @UserId
                          AND rolePermission.PermissionId = p.PermissionId
                    ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END
                  ) = 1
            ORDER BY p.PermissionName;
            """;

        var permissions = await connection.QueryAsync<string>(sql, new { UserId = userId });
        return permissions.AsList();
    }

    public async Task UpdateLastLoginAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE dbo.Users SET LastLoginDate = SYSUTCDATETIME() WHERE UserId = @UserId;",
            new { UserId = userId });
    }

    public async Task<IReadOnlyList<PermissionListItem>> GetPermissionOptionsAsync(long userId)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
            SELECT p.PermissionId,
                   p.PermissionName,
                   p.Description,
                   CAST(CASE WHEN EXISTS
                   (
                       SELECT 1
                       FROM dbo.UserRoles userRole
                       INNER JOIN dbo.Roles activeRole ON activeRole.RoleId = userRole.RoleId AND activeRole.IsActive = 1
                       INNER JOIN dbo.RolePermissions rolePermission
                           ON rolePermission.RoleId = userRole.RoleId
                       WHERE userRole.UserId = @UserId
                         AND rolePermission.PermissionId = p.PermissionId
                   ) THEN 1 ELSE 0 END AS BIT) AS IsRoleGranted,
                   CAST(CASE WHEN userOverride.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT) AS HasOverride,
                   COALESCE(
                       userOverride.IsGranted,
                       CAST(CASE WHEN EXISTS
                       (
                           SELECT 1
                           FROM dbo.UserRoles userRole
                           INNER JOIN dbo.Roles activeRole ON activeRole.RoleId = userRole.RoleId AND activeRole.IsActive = 1
                           INNER JOIN dbo.RolePermissions rolePermission
                               ON rolePermission.RoleId = userRole.RoleId
                           WHERE userRole.UserId = @UserId
                             AND rolePermission.PermissionId = p.PermissionId
                       ) THEN 1 ELSE 0 END AS BIT)
                   ) AS IsSelected
            FROM dbo.Permissions p
            LEFT JOIN dbo.UserPermissions userOverride
                ON userOverride.PermissionId = p.PermissionId
               AND userOverride.UserId = @UserId
            WHERE p.IsActive = 1
              AND p.PermissionName IN @ApplicationPermissions
              AND (@CanGrantPlatformPermission = 1 OR p.PermissionName NOT IN @PlatformPermissions)
            ORDER BY p.PermissionName;
            """;

        var permissions = await connection.QueryAsync<PermissionListItem>(sql, new
        {
            UserId = userId,
            CanGrantPlatformPermission = CanViewAllClients(),
            ApplicationPermissions = AppPermissions.All,
            PlatformPermissions = AppPermissions.PlatformOnly
        });
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
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId AND (@CanViewAll = 1 OR ClientId = @ClientId);",
                new { UserId = userId, ClientId = _currentUser.ClientId, CanViewAll = CanViewAllClients() }, transaction) > 0;

            if (!userExists)
            {
                transaction.Rollback();
                return false;
            }

            const string deleteManageableOverrides = """
                DELETE userPermission
                FROM dbo.UserPermissions userPermission
                INNER JOIN dbo.Permissions permission
                    ON permission.PermissionId = userPermission.PermissionId
                WHERE userPermission.UserId = @UserId
                  AND permission.PermissionName IN @ApplicationPermissions
                  AND (@CanGrantPlatformPermission = 1 OR permission.PermissionName NOT IN @PlatformPermissions);
                """;

            await connection.ExecuteAsync(deleteManageableOverrides, new
            {
                UserId = userId,
                CanGrantPlatformPermission = CanViewAllClients(),
                ApplicationPermissions = AppPermissions.All,
                PlatformPermissions = AppPermissions.PlatformOnly
            }, transaction);

            const string permissionsSql = """
                SELECT p.PermissionId,
                       CAST(CASE WHEN rolePermission.PermissionId IS NULL THEN 0 ELSE 1 END AS BIT) AS IsRoleGranted
                FROM dbo.Permissions p
                LEFT JOIN
                (
                    SELECT DISTINCT rp.PermissionId
                    FROM dbo.UserRoles ur
                    INNER JOIN dbo.Roles activeRole ON activeRole.RoleId = ur.RoleId AND activeRole.IsActive = 1
                    INNER JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
                    WHERE ur.UserId = @UserId
                ) rolePermission ON rolePermission.PermissionId = p.PermissionId
                WHERE p.IsActive = 1
                  AND p.PermissionName IN @ApplicationPermissions
                  AND (@CanGrantPlatformPermission = 1 OR p.PermissionName NOT IN @PlatformPermissions);
                """;

            var availablePermissions = (await connection.QueryAsync<PermissionGrantState>(
                permissionsSql,
                new
                {
                    UserId = userId,
                    CanGrantPlatformPermission = CanViewAllClients(),
                    ApplicationPermissions = AppPermissions.All,
                    PlatformPermissions = AppPermissions.PlatformOnly
                },
                transaction)).AsList();
            var selectedIds = permissionIds.Distinct().ToHashSet();
            var overrides = availablePermissions
                .Where(permission => selectedIds.Contains(permission.PermissionId) != permission.IsRoleGranted)
                .Select(permission => new
                {
                    UserId = userId,
                    permission.PermissionId,
                    IsGranted = selectedIds.Contains(permission.PermissionId)
                })
                .ToList();

            if (overrides.Count > 0)
            {
                const string insert = """
                    INSERT INTO dbo.UserPermissions (UserId, PermissionId, IsGranted)
                    VALUES (@UserId, @PermissionId, @IsGranted);
                    """;

                await connection.ExecuteAsync(insert, overrides, transaction);
            }

            await InsertAuditAsync(
                connection,
                transaction,
                "UpdatePermissions",
                "User",
                userId.ToString(),
                $"Stored {overrides.Count} permission override(s).");

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
        EnsureCanCreateUsers();

        using var connection = _dbConnectionFactory.CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // The selected organization must exist and be active.
            const string clientSql = """
            SELECT COUNT(1)
            FROM dbo.Clients WITH (UPDLOCK, HOLDLOCK)
            WHERE Id = @ClientId
              AND IsActive = 1
              AND DeleteFg = 'N';
            """;

            var clientExists = await connection.ExecuteScalarAsync<int>(
                clientSql,
                new { user.ClientId },
                transaction);

            if (clientExists != 1)
            {
                throw new InvalidOperationException(
                    "Select a valid active organization.");
            }

            // The selected role must exist and be active.
            const string roleSql = """
            SELECT RoleName
            FROM dbo.Roles WITH (UPDLOCK, HOLDLOCK)
            WHERE RoleId = @RoleId
              AND IsActive = 1;
            """;

            var roleName = await connection.QuerySingleOrDefaultAsync<string>(
                roleSql,
                new { RoleId = roleId },
                transaction);

            if (string.IsNullOrWhiteSpace(roleName))
            {
                throw new InvalidOperationException(
                    "Select a valid active role.");
            }

            // Admin is the vendor role, not a partner role.
            if (roleName.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                user.ClientId != _currentUser.ClientId)
            {
                throw new InvalidOperationException(
                    "Admin can only be assigned within your vendor organization.");
            }

            const string insertUserSql = """
            INSERT INTO dbo.Users
                (ClientId, Username, FullName, PasswordHash, CanAccessPortal,
                 IsActive, CreatedDate)
            OUTPUT INSERTED.UserId
            VALUES
                (@ClientId, @Username, @FullName, @PasswordHash, @CanAccessPortal,
                 @IsActive, @CreatedDate);
            """;

            var userId = await connection.ExecuteScalarAsync<long>(
                insertUserSql,
                user,
                transaction);

            const string insertRoleSql = """
            INSERT INTO dbo.UserRoles (UserId, RoleId)
            VALUES (@UserId, @RoleId);
            """;

            await connection.ExecuteAsync(
                insertRoleSql,
                new { UserId = userId, RoleId = roleId },
                transaction);

            await InsertAuditAsync(
                connection,
                transaction,
                "Create",
                "User",
                userId.ToString(),
                $"Created user {user.Username} for ClientId {user.ClientId}.");

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
                "SELECT COUNT(1) FROM dbo.Users WHERE UserId = @UserId AND (@CanViewAll = 1 OR ClientId = @ClientId);",
                new { UserId = userId, ClientId = _currentUser.ClientId, CanViewAll = CanViewAllClients() }, transaction) > 0;

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

            await InsertAuditAsync(
                connection,
                transaction,
                "UpdateRole",
                "User",
                userId.ToString(),
                $"Assigned role {roleId}.");

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<IReadOnlyList<ClientOption>> GetActiveClientsAsync()
    {
        EnsureCanCreateUsers();
        using var connection = _dbConnectionFactory.CreateConnection();
        const string sql = """
        SELECT
            Id AS ClientId,
            ClientName
        FROM dbo.Clients
        WHERE IsActive = 1
          AND DeleteFg = 'N'
        ORDER BY ClientName;
        """;
        var clients = await connection.QueryAsync<ClientOption>(sql);
        return clients.ToList();
    }
    private bool CanViewAllClients() =>
        _currentUser.HasPermission(AppPermissions.AmlRecordsViewAll);

    private async Task InsertAuditAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction transaction,
        string action,
        string entityType,
        string? entityId,
        string? details)
    {
        const string sql = """
            INSERT INTO dbo.AuditLogs
                (ClientId, UserId, Username, Action, EntityType, EntityId, Details, CreatedDate)
            VALUES
                (@ClientId, @CurrentUserId, @Username, @Action, @EntityType, @EntityId, @Details, SYSUTCDATETIME());
            """;

        await connection.ExecuteAsync(sql, new
        {
            ClientId = _currentUser.ClientId,
            CurrentUserId = _currentUser.UserId,
            Username = _currentUser.Username,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        }, transaction);
    }

    private sealed class PermissionGrantState
    {
        public int PermissionId { get; init; }
        public bool IsRoleGranted { get; init; }
    }

    private sealed class UserSummaryRow
    {
        public int TotalUsers { get; init; }
        public int ActiveUsers { get; init; }
        public int InactiveUsers { get; init; }
        public int Administrators { get; init; }
    }
    private void EnsureCanCreateUsers()
    {
        if (!_currentUser.IsAuthenticated ||
            !_currentUser.IsAdministrator ||
            !_currentUser.HasPermission(AppPermissions.UsersCreate) ||
            !_currentUser.HasPermission(AppPermissions.RolesManage))
        {
            throw new UnauthorizedAccessException(
                "Only authorized vendor administrators can create users.");
        }
    }
}
