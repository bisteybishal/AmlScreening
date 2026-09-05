/*
    UAT stabilization for the existing AMLScreening database.

    DBA instructions:
    1. Back up the UAT database.
    2. Review this script against the target schema.
    3. Run it manually in SQL Server Management Studio.

    The portal does not execute this script automatically.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AmlRecords', N'U') IS NULL
        THROW 50200, 'dbo.AmlRecords does not exist.', 1;

    IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
        THROW 50202, 'Run MenuItems.sql before UAT-Stabilization.sql.', 1;

    IF COL_LENGTH(N'dbo.AmlRecords', N'LastSyncDate') IS NULL OR
       COL_LENGTH(N'dbo.AmlRecords', N'SourceUpdatedDate') IS NULL
        THROW 50203, 'AmlRecords is missing live-source synchronization columns.', 1;

    IF COL_LENGTH(N'dbo.AmlRecords', N'IsShared') IS NULL
    BEGIN
        ALTER TABLE dbo.AmlRecords
            ADD IsShared BIT NOT NULL
                CONSTRAINT DF_AmlRecords_IsShared DEFAULT (0) WITH VALUES;
    END;

    /* Existing synchronized UN/EU data is reference data and is client-visible. */
    UPDATE dbo.AmlRecords
    SET IsShared = 1
    WHERE SourceName IN (N'UN Sanctions', N'EU Sanctions');

    IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteDate') IS NULL
        ALTER TABLE dbo.AmlRecords ADD DeleteDate DATETIME2(0) NULL;

    IF COL_LENGTH(N'dbo.AmlRecords', N'DeletedDate') IS NOT NULL
    BEGIN
        EXEC(N'
            UPDATE dbo.AmlRecords
            SET DeleteDate = COALESCE(DeleteDate, CONVERT(DATETIME2(0), DeletedDate))
            WHERE DeletedDate IS NOT NULL;');
    END;

    IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteBy') IS NULL
        ALTER TABLE dbo.AmlRecords ADD DeleteBy NVARCHAR(100) NULL;

    IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteFg') IS NULL
    BEGIN
        ALTER TABLE dbo.AmlRecords
            ADD DeleteFg CHAR(1) NOT NULL
                CONSTRAINT DF_AmlRecords_DeleteFg DEFAULT ('N') WITH VALUES;
    END;

    /*
       Older UN/EU imports allowed NULL text values. EF Core treats these
       domain fields as required, so normalize legacy rows before the portal
       reads them.
    */
    UPDATE dbo.AmlRecords
    SET Country = COALESCE(Country, N''),
        Nationality = COALESCE(Nationality, N''),
        Remarks = COALESCE(Remarks, N''),
        DeleteFg = COALESCE(DeleteFg, 'N');

    IF EXISTS
    (
        SELECT 1
        FROM dbo.AmlRecords
        WHERE RecordType IS NULL
           OR FullName IS NULL
           OR ReferenceNo IS NULL
           OR SourceName IS NULL
    )
        THROW 50204,
            'Critical AML identity data is null. Repair or quarantine those rows before continuing.',
            1;

    IF COL_LENGTH(N'dbo.AmlRecords', N'CreatedBy') IS NOT NULL
        ALTER TABLE dbo.AmlRecords ALTER COLUMN CreatedBy NVARCHAR(100) NULL;

    IF COL_LENGTH(N'dbo.AmlRecords', N'UpdatedBy') IS NOT NULL
        ALTER TABLE dbo.AmlRecords ALTER COLUMN UpdatedBy NVARCHAR(100) NULL;

    ALTER TABLE dbo.AmlRecords ALTER COLUMN DeleteBy NVARCHAR(100) NULL;
    ALTER TABLE dbo.AmlRecords ALTER COLUMN Remarks NVARCHAR(MAX) NOT NULL;

    IF EXISTS
    (
        SELECT 1 FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.AmlRecords')
          AND name = N'UQ_AmlRecords_Client_Reference'
    )
        ALTER TABLE dbo.AmlRecords DROP CONSTRAINT UQ_AmlRecords_Client_Reference;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AmlRecords')
          AND name = N'UX_AmlRecords_Client_Reference'
    )
        DROP INDEX UX_AmlRecords_Client_Reference ON dbo.AmlRecords;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AmlRecords')
          AND name = N'UX_AmlRecords_Client_Source_Reference'
    )
        CREATE UNIQUE INDEX UX_AmlRecords_Client_Source_Reference
            ON dbo.AmlRecords (ClientId, SourceName, ReferenceNo);

    IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL OR
       OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL OR
       OBJECT_ID(N'dbo.UserPermissions', N'U') IS NULL
        THROW 50201, 'Run RolesPermissions.sql before UAT-Stabilization.sql.', 1;

    DECLARE @ObsoletePermissions TABLE
    (
        PermissionName VARCHAR(100) PRIMARY KEY
    );

    INSERT INTO @ObsoletePermissions (PermissionName)
    VALUES
        ('Customers.Create'),
        ('Customers.Edit'),
        ('Customers.Delete'),
        ('Transactions.Create'),
        ('Transactions.Evaluate'),
        ('SearchScreening.View'),
        ('Reports.View'),
        ('Settings.View');

    DELETE userPermission
    FROM dbo.UserPermissions userPermission
    INNER JOIN dbo.Permissions permission
        ON permission.PermissionId = userPermission.PermissionId
    INNER JOIN @ObsoletePermissions obsolete
        ON obsolete.PermissionName = permission.PermissionName;

    DELETE rolePermission
    FROM dbo.RolePermissions rolePermission
    INNER JOIN dbo.Permissions permission
        ON permission.PermissionId = rolePermission.PermissionId
    INNER JOIN @ObsoletePermissions obsolete
        ON obsolete.PermissionName = permission.PermissionName;

    UPDATE permission
    SET IsActive = 0
    FROM dbo.Permissions permission
    INNER JOIN @ObsoletePermissions obsolete
        ON obsolete.PermissionName = permission.PermissionName;

    /*
       UserPermissions stores exceptions only:
       - remove a direct grant already supplied by a role;
       - remove a direct deny when no role supplies that permission.
    */
    DELETE userPermission
    FROM dbo.UserPermissions userPermission
    WHERE
        (userPermission.IsGranted = 1 AND EXISTS
        (
            SELECT 1
            FROM dbo.UserRoles userRole
            INNER JOIN dbo.RolePermissions rolePermission
                ON rolePermission.RoleId = userRole.RoleId
            WHERE userRole.UserId = userPermission.UserId
              AND rolePermission.PermissionId = userPermission.PermissionId
        ))
        OR
        (userPermission.IsGranted = 0 AND NOT EXISTS
        (
            SELECT 1
            FROM dbo.UserRoles userRole
            INNER JOIN dbo.RolePermissions rolePermission
                ON rolePermission.RoleId = userRole.RoleId
            WHERE userRole.UserId = userPermission.UserId
              AND rolePermission.PermissionId = userPermission.PermissionId
        ));

    IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NOT NULL
    BEGIN
        DECLARE @CanonicalAmlSourceMenuId INT =
        (
            SELECT TOP (1) MenuId
            FROM dbo.MenuItems
            WHERE MenuKey IN ('AmlSource', 'AmlSources')
            ORDER BY CASE WHEN MenuKey = 'AmlSource' THEN 0 ELSE 1 END, MenuId
        );

        IF @CanonicalAmlSourceMenuId IS NULL
        BEGIN
            INSERT INTO dbo.MenuItems
                (ParentMenuId, MenuKey, DisplayName, SectionName, ControllerName,
                 ActionName, IconClass, PermissionCode, SortOrder, IsActive)
            VALUES
                (NULL, 'AmlSource', N'AML Sources', 'MAIN', 'AmlSource',
                 'Index', 'bi-database-check', 'AmlSource.View', 30, 1);

            SET @CanonicalAmlSourceMenuId = CONVERT(INT, SCOPE_IDENTITY());
        END;

        UPDATE dbo.MenuItems
        SET ParentMenuId = @CanonicalAmlSourceMenuId
        WHERE ParentMenuId IN
        (
            SELECT MenuId
            FROM dbo.MenuItems
            WHERE MenuKey IN ('AmlSource', 'AmlSources')
              AND MenuId <> @CanonicalAmlSourceMenuId
        );

        DELETE FROM dbo.MenuItems
        WHERE MenuKey IN ('AmlSource', 'AmlSources')
          AND MenuId <> @CanonicalAmlSourceMenuId;

        UPDATE dbo.MenuItems
        SET MenuKey = 'AmlSource',
            DisplayName = N'AML Sources',
            SectionName = 'MAIN',
            ControllerName = 'AmlSource',
            ActionName = 'Index',
            IconClass = 'bi-database-check',
            PermissionCode = 'AmlSource.View',
            SortOrder = 30,
            IsActive = 1
        WHERE MenuId = @CanonicalAmlSourceMenuId;
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AmlRecords')
          AND name = N'IX_AmlRecords_DashboardScope'
    )
    BEGIN
        CREATE INDEX IX_AmlRecords_DashboardScope
            ON dbo.AmlRecords
                (ClientId, DeleteFg, IsShared, OwnerUserId, CreatedDate DESC)
            INCLUDE (IsActive, RecordType, Country, ReferenceNo, FullName, SourceName);
    END;

    IF OBJECT_ID(N'dbo.AmlTransactions', N'U') IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AmlTransactions')
          AND name = N'IX_AmlTransactions_Dashboard'
    )
        CREATE INDEX IX_AmlTransactions_Dashboard
            ON dbo.AmlTransactions (ClientId, DeleteFg, Decision);

    IF OBJECT_ID(N'dbo.ComplianceAlerts', N'U') IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ComplianceAlerts')
          AND name = N'IX_ComplianceAlerts_Dashboard'
    )
        CREATE INDEX IX_ComplianceAlerts_Dashboard
            ON dbo.ComplianceAlerts (ClientId, DeleteFg, Status);

    IF OBJECT_ID(N'dbo.ComplianceCases', N'U') IS NOT NULL AND NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.ComplianceCases')
          AND name = N'IX_ComplianceCases_Dashboard'
    )
        CREATE INDEX IX_ComplianceCases_Dashboard
            ON dbo.ComplianceCases (ClientId, DeleteFg, Status);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

CREATE OR ALTER PROCEDURE dbo.spa_AML_Record_BulkInsert
    @RecordType NVARCHAR(50),
    @FullName NVARCHAR(200),
    @DateOfBirth DATE = NULL,
    @Country NVARCHAR(100) = N'',
    @Nationality NVARCHAR(100) = N'',
    @ReferenceNo NVARCHAR(100),
    @SourceName NVARCHAR(150),
    @Remarks NVARCHAR(MAX) = N'',
    @ClientId INT,
    @OwnerUserId BIGINT,
    @IsShared BIT = 1,
    @CreatedBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    UPDATE dbo.AmlRecords WITH (UPDLOCK, SERIALIZABLE)
    SET RecordType = @RecordType,
        FullName = @FullName,
        DateOfBirth = @DateOfBirth,
        Country = COALESCE(@Country, N''),
        Nationality = COALESCE(@Nationality, N''),
        Remarks = COALESCE(@Remarks, N''),
        IsActive = 1,
        IsShared = @IsShared,
        LastSyncDate = SYSUTCDATETIME(),
        UpdatedBy = @CreatedBy,
        UpdatedDate = SYSUTCDATETIME(),
        DeleteFg = 'N',
        DeleteDate = NULL,
        DeleteBy = NULL
    WHERE ClientId = @ClientId
      AND SourceName = @SourceName
      AND ReferenceNo = @ReferenceNo;

    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO dbo.AmlRecords
        (
            ClientId, OwnerUserId, IsShared, RecordType, FullName,
            DateOfBirth, Country, Nationality, ReferenceNo, SourceName,
            Remarks, IsActive, CreatedDate, CreatedBy, LastSyncDate,
            SourceUpdatedDate, DeleteFg
        )
        VALUES
        (
            @ClientId, @OwnerUserId, @IsShared, @RecordType, @FullName,
            @DateOfBirth, COALESCE(@Country, N''), COALESCE(@Nationality, N''),
            @ReferenceNo, @SourceName, COALESCE(@Remarks, N''), 1,
            SYSUTCDATETIME(), @CreatedBy, SYSUTCDATETIME(),
            SYSUTCDATETIME(), 'N'
        );
    END;
END;
GO

SELECT PermissionName, IsActive
FROM dbo.Permissions
ORDER BY PermissionName;

SELECT userPermission.UserId, permission.PermissionName, userPermission.IsGranted
FROM dbo.UserPermissions userPermission
INNER JOIN dbo.Permissions permission
    ON permission.PermissionId = userPermission.PermissionId
ORDER BY userPermission.UserId, permission.PermissionName;

SELECT MenuId, MenuKey, DisplayName, PermissionCode, SortOrder, IsActive
FROM dbo.MenuItems
WHERE MenuKey = 'AmlSource';

SELECT IsShared, SourceName, COUNT(*) AS RecordCount
FROM dbo.AmlRecords
GROUP BY IsShared, SourceName
ORDER BY SourceName, IsShared;
