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


    // =========================================================
    // PARTNER LIST
    // =========================================================

    public async Task<PartnerPageResult>
        GetPartnersAsync(
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


        var offset =
            (pageNumber - 1)
            * pageSize;


        const string sql = """
            SELECT
                COUNT(1) AS TotalCount,

                COALESCE(
                    SUM(
                        CASE
                            WHEN P.IsActive = 1
                            THEN 1
                            ELSE 0
                        END
                    ),
                    0
                ) AS ActiveCount,

                COALESCE(
                    SUM(
                        CASE
                            WHEN P.IsActive = 0
                            THEN 1
                            ELSE 0
                        END
                    ),
                    0
                ) AS InactiveCount,

                (
                    SELECT COUNT(1)
                    FROM dbo.Users
                ) AS TotalUsers

            FROM dbo.Partners P;


            SELECT
                P.*,

                (
                    SELECT COUNT(1)

                    FROM dbo.PartnerBranches B

                    WHERE B.PartnerId =
                          P.PartnerId
                ) AS BranchCount,

                (
                    SELECT COUNT(1)

                    FROM dbo.Users U

                    WHERE U.PartnerId =
                          P.PartnerId
                ) AS UserCount

            FROM dbo.Partners P

            ORDER BY
                P.CreatedDate DESC,
                P.PartnerId DESC

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
                        Offset =
                            offset,

                        PageSize =
                            pageSize
                    });


        var summary =
            await multi
                .ReadSingleAsync<
                    PartnerSummaryRow>();


        var partners =
            (
                await multi
                    .ReadAsync<
                        PartnerListItem>()
            ).AsList();


        return new PartnerPageResult
        {
            Items =
                partners,

            TotalCount =
                summary.TotalCount,

            ActiveCount =
                summary.ActiveCount,

            InactiveCount =
                summary.InactiveCount,

            TotalUsers =
                summary.TotalUsers,

            PageNumber =
                pageNumber,

            PageSize =
                pageSize
        };
    }


    // =========================================================
    // ACTIVE PARTNER OPTIONS
    // =========================================================

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

            ORDER BY
                PartnerName;
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


    // =========================================================
    // GET PARTNER
    // =========================================================

    public async Task<Partner?>
        GetPartnerByIdAsync(
            int partnerId)
    {
        EnsureSignedIn();


        if (partnerId <= 0)
        {
            return null;
        }


        const string sql = """
            SELECT
                P.*

            FROM dbo.Partners P

            WHERE P.PartnerId =
                  @PartnerId;
            """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .QueryFirstOrDefaultAsync<Partner>(
                sql,
                new
                {
                    PartnerId =
                        partnerId
                });
    }


    // =========================================================
    // PARTNER CODE EXISTS
    // =========================================================

    public async Task<bool>
        PartnerCodeExistsAsync(
            string partnerCode)
    {
        EnsureSignedIn();


        if (string.IsNullOrWhiteSpace(
            partnerCode))
        {
            return false;
        }


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
                        partnerCode
                            .Trim()
                            .ToUpperInvariant()
                });
    }


    // =========================================================
    // PARTNER CODE EXISTS - EDIT
    // =========================================================

    public async Task<bool>
        PartnerCodeExistsAsync(
            string partnerCode,
            int excludePartnerId)
    {
        EnsureSignedIn();


        if (string.IsNullOrWhiteSpace(
            partnerCode))
        {
            return false;
        }


        const string sql = """
            SELECT
                CASE
                    WHEN EXISTS
                    (
                        SELECT 1

                        FROM dbo.Partners

                        WHERE PartnerCode =
                              @PartnerCode

                        AND PartnerId <>
                            @ExcludePartnerId
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
                        partnerCode
                            .Trim()
                            .ToUpperInvariant(),

                    ExcludePartnerId =
                        excludePartnerId
                });
    }


    // =========================================================
    // CREATE PARTNER
    // =========================================================

    public async Task<int>
        CreatePartnerAsync(
            Partner partner)
    {
        EnsureSignedIn();


        ArgumentNullException
            .ThrowIfNull(
                partner);


        partner.PartnerCode =
            partner.PartnerCode
                .Trim()
                .ToUpperInvariant();


        partner.PartnerName =
            partner.PartnerName
                .Trim();


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
            throw new InvalidOperationException(
                "Partner code already exists.");
        }


        const string sql = """
            INSERT INTO dbo.Partners
            (
                PartnerCode,
                PartnerName,
                Address,
                     Country,
            City,
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
                    @Country,
            @City,
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


    // =========================================================
    // UPDATE PARTNER
    // =========================================================

    public async Task<bool>
        UpdatePartnerAsync(
            Partner partner)
    {
        EnsureSignedIn();


        ArgumentNullException
            .ThrowIfNull(
                partner);


        if (partner.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Invalid partner.",
                nameof(partner));
        }


        partner.PartnerCode =
            partner.PartnerCode
                .Trim()
                .ToUpperInvariant();


        partner.PartnerName =
            partner.PartnerName
                .Trim();


        using var connection =
            _connectionFactory
                .CreateConnection();


        var duplicate =
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

                                AND PartnerId <>
                                    @PartnerId
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        partner.PartnerCode,
                        partner.PartnerId
                    });


        if (duplicate)
        {
            throw new InvalidOperationException(
                "Partner code already exists.");
        }


        const string sql = """
            UPDATE dbo.Partners

            SET
                PartnerCode =
                    @PartnerCode,

                PartnerName =
                    @PartnerName,

                Address =
                    @Address,
                        Country = @Country,
            City = @City,

                Email =
                    @Email,

                PhoneNumber =
                    @PhoneNumber,

                IsActive =
                    @IsActive,

                UpdatedDate =
                    SYSUTCDATETIME(),

                UpdatedBy =
                    @UpdatedBy

            WHERE PartnerId =
                  @PartnerId;
            """;


        var affectedRows =
            await connection
                .ExecuteAsync(
                    sql,
                    new
                    {
                        partner.PartnerId,
                        partner.PartnerCode,
                        partner.PartnerName,
                        partner.Address,
                        partner.Country,
                        partner.City,
                        partner.Email,
                        partner.PhoneNumber,
                        partner.IsActive,

                        UpdatedBy =
                            _currentUser.Username
                    });


        return affectedRows == 1;
    }


    // =========================================================
    // ACTIVE BRANCH OPTIONS
    // =========================================================

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

            ORDER BY
                BranchName;
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


    // =========================================================
    // BRANCH LIST BY PARTNER
    // =========================================================

    public async Task<IReadOnlyList<BranchListItem>>
        GetBranchesByPartnerAsync(
            int partnerId)
    {
        EnsureSignedIn();


        if (partnerId <= 0)
        {
            return Array.Empty<BranchListItem>();
        }


        const string sql = """
            SELECT
                B.BranchId,
                B.PartnerId,
                B.BranchName,
                B.Address,
                B.Email,
                B.PhoneNumber,
                B.IsActive,
                B.CreatedDate,

                (
                    SELECT COUNT(1)

                    FROM dbo.Users U

                    WHERE U.PartnerId =
                          B.PartnerId

                    AND U.BranchId =
                        B.BranchId
                ) AS UserCount

            FROM dbo.PartnerBranches B

            WHERE B.PartnerId =
                  @PartnerId

            ORDER BY
                B.BranchName,
                B.BranchId;
            """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return (
            await connection
                .QueryAsync<BranchListItem>(
                    sql,
                    new
                    {
                        PartnerId =
                            partnerId
                    })
        ).AsList();
    }


    // =========================================================
    // GET BRANCH
    // =========================================================

    public async Task<PartnerBranch?>
        GetBranchByIdAsync(
            int branchId)
    {
        EnsureSignedIn();


        if (branchId <= 0)
        {
            return null;
        }


        const string sql = """
            SELECT
                B.*

            FROM dbo.PartnerBranches B

            WHERE B.BranchId =
                  @BranchId;
            """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .QueryFirstOrDefaultAsync<PartnerBranch>(
                sql,
                new
                {
                    BranchId =
                        branchId
                });
    }


    // =========================================================
    // BRANCH NAME EXISTS
    // =========================================================

    public async Task<bool>
        BranchNameExistsAsync(
            int partnerId,
            string branchName)
    {
        EnsureSignedIn();


        if (partnerId <= 0 ||
            string.IsNullOrWhiteSpace(
                branchName))
        {
            return false;
        }


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


    // =========================================================
    // BRANCH NAME EXISTS - EDIT
    // =========================================================

    public async Task<bool>
        BranchNameExistsAsync(
            int partnerId,
            string branchName,
            int excludeBranchId)
    {
        EnsureSignedIn();


        if (partnerId <= 0 ||
            string.IsNullOrWhiteSpace(
                branchName))
        {
            return false;
        }


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

                        AND BranchId <>
                            @ExcludeBranchId
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
                        branchName.Trim(),

                    ExcludeBranchId =
                        excludeBranchId
                });
    }


    // =========================================================
    // CREATE BRANCH
    // =========================================================

    public async Task<int>
        CreateBranchAsync(
            PartnerBranch branch)
    {
        EnsureSignedIn();


        ArgumentNullException
            .ThrowIfNull(
                branch);


        if (branch.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Partner is required.");
        }


        if (string.IsNullOrWhiteSpace(
            branch.BranchName))
        {
            throw new ArgumentException(
                "Branch name is required.");
        }


        branch.BranchName =
            branch.BranchName.Trim();


        branch.CreatedBy =
            _currentUser.Username;


        using var connection =
            _connectionFactory
                .CreateConnection();


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
            throw new InvalidOperationException(
                "Selected partner is invalid.");
        }


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
            throw new InvalidOperationException(
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


    // =========================================================
    // UPDATE BRANCH
    // =========================================================

    public async Task<bool>
        UpdateBranchAsync(
            PartnerBranch branch)
    {
        EnsureSignedIn();


        ArgumentNullException
            .ThrowIfNull(
                branch);


        if (branch.BranchId <= 0 ||
            branch.PartnerId <= 0)
        {
            throw new ArgumentException(
                "Invalid branch.");
        }


        if (string.IsNullOrWhiteSpace(
            branch.BranchName))
        {
            throw new ArgumentException(
                "Branch name is required.");
        }


        branch.BranchName =
            branch.BranchName.Trim();


        using var connection =
            _connectionFactory
                .CreateConnection();


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

                                WHERE BranchId =
                                      @BranchId

                                AND PartnerId =
                                    @PartnerId
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        branch.BranchId,
                        branch.PartnerId
                    });


        if (!branchExists)
        {
            return false;
        }


        var duplicateName =
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

                                AND BranchId <>
                                    @BranchId
                            )
                            THEN 1
                            ELSE 0
                        END;
                    """,
                    new
                    {
                        branch.PartnerId,
                        branch.BranchName,
                        branch.BranchId
                    });


        if (duplicateName)
        {
            throw new InvalidOperationException(
                "This branch already exists for the selected partner.");
        }


        const string sql = """
            UPDATE dbo.PartnerBranches

            SET
                BranchName =
                    @BranchName,

                Address =
                    @Address,

                Email =
                    @Email,

                PhoneNumber =
                    @PhoneNumber,

                IsActive =
                    @IsActive,

                UpdatedDate =
                    SYSUTCDATETIME(),

                UpdatedBy =
                    @UpdatedBy

            WHERE BranchId =
                  @BranchId

            AND PartnerId =
                @PartnerId;
            """;


        var affectedRows =
            await connection
                .ExecuteAsync(
                    sql,
                    new
                    {
                        branch.BranchId,
                        branch.PartnerId,
                        branch.BranchName,
                        branch.Address,
                        branch.Email,
                        branch.PhoneNumber,
                        branch.IsActive,

                        UpdatedBy =
                            _currentUser.Username
                    });


        return affectedRows == 1;
    }


    // =========================================================
    // USER LIST
    // =========================================================

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
                U.Address,
            	U.Email,
            	U.PhoneNumber,

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

                            ORDER BY
                                R.RoleName

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
                U.CreatedDate DESC,
                U.UserId DESC

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


    // =========================================================
    // ACTIVE ROLES
    // =========================================================

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

            ORDER BY
                RoleName;
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


    // =========================================================
    // USERNAME EXISTS
    // =========================================================

    public async Task<bool>
        UsernameExistsAsync(
            string username)
    {
        EnsureSignedIn();


        if (string.IsNullOrWhiteSpace(
            username))
        {
            return false;
        }


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


    // =========================================================
    // CREATE USER
    // =========================================================

    public async Task<long>
        CreateAsync(
            User user,
            IReadOnlyCollection<int> roleIds)
    {
        EnsureSignedIn();


        ArgumentNullException
            .ThrowIfNull(
                user);


        ArgumentNullException
            .ThrowIfNull(
                roleIds);


        var selectedRoleIds =
            roleIds
                .Where(
                    roleId =>
                        roleId > 0)
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


        if (string.IsNullOrWhiteSpace(
            user.Username))
        {
            throw new ArgumentException(
                "Username is required.");
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
                throw new InvalidOperationException(
                    "Invalid partner.");
            }


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
                throw new InvalidOperationException(
                    "The selected branch does not belong to the selected partner.");
            }


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
                throw new InvalidOperationException(
                    "Username already exists.");
            }


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
                throw new InvalidOperationException(
                    "One or more roles are invalid.");
            }


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


            foreach (var roleId in
                     selectedRoleIds)
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


    // =========================================================
    // USER ACCESS
    // =========================================================

    public async Task<UserAccessInfo?>
        GetAccessAsync(
            long userId)
    {
        EnsureSignedIn();


        if (userId <= 0)
        {
            return null;
        }


        const string sql = """
            SELECT
                U.UserId,
                U.PartnerId,
                P.PartnerName,
                U.BranchId,
                B.BranchName,
                U.Username,
                U.IsActive

            FROM dbo.Users U

            INNER JOIN dbo.Partners P
                ON P.PartnerId =
                   U.PartnerId

            INNER JOIN dbo.PartnerBranches B
                ON B.PartnerId =
                   U.PartnerId

                AND B.BranchId =
                    U.BranchId

            WHERE U.UserId =
                  @UserId;
            """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        return await connection
            .QueryFirstOrDefaultAsync<UserAccessInfo>(
                sql,
                new
                {
                    UserId =
                        userId
                });
    }


    // =========================================================
    // USER ROLE IDS
    // =========================================================

    public async Task<IReadOnlyList<int>>
        GetUserRoleIdsAsync(
            long userId)
    {
        EnsureSignedIn();


        if (userId <= 0)
        {
            return Array.Empty<int>();
        }


        const string sql = """
            SELECT
                UR.RoleId

            FROM dbo.UserRoles UR

            INNER JOIN dbo.Roles R
                ON R.RoleId =
                   UR.RoleId

            WHERE UR.UserId =
                  @UserId

            AND R.IsActive = 1

            ORDER BY
                R.RoleName;
            """;


        using var connection =
            _connectionFactory
                .CreateConnection();


        var rows =
            await connection
                .QueryAsync<int>(
                    sql,
                    new
                    {
                        UserId =
                            userId
                    });


        return rows.AsList();
    }


    // =========================================================
    // UPDATE USER ACCESS
    // =========================================================

    public async Task UpdateAccessAsync(
    long userId,
    bool isActive,
    IReadOnlyCollection<int> roleIds)
    {
        EnsureSignedIn();

        if (userId <= 0)
            throw new ArgumentException("Invalid user.", nameof(userId));

        var selectedRoleIds = (roleIds ?? Array.Empty<int>())
            .Distinct()
            .ToArray();

        if (selectedRoleIds.Length == 0 ||
            selectedRoleIds.Any(id => id <= 0))
        {
            throw new ArgumentException(
                "Select at least one valid role.",
                nameof(roleIds));
        }

        using var connection = _connectionFactory.CreateConnection();

        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();

        var existingUser = await connection.QuerySingleOrDefaultAsync<User>(
            """
        SELECT UserId, IsActive
        FROM dbo.Users WITH (UPDLOCK, HOLDLOCK)
        WHERE UserId = @UserId;
        """,
            new { UserId = userId },
            transaction);

        if (existingUser is null)
            throw new InvalidOperationException("User was not found.");

        var existingRoleIds = (
            await connection.QueryAsync<int>(
                """
            SELECT RoleId
            FROM dbo.UserRoles
            WHERE UserId = @UserId;
            """,
                new { UserId = userId },
                transaction))
            .ToHashSet();

        var rolesChanged = !existingRoleIds.SetEquals(selectedRoleIds);
        var statusChanged = existingUser.IsActive != isActive;

        if (userId == _currentUser.UserId &&
            (rolesChanged || statusChanged))
        {
            throw new InvalidOperationException(
                "Another administrator must change your roles or account status.");
        }

        var validRoleCount = await connection.ExecuteScalarAsync<int>(
            """
        SELECT COUNT(1)
        FROM dbo.Roles
        WHERE RoleId IN @RoleIds
          AND IsActive = 1;
        """,
            new { RoleIds = selectedRoleIds },
            transaction);

        if (validRoleCount != selectedRoleIds.Length)
        {
            throw new InvalidOperationException(
                "One or more selected roles are invalid or inactive.");
        }

        if (!rolesChanged && !statusChanged)
        {
            transaction.Commit();
            return;
        }

        await connection.ExecuteAsync(
            """
        UPDATE dbo.Users
        SET
            IsActive = @IsActive,
            UpdatedDate = SYSUTCDATETIME(),
            UpdatedBy = @UpdatedBy
        WHERE UserId = @UserId;

        DELETE FROM dbo.UserRoles
        WHERE UserId = @UserId;
        """,
            new
            {
                UserId = userId,
                IsActive = isActive,
                UpdatedBy = _currentUser.Username
            },
            transaction);

        foreach (var roleId in selectedRoleIds)
        {
            await connection.ExecuteAsync(
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
                    UserId = userId,
                    RoleId = roleId,
                    CreatedBy = _currentUser.Username
                },
                transaction);
        }

        // Existing cookies contain the old role/permission claims.
        // Ending sessions makes the user sign in with fresh claims.
        await connection.ExecuteAsync(
            """
        UPDATE dbo.UserSessions
        SET
            IsActive = 0,
            EndedAtUtc = SYSUTCDATETIME(),
            EndReason = 'Access changed'
        WHERE UserId = @UserId
          AND IsActive = 1;
        """,
            new { UserId = userId },
            transaction);

        transaction.Commit();
    }

    public async Task<User?> GetUserByIdAsync(long userId)
    {
        EnsureSignedIn();

        if (userId <= 0)
            return null;

        using var connection = _connectionFactory.CreateConnection();

        const string sql = """
        SELECT
            UserId,
            PartnerId,
            BranchId,
            Username,
            Address,
            Email,
            PhoneNumber,
            IsActive
        FROM dbo.Users
        WHERE UserId = @UserId;
        """;

        return await connection.QuerySingleOrDefaultAsync<User>(
            sql,
            new { UserId = userId });
    }

    public async Task<bool> UpdateUserProfileAsync(
        long userId,
        string? address,
        string? email,
        string? phoneNumber,
        bool isActive)
    {
        EnsureSignedIn();

        if (userId <= 0)
            throw new ArgumentException("Invalid user.", nameof(userId));

        if (userId == _currentUser.UserId && !isActive)
            throw new InvalidOperationException(
                "You cannot deactivate your own account.");

        using var connection = _connectionFactory.CreateConnection();

        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var transaction = connection.BeginTransaction();

        const string updateSql = """
        UPDATE dbo.Users
        SET
            Address = @Address,
            Email = @Email,
            PhoneNumber = @PhoneNumber,
            IsActive = @IsActive,
            UpdatedDate = SYSUTCDATETIME(),
            UpdatedBy = @UpdatedBy
        WHERE UserId = @UserId;
        """;

        var affected = await connection.ExecuteAsync(
            updateSql,
            new
            {
                UserId = userId,
                Address = address,
                Email = email,
                PhoneNumber = phoneNumber,
                IsActive = isActive,
                UpdatedBy = _currentUser.Username
            },
            transaction);

        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        if (!isActive)
        {
            await connection.ExecuteAsync(
                """
            UPDATE dbo.UserSessions
            SET
                IsActive = 0,
                EndedAtUtc = SYSUTCDATETIME(),
                EndReason = 'Account deactivated'
            WHERE UserId = @UserId
              AND IsActive = 1;
            """,
                new { UserId = userId },
                transaction);
        }

        transaction.Commit();
        return true;
    }



    // =========================================================
    // AUTH CHECK
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


    // =========================================================
    // INTERNAL SUMMARY TYPES
    // =========================================================

    private sealed class PartnerSummaryRow
    {
        public int TotalCount { get; set; }

        public int ActiveCount { get; set; }

        public int InactiveCount { get; set; }

        public int TotalUsers { get; set; }
    }


    private sealed class UserSummaryRow
    {
        public int TotalUsers { get; set; }

        public int ActiveUsers { get; set; }

        public int InactiveUsers { get; set; }
    }
}