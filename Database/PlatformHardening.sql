SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* Subscription soft delete, audit metadata, and optimistic concurrency. */
IF OBJECT_ID(N'dbo.AML_Subscription_plan', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteFg') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteFg CHAR(1) NOT NULL
            CONSTRAINT DF_AML_Subscription_plan_DeleteFg DEFAULT 'N';
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteDate') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteDate DATETIME2(0) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'CreatedDate') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD CreatedDate DATETIME2(0) NOT NULL
            CONSTRAINT DF_AML_Subscription_plan_CreatedDate DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'CreatedBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD CreatedBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'UpdatedDate') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD UpdatedDate DATETIME2(0) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'UpdatedBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD UpdatedBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'RowVersion') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD RowVersion ROWVERSION NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AML_Subscription_plan')
          AND name = N'IX_AML_Subscription_plan_DeleteFg'
    )
        CREATE INDEX IX_AML_Subscription_plan_DeleteFg
            ON dbo.AML_Subscription_plan(DeleteFg, IsActive, PlanId);
END;

/* Row versions prevent one user's edit from silently overwriting another. */
IF OBJECT_ID(N'dbo.AmlRecords', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.AmlRecords', N'RowVersion') IS NULL
    ALTER TABLE dbo.AmlRecords ADD RowVersion ROWVERSION NOT NULL;

IF OBJECT_ID(N'dbo.ComplianceRules', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.ComplianceRules', N'RowVersion') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD RowVersion ROWVERSION NOT NULL;

/* UserPermissions now stores overrides only: grant (1) or deny (0). */
DECLARE @AddedIsGranted BIT = 0;
IF OBJECT_ID(N'dbo.UserPermissions', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.UserPermissions', N'IsGranted') IS NULL
BEGIN
    ALTER TABLE dbo.UserPermissions ADD IsGranted BIT NOT NULL
        CONSTRAINT DF_UserPermissions_IsGranted DEFAULT 1;
    SET @AddedIsGranted = 1;
END;

IF @AddedIsGranted = 1
BEGIN
    -- Preserve the effective access that users had immediately before migration.
    INSERT INTO dbo.UserPermissions (UserId, PermissionId, IsGranted)
    SELECT ur.UserId, rp.PermissionId, 0
    FROM dbo.UserRoles ur
    INNER JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.UserPermissions existing
        WHERE existing.UserId = ur.UserId
          AND existing.PermissionId = rp.PermissionId
    );

    -- Matching grants are inherited from the role and no longer need copied rows.
    DELETE userPermission
    FROM dbo.UserPermissions userPermission
    INNER JOIN dbo.UserRoles userRole ON userRole.UserId = userPermission.UserId
    INNER JOIN dbo.RolePermissions rolePermission
        ON rolePermission.RoleId = userRole.RoleId
       AND rolePermission.PermissionId = userPermission.PermissionId
    WHERE userPermission.IsGranted = 1;
END;

/* Simple database-driven navigation. Controller/action routing stays in MVC. */
IF OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MenuItems
    (
        MenuId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MenuItems PRIMARY KEY,
        ParentMenuId    INT NULL,
        MenuKey         VARCHAR(80) NOT NULL,
        DisplayName     NVARCHAR(100) NOT NULL,
        SectionName     VARCHAR(30) NOT NULL CONSTRAINT DF_MenuItems_SectionName DEFAULT 'MAIN',
        ControllerName  VARCHAR(100) NULL,
        ActionName      VARCHAR(100) NULL,
        IconClass       VARCHAR(100) NOT NULL CONSTRAINT DF_MenuItems_IconClass DEFAULT 'bi-circle',
        PermissionCode  VARCHAR(100) NULL,
        SortOrder       INT NOT NULL CONSTRAINT DF_MenuItems_SortOrder DEFAULT 0,
        IsActive        BIT NOT NULL CONSTRAINT DF_MenuItems_IsActive DEFAULT 1,
        CONSTRAINT UQ_MenuItems_MenuKey UNIQUE (MenuKey),
        CONSTRAINT FK_MenuItems_Parent FOREIGN KEY (ParentMenuId)
            REFERENCES dbo.MenuItems(MenuId)
    );
END;

DECLARE @Menus TABLE
(
    MenuKey VARCHAR(80), ParentKey VARCHAR(80) NULL, DisplayName NVARCHAR(100),
    SectionName VARCHAR(30), ControllerName VARCHAR(100) NULL,
    ActionName VARCHAR(100) NULL, IconClass VARCHAR(100),
    PermissionCode VARCHAR(100) NULL, SortOrder INT
);

INSERT INTO @Menus VALUES
('Dashboard',      NULL,         N'Dashboard',           'MAIN',       'Dashboard',       'Index', 'bi-house-door-fill',    NULL,                   10),
('AmlRecords',     NULL,         N'AML Records',         'MAIN',       'AmlRecords',      'Index', 'bi-shield-check',       'AMLRecords.View',      20),
('Users',          NULL,         N'Users',               'MANAGEMENT', 'Users',           'Index', 'bi-people',             'Users.View',            10),
('Roles',          NULL,         N'Roles & Permissions', 'MANAGEMENT', 'Roles',           'Index', 'bi-shield-lock',        'Roles.Manage',          20),
('Subscription',   NULL,         N'Subscription',        'MANAGEMENT', 'Subscription',    'Index', 'bi-credit-card',        'Subscription.View',     30),
('Compliance',     NULL,         N'Compliance',          'MANAGEMENT', NULL,               NULL,    'bi-shield-check',       NULL,                    40),
('Customers',      'Compliance', N'Customers',           'MANAGEMENT', 'Customers',        'Index', 'bi-person-vcard',       'Customers.View',         10),
('Transactions',   'Compliance', N'Transactions',        'MANAGEMENT', 'Transactions',     'Index', 'bi-arrow-left-right',    'Transactions.View',      20),
('ThresholdRules', 'Compliance', N'Threshold Rules',     'MANAGEMENT', 'ComplianceRules', 'Index', 'bi-sliders',            'ComplianceRules.View',  30),
('ComplianceAlerts','Compliance',N'Alerts',              'MANAGEMENT', 'Alerts',           'Index', 'bi-exclamation-triangle','Alerts.View',          40),
('ComplianceCases','Compliance', N'Cases',               'MANAGEMENT', 'Cases',            'Index', 'bi-folder2-open',        'Cases.View',             50),
('AuditLogs',      NULL,         N'Audit Logs',          'MANAGEMENT', 'AuditLogs',       'Index', 'bi-file-earmark-text', 'AuditLogs.View',         50);

INSERT INTO dbo.MenuItems
    (MenuKey, DisplayName, SectionName, ControllerName, ActionName,
     IconClass, PermissionCode, SortOrder, IsActive)
SELECT source.MenuKey, source.DisplayName, source.SectionName,
       source.ControllerName, source.ActionName, source.IconClass,
       source.PermissionCode, source.SortOrder, 1
FROM @Menus source
WHERE source.ParentKey IS NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.MenuItems existing WHERE existing.MenuKey = source.MenuKey);

INSERT INTO dbo.MenuItems
    (ParentMenuId, MenuKey, DisplayName, SectionName, ControllerName,
     ActionName, IconClass, PermissionCode, SortOrder, IsActive)
SELECT parent.MenuId, source.MenuKey, source.DisplayName, source.SectionName,
       source.ControllerName, source.ActionName, source.IconClass,
       source.PermissionCode, source.SortOrder, 1
FROM @Menus source
INNER JOIN dbo.MenuItems parent ON parent.MenuKey = source.ParentKey
WHERE source.ParentKey IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.MenuItems existing WHERE existing.MenuKey = source.MenuKey);

COMMIT TRANSACTION;
