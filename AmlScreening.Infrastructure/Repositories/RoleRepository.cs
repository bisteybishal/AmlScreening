using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Application.Security;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly IDBConnectionFactory
        _connectionFactory;

    private readonly ICurrentUserContext
        _currentUser;


    public RoleRepository(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser)
    {
        _connectionFactory =
            connectionFactory;

        _currentUser =
            currentUser;
    }


    // =========================================================
    // GET ALL ROLES
    // =========================================================

    public async Task<IReadOnlyList<RoleListItem>>
        GetAllAsync(
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            R.RoleId,
            R.RoleName,
            R.IsActive,
            R.CreatedDate,
            R.CreatedBy,

            (
                SELECT COUNT(DISTINCT UR.UserId)
                FROM dbo.UserRoles UR
                WHERE UR.RoleId = R.RoleId
            ) AS UserCount,

            (
                SELECT COUNT(DISTINCT RP.PermissionId)
                FROM dbo.RolePermissions RP
                WHERE RP.RoleId = R.RoleId
            ) AS PermissionCount

        FROM dbo.Roles R

        ORDER BY
            CASE
                WHEN R.RoleName = 'ADMIN'
                    THEN 0

                WHEN R.RoleName = 'USER'
                    THEN 1

                WHEN R.RoleName = 'API_USER'
                    THEN 2

                ELSE 3
            END,

            R.RoleName;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var command =
            new CommandDefinition(
                sql,
                cancellationToken:
                    cancellationToken);


        var result =
            await connection
                .QueryAsync<RoleListItem>(
                    command);


        return result.AsList();
    }


    // =========================================================
    // GET ROLE
    // =========================================================

    public async Task<RoleListItem?>
        GetByIdAsync(
            int roleId,
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        if (roleId <= 0)
        {
            return null;
        }


        const string sql = """
        SELECT
            R.RoleId,
            R.RoleName,
            R.IsActive,
            R.CreatedDate,
            R.CreatedBy,

            (
                SELECT COUNT(1)

                FROM dbo.UserRoles UR

                WHERE UR.RoleId =
                      R.RoleId

            ) AS UserCount,

            (
                SELECT COUNT(1)

                FROM dbo.RolePermissions RP

                WHERE RP.RoleId =
                      R.RoleId

            ) AS PermissionCount

        FROM dbo.Roles R

        WHERE R.RoleId =
              @RoleId;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var command =
            new CommandDefinition(
                sql,
                new
                {
                    RoleId =
                        roleId
                },
                cancellationToken:
                    cancellationToken);


        return await connection
            .QuerySingleOrDefaultAsync<
                RoleListItem>(
                    command);
    }


    // =========================================================
    // GET ROLE PERMISSIONS
    // =========================================================

    public async Task<
        IReadOnlyList<PermissionListItem>>
        GetPermissionsAsync(
            int roleId,
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            P.PermissionId,

            P.PermissionCode,

            P.PermissionName,

            CAST
            (
                CASE
                    WHEN RP.PermissionId IS NULL
                        THEN 0
                    ELSE 1
                END
                AS BIT
            ) AS IsSelected

        FROM dbo.Permissions P

        LEFT JOIN dbo.RolePermissions RP

            ON RP.PermissionId =
               P.PermissionId

            AND RP.RoleId =
                @RoleId

        WHERE P.IsActive = 1

        AND P.PermissionCode
            IN @ApplicationPermissions

        ORDER BY
            P.PermissionCode;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var command =
            new CommandDefinition(
                sql,
                new
                {
                    RoleId =
                        roleId,

                    ApplicationPermissions =
                        AppPermissions.All
                },
                cancellationToken:
                    cancellationToken);


        var result =
            await connection
                .QueryAsync<
                    PermissionListItem>(
                        command);


        return result.AsList();
    }


    // =========================================================
    // ROLE NAME EXISTS
    // =========================================================

    public async Task<bool>
        RoleNameExistsAsync(
            string roleName,
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        roleName =
            roleName?.Trim()
            ?? string.Empty;


        if (roleName.Length == 0)
        {
            return false;
        }


        const string sql = """
        SELECT
            CASE
                WHEN EXISTS
                (
                    SELECT 1

                    FROM dbo.Roles

                    WHERE RoleName =
                          @RoleName
                )
                THEN 1
                ELSE 0
            END;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var command =
            new CommandDefinition(
                sql,
                new
                {
                    RoleName =
                        roleName
                },
                cancellationToken:
                    cancellationToken);


        return await connection
            .ExecuteScalarAsync<bool>(
                command);
    }


    // =========================================================
    // CREATE ROLE
    // =========================================================

    public async Task<int>
        CreateAsync(
            string roleName,
            bool isActive,
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        roleName =
            roleName?.Trim()
            ?? string.Empty;


        if (roleName.Length < 2)
        {
            throw new ArgumentException(
                "Role name is required.");
        }


        using var connection =
            _connectionFactory
                .CreateConnection();


        if (connection.State !=
            ConnectionState.Open)
        {
            connection.Open();
        }


        using var transaction =
            connection.BeginTransaction();


        try
        {
            const string duplicateSql = """
            SELECT
                CASE
                    WHEN EXISTS
                    (
                        SELECT 1

                        FROM dbo.Roles

                        WHERE RoleName =
                              @RoleName
                    )
                    THEN 1
                    ELSE 0
                END;
            """;


            var duplicate =
                await connection
                    .ExecuteScalarAsync<bool>(
                        new CommandDefinition(
                            duplicateSql,
                            new
                            {
                                RoleName =
                                    roleName
                            },
                            transaction,
                            cancellationToken:
                                cancellationToken));


            if (duplicate)
            {
                throw new
                    InvalidOperationException(
                        "This role already exists.");
            }


            const string insertSql = """
            INSERT INTO dbo.Roles
            (
                RoleName,
                IsActive,
                CreatedDate,
                CreatedBy
            )
            OUTPUT
                INSERTED.RoleId
            VALUES
            (
                @RoleName,
                @IsActive,
                SYSUTCDATETIME(),
                @CreatedBy
            );
            """;


            var roleId =
                await connection
                    .ExecuteScalarAsync<int>(
                        new CommandDefinition(
                            insertSql,
                            new
                            {
                                RoleName =
                                    roleName,

                                IsActive =
                                    isActive,

                                CreatedBy =
                                    _currentUser.Username
                            },
                            transaction,
                            cancellationToken:
                                cancellationToken));


            transaction.Commit();


            return roleId;
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }


    // =========================================================
    // UPDATE ROLE PERMISSIONS
    // =========================================================

    public async Task
        UpdatePermissionsAsync(
            int roleId,
            IReadOnlyCollection<int> permissionIds,
            CancellationToken cancellationToken = default)
    {
        EnsureSignedIn();


        if (roleId <= 0)
        {
            throw new ArgumentException(
                "Invalid role.");
        }


        permissionIds ??=
            Array.Empty<int>();


        using var connection =
            _connectionFactory
                .CreateConnection();


        if (connection.State !=
            ConnectionState.Open)
        {
            connection.Open();
        }


        using var transaction =
            connection.BeginTransaction();


        try
        {
            // -----------------------------------------
            // ROLE EXISTS
            // -----------------------------------------

            const string roleSql = """
            SELECT
                RoleName

            FROM dbo.Roles

            WHERE RoleId =
                  @RoleId;
            """;


            var roleName =
                await connection
                    .QuerySingleOrDefaultAsync<string>(
                        new CommandDefinition(
                            roleSql,
                            new
                            {
                                RoleId =
                                    roleId
                            },
                            transaction,
                            cancellationToken:
                                cancellationToken));


            if (string.IsNullOrWhiteSpace(
                    roleName))
            {
                throw new
                    InvalidOperationException(
                        "Role was not found.");
            }


            // -----------------------------------------
            // VALID PERMISSIONS
            // -----------------------------------------

            const string permissionsSql = """
            SELECT
                PermissionId,
                PermissionCode

            FROM dbo.Permissions

            WHERE IsActive = 1

            AND PermissionCode
                IN @ApplicationPermissions;
            """;


            var validPermissions =
                (
                    await connection
                        .QueryAsync<
                            PermissionCodeRow>(
                                new CommandDefinition(
                                    permissionsSql,
                                    new
                                    {
                                        ApplicationPermissions =
                                            AppPermissions.All
                                    },
                                    transaction,
                                    cancellationToken:
                                        cancellationToken))
                ).ToList();


            IReadOnlyCollection<int>
                finalPermissionIds;


            // -----------------------------------------
            // ADMIN PROTECTION
            // -----------------------------------------

            if (roleName.Equals(
                    AppRoles.Admin,
                    StringComparison.OrdinalIgnoreCase))
            {
                finalPermissionIds =
                    validPermissions
                        .Where(
                            permission =>
                                AppPermissions
                                    .AdminDefaults
                                    .Contains(
                                        permission
                                            .PermissionCode,
                                        StringComparer
                                            .OrdinalIgnoreCase))
                        .Select(
                            permission =>
                                permission
                                    .PermissionId)
                        .ToArray();
            }
            else
            {
                var validIds =
                    validPermissions
                        .Select(
                            x =>
                                x.PermissionId)
                        .ToHashSet();


                finalPermissionIds =
                    permissionIds

                        .Where(
                            id =>
                                validIds.Contains(
                                    id))

                        .Distinct()

                        .ToArray();
            }


            // -----------------------------------------
            // REMOVE OLD MAPPINGS
            // -----------------------------------------

            await connection
                .ExecuteAsync(
                    new CommandDefinition(
                        """
                        DELETE
                        FROM dbo.RolePermissions
                        WHERE RoleId =
                              @RoleId;
                        """,
                        new
                        {
                            RoleId =
                                roleId
                        },
                        transaction,
                        cancellationToken:
                            cancellationToken));


            // -----------------------------------------
            // INSERT CURRENT MAPPINGS
            // -----------------------------------------

            const string insertSql = """
            INSERT INTO dbo.RolePermissions
            (
                RoleId,
                PermissionId,
                CreatedDate,
                CreatedBy
            )
            VALUES
            (
                @RoleId,
                @PermissionId,
                SYSUTCDATETIME(),
                @CreatedBy
            );
            """;


            foreach (
                var permissionId
                in finalPermissionIds)
            {
                await connection
                    .ExecuteAsync(
                        new CommandDefinition(
                            insertSql,
                            new
                            {
                                RoleId =
                                    roleId,

                                PermissionId =
                                    permissionId,

                                CreatedBy =
                                    _currentUser.Username
                            },
                            transaction,
                            cancellationToken:
                                cancellationToken));
            }


            // -----------------------------------------
            // LAST CHANGE INFORMATION
            // -----------------------------------------

            await connection
                .ExecuteAsync(
                    new CommandDefinition(
                        """
                        UPDATE dbo.Roles

                        SET
                            UpdatedDate =
                                SYSUTCDATETIME(),

                            UpdatedBy =
                                @UpdatedBy

                        WHERE RoleId =
                              @RoleId;
                        """,
                        new
                        {
                            RoleId =
                                roleId,

                            UpdatedBy =
                                _currentUser.Username
                        },
                        transaction,
                        cancellationToken:
                            cancellationToken));

            // Existing login cookies contain the previous permission claims.
            await connection.ExecuteAsync(
                new CommandDefinition(
                    """
        UPDATE sessions
        SET IsActive = 0,
            EndedAtUtc = SYSUTCDATETIME(),
            EndReason = 'Role permissions changed'
        FROM dbo.UserSessions AS sessions
        WHERE sessions.IsActive = 1
          AND EXISTS
          (
              SELECT 1
              FROM dbo.UserRoles AS assigned
              WHERE assigned.UserId = sessions.UserId
                AND assigned.RoleId = @RoleId
          );
        """,
                    new { RoleId = roleId },
                    transaction,
                    cancellationToken: cancellationToken));


            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }


    // =========================================================
    // SECURITY
    // =========================================================

    private void EnsureSignedIn()
    {
        if (!_currentUser.IsAuthenticated
            ||
            _currentUser.UserId <= 0)
        {
            throw new UnauthorizedAccessException(
                "Login required.");
        }
    }


    private sealed class PermissionCodeRow
    {
        public int PermissionId { get; set; }

        public string PermissionCode { get; set; }
            = string.Empty;
    }
}