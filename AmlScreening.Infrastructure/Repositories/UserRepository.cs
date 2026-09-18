using AmlScreening.Application.Interface;
using AmlScreening.Application.Interface.Repositories;
using AmlScreening.Application.Models;
using AmlScreening.Domain.Entities;
using AmlScreening.Infrastructure.Persistence;
using Dapper;
using System.Data;

namespace AmlScreening.Infrastructure.Repositories;

public sealed class UserRepository
    : IUserRepository
{
    private readonly IDBConnectionFactory
        _connectionFactory;

    private readonly ICurrentUserContext
        _currentUser;


    public UserRepository(
        IDBConnectionFactory connectionFactory,
        ICurrentUserContext currentUser)
    {
        _connectionFactory =
            connectionFactory;

        _currentUser =
            currentUser;
    }


    // ==========================================================
    // PARTNERS
    // ==========================================================

    public async Task<IReadOnlyList<PartnerOption>>
        GetActivePartnersAsync()
    {
        EnsureSignedIn();

        const string sql = """
        SELECT
            PartnerId,
            PartnerCode,
            PartnerName

        FROM dbo.Partners

        WHERE IsActive = 1

        ORDER BY PartnerName;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return (
            await connection
                .QueryAsync<PartnerOption>(
                    sql)
        ).AsList();
    }


    public async Task<bool>
        PartnerCodeExistsAsync(
            string partnerCode)
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            CASE
                WHEN EXISTS
                (
                    SELECT 1

                    FROM dbo.Partners

                    WHERE PartnerCode =
                          @PartnerCode
                )
                THEN 1
                ELSE 0
            END;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .ExecuteScalarAsync<bool>(
                sql,
                new
                {
                    PartnerCode =
                        partnerCode.Trim()
                });
    }


    public async Task<int>
        CreatePartnerAsync(
            Partner partner)
    {
        EnsureSignedIn();


        partner.PartnerCode =
            partner.PartnerCode
                .Trim()
                .ToUpperInvariant();

        partner.PartnerName =
            partner.PartnerName.Trim();

        partner.CreatedBy =
            _currentUser.Username;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var exists =
            await connection
                .ExecuteScalarAsync<bool>(
                    """
                    SELECT
                        CASE
                            WHEN EXISTS
                            (
                                SELECT 1

                                FROM dbo.Partners

                                WHERE PartnerCode =
                                      @PartnerCode
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        partner.PartnerCode
                    });


        if (exists)
        {
            throw new
                InvalidOperationException(
                    "Partner code already exists.");
        }


        const string sql = """
        INSERT INTO dbo.Partners
        (
            PartnerCode,
            PartnerName,
            Address,
            Email,
            PhoneNumber,
            IsActive,
            CreatedDate,
            CreatedBy
        )
        VALUES
        (
            @PartnerCode,
            @PartnerName,
            @Address,
            @Email,
            @PhoneNumber,
            @IsActive,
            SYSUTCDATETIME(),
            @CreatedBy
        );

        SELECT
            CAST(
                SCOPE_IDENTITY()
                AS INT
            );
        """;


        return await connection
            .ExecuteScalarAsync<int>(
                sql,
                partner);
    }


    // ==========================================================
    // BRANCHES
    // ==========================================================

    public async Task<IReadOnlyList<BranchOption>>
        GetActiveBranchesAsync(
            int partnerId)
    {
        EnsureSignedIn();


        if (partnerId <= 0)
        {
            return Array.Empty<BranchOption>();
        }


        const string sql = """
        SELECT
            BranchId,
            PartnerId,
            BranchName

        FROM dbo.PartnerBranches

        WHERE PartnerId =
              @PartnerId

        AND IsActive = 1

        ORDER BY BranchName;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return (
            await connection
                .QueryAsync<BranchOption>(
                    sql,
                    new
                    {
                        PartnerId =
                            partnerId
                    })
        ).AsList();
    }


    public async Task<bool>
        BranchNameExistsAsync(
            int partnerId,
            string branchName)
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            CASE
                WHEN EXISTS
                (
                    SELECT 1

                    FROM dbo.PartnerBranches

                    WHERE PartnerId =
                          @PartnerId

                    AND BranchName =
                        @BranchName
                )
                THEN 1
                ELSE 0
            END;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .ExecuteScalarAsync<bool>(
                sql,
                new
                {
                    PartnerId =
                        partnerId,

                    BranchName =
                        branchName.Trim()
                });
    }


    public async Task<int>
        CreateBranchAsync(
            PartnerBranch branch)
    {
        EnsureSignedIn();


        if (branch.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Partner is required.");
        }


        branch.BranchName =
            branch.BranchName.Trim();

        branch.CreatedBy =
            _currentUser.Username;


        using var connection =
            _connectionFactory
                .CreateConnection();


        // ------------------------------------------
        // Verify Partner
        // ------------------------------------------

        var partnerExists =
            await connection
                .ExecuteScalarAsync<bool>(
                    """
                    SELECT
                        CASE
                            WHEN EXISTS
                            (
                                SELECT 1

                                FROM dbo.Partners

                                WHERE PartnerId =
                                      @PartnerId

                                AND IsActive = 1
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        branch.PartnerId
                    });


        if (!partnerExists)
        {
            throw new
                InvalidOperationException(
                    "Selected partner is invalid.");
        }


        // ------------------------------------------
        // Duplicate branch
        // ------------------------------------------

        var branchExists =
            await connection
                .ExecuteScalarAsync<bool>(
                    """
                    SELECT
                        CASE
                            WHEN EXISTS
                            (
                                SELECT 1

                                FROM dbo.PartnerBranches

                                WHERE PartnerId =
                                      @PartnerId

                                AND BranchName =
                                    @BranchName
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        branch.PartnerId,
                        branch.BranchName
                    });


        if (branchExists)
        {
            throw new
                InvalidOperationException(
                    "This branch already exists for the selected partner.");
        }


        const string sql = """
        INSERT INTO dbo.PartnerBranches
        (
            PartnerId,
            BranchName,
            Address,
            Email,
            PhoneNumber,
            IsActive,
            CreatedDate,
            CreatedBy
        )
        VALUES
        (
            @PartnerId,
            @BranchName,
            @Address,
            @Email,
            @PhoneNumber,
            @IsActive,
            SYSUTCDATETIME(),
            @CreatedBy
        );

        SELECT
            CAST(
                SCOPE_IDENTITY()
                AS INT
            );
        """;


        return await connection
            .ExecuteScalarAsync<int>(
                sql,
                branch);
    }


    // ==========================================================
    // USERS LIST
    // ==========================================================

    public async Task<UserPageResult>
        GetPageAsync(
            int? partnerId,
            int? branchId,
            int pageNumber,
            int pageSize)
    {
        EnsureSignedIn();


        pageNumber =
            Math.Max(
                1,
                pageNumber);


        pageSize =
            Math.Clamp(
                pageSize,
                1,
                50);


        const string sql = """
        SELECT
            COUNT(1) AS TotalUsers,

            COALESCE(
                SUM(
                    CASE
                        WHEN U.IsActive = 1
                        THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS ActiveUsers,

            COALESCE(
                SUM(
                    CASE
                        WHEN U.IsActive = 0
                        THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS InactiveUsers

        FROM dbo.Users U

        WHERE
            (
                @PartnerId IS NULL
                OR
                U.PartnerId =
                @PartnerId
            )

        AND
            (
                @BranchId IS NULL
                OR
                U.BranchId =
                @BranchId
            );


        SELECT
            U.UserId,

            U.PartnerId,

            P.PartnerCode,

            P.PartnerName,

            U.BranchId,

            B.BranchName,

            U.Username,

            COALESCE
            (
                STUFF
                (
                    (
                        SELECT
                            ', ' +
                            R.RoleName

                        FROM dbo.UserRoles UR

                        INNER JOIN dbo.Roles R
                            ON R.RoleId =
                               UR.RoleId

                        WHERE UR.UserId =
                              U.UserId

                        AND R.IsActive = 1

                        ORDER BY R.RoleName

                        FOR XML PATH(''),
                        TYPE

                    ).value(
                        '.',
                        'nvarchar(max)'
                    ),

                    1,
                    2,
                    ''
                ),
                ''
            ) AS RoleName,

            U.IsActive,

            U.LastLoginDate,

            U.CreatedDate

        FROM dbo.Users U

        INNER JOIN dbo.Partners P
            ON P.PartnerId =
               U.PartnerId

        INNER JOIN dbo.PartnerBranches B
            ON B.PartnerId =
               U.PartnerId

            AND B.BranchId =
                U.BranchId

        WHERE
            (
                @PartnerId IS NULL
                OR
                U.PartnerId =
                @PartnerId
            )

        AND
            (
                @BranchId IS NULL
                OR
                U.BranchId =
                @BranchId
            )

        ORDER BY
            P.PartnerName,
            B.BranchName,
            U.CreatedDate DESC

        OFFSET @Offset ROWS

        FETCH NEXT @PageSize
        ROWS ONLY;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        using var multi =
            await connection
                .QueryMultipleAsync(
                    sql,
                    new
                    {
                        PartnerId =
                            partnerId,

                        BranchId =
                            branchId,

                        Offset =
                            (pageNumber - 1)
                            * pageSize,

                        PageSize =
                            pageSize
                    });


        var summary =
            await multi
                .ReadSingleAsync<
                    UserSummaryRow>();


        var users =
            (
                await multi
                    .ReadAsync<
                        UserListItem>()
            ).AsList();


        return new UserPageResult
        {
            Items =
                users,

            TotalUsers =
                summary.TotalUsers,

            ActiveUsers =
                summary.ActiveUsers,

            InactiveUsers =
                summary.InactiveUsers,

            PageNumber =
                pageNumber,

            PageSize =
                pageSize
        };
    }


    // ==========================================================
    // ROLES
    // ==========================================================

    public async Task<IReadOnlyList<RoleOption>>
        GetActiveRolesAsync()
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            RoleId,
            RoleName

        FROM dbo.Roles

        WHERE IsActive = 1

        ORDER BY RoleName;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return (
            await connection
                .QueryAsync<RoleOption>(
                    sql)
        ).AsList();
    }


    // ==========================================================
    // USERNAME
    // ==========================================================

    public async Task<bool>
        UsernameExistsAsync(
            string username)
    {
        EnsureSignedIn();


        const string sql = """
        SELECT
            CASE
                WHEN EXISTS
                (
                    SELECT 1

                    FROM dbo.Users

                    WHERE Username =
                          @Username
                )
                THEN 1
                ELSE 0
            END;
        """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .ExecuteScalarAsync<bool>(
                sql,
                new
                {
                    Username =
                        username.Trim()
                });
    }


    // ==========================================================
    // CREATE USER
    // ==========================================================

    public async Task<long>
        CreateAsync(
            User user,
            IReadOnlyCollection<int> roleIds)
    {
        EnsureSignedIn();


        var selectedRoleIds =
            roleIds
                .Where(
                    x => x > 0)
                .Distinct()
                .ToArray();


        if (selectedRoleIds.Length == 0)
        {
            throw new ArgumentException(
                "Select at least one role.");
        }


        if (user.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Partner is required.");
        }


        if (user.BranchId <= 0)
        {
            throw new ArgumentException(
                "Branch is required.");
        }


        user.Username =
            user.Username.Trim();

        user.CreatedBy =
            _currentUser.Username;


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
            // --------------------------------------
            // Verify Partner
            // --------------------------------------

            var partnerValid =
                await connection
                    .ExecuteScalarAsync<bool>(
                        """
                        SELECT
                            CASE
                                WHEN EXISTS
                                (
                                    SELECT 1

                                    FROM dbo.Partners

                                    WHERE PartnerId =
                                          @PartnerId

                                    AND IsActive = 1
                                )
                                THEN 1
                                ELSE 0
                            END;
                        """,
                        new
                        {
                            user.PartnerId
                        },
                        transaction);


            if (!partnerValid)
            {
                throw new
                    InvalidOperationException(
                        "Invalid partner.");
            }


            // --------------------------------------
            // Verify Branch belongs to Partner
            // --------------------------------------

            var branchValid =
                await connection
                    .ExecuteScalarAsync<bool>(
                        """
                        SELECT
                            CASE
                                WHEN EXISTS
                                (
                                    SELECT 1

                                    FROM dbo.PartnerBranches

                                    WHERE PartnerId =
                                          @PartnerId

                                    AND BranchId =
                                        @BranchId

                                    AND IsActive = 1
                                )
                                THEN 1
                                ELSE 0
                            END;
                        """,
                        new
                        {
                            user.PartnerId,
                            user.BranchId
                        },
                        transaction);


            if (!branchValid)
            {
                throw new
                    InvalidOperationException(
                        "The selected branch does not belong to the selected partner.");
            }


            // --------------------------------------
            // Username
            // --------------------------------------

            var usernameExists =
                await connection
                    .ExecuteScalarAsync<bool>(
                        """
                        SELECT
                            CASE
                                WHEN EXISTS
                                (
                                    SELECT 1

                                    FROM dbo.Users

                                    WHERE Username =
                                          @Username
                                )
                                THEN 1
                                ELSE 0
                            END;
                        """,
                        new
                        {
                            user.Username
                        },
                        transaction);


            if (usernameExists)
            {
                throw new
                    InvalidOperationException(
                        "Username already exists.");
            }


            // --------------------------------------
            // Validate roles
            // --------------------------------------

            var validRoleCount =
                await connection
                    .ExecuteScalarAsync<int>(
                        """
                        SELECT
                            COUNT(1)

                        FROM dbo.Roles

                        WHERE RoleId
                              IN @RoleIds

                        AND IsActive = 1;
                        """,
                        new
                        {
                            RoleIds =
                                selectedRoleIds
                        },
                        transaction);


            if (validRoleCount !=
                selectedRoleIds.Length)
            {
                throw new
                    InvalidOperationException(
                        "One or more roles are invalid.");
            }


            // --------------------------------------
            // Insert user
            // --------------------------------------

            var userId =
                await connection
                    .ExecuteScalarAsync<long>(
                        """
                        INSERT INTO dbo.Users
                        (
                            PartnerId,
                            BranchId,
                            Username,
                            PasswordHash,
                            Address,
                            Email,
                            PhoneNumber,
                            IsActive,
                            FailedLoginCount,
                            CreatedDate,
                            CreatedBy
                        )
                        VALUES
                        (
                            @PartnerId,
                            @BranchId,
                            @Username,
                            @PasswordHash,
                            @Address,
                            @Email,
                            @PhoneNumber,
                            @IsActive,
                            0,
                            SYSUTCDATETIME(),
                            @CreatedBy
                        );

                        SELECT
                            CAST(
                                SCOPE_IDENTITY()
                                AS BIGINT
                            );
                        """,
                        user,
                        transaction);


            // --------------------------------------
            // User roles
            // --------------------------------------

            foreach (
                var roleId
                in selectedRoleIds)
            {
                await connection
                    .ExecuteAsync(
                        """
                        INSERT INTO dbo.UserRoles
                        (
                            UserId,
                            RoleId,
                            CreatedDate,
                            CreatedBy
                        )
                        VALUES
                        (
                            @UserId,
                            @RoleId,
                            SYSUTCDATETIME(),
                            @CreatedBy
                        );
                        """,
                        new
                        {
                            UserId =
                                userId,

                            RoleId =
                                roleId,

                            CreatedBy =
                                _currentUser.Username
                        },
                        transaction);
            }


            transaction.Commit();


            return userId;
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }


    // ==========================================================
    // AUTH CHECK
    // ==========================================================

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


    private sealed class UserSummaryRow
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        public int InactiveUsers { get; set; }
    }
}