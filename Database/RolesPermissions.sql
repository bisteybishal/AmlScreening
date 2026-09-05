SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Permissions
    (
        PermissionId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permissions PRIMARY KEY,
        PermissionName VARCHAR(100) NOT NULL,
        Description    VARCHAR(250) NULL,
        IsActive       BIT NOT NULL CONSTRAINT DF_Permissions_IsActive DEFAULT 1,
        CONSTRAINT UQ_Permissions_PermissionName UNIQUE (PermissionName)
    );
END;

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RolePermissions
    (
        RoleId      INT NOT NULL,
        PermissionId INT NOT NULL,
        CONSTRAINT PK_RolePermissions PRIMARY KEY (RoleId, PermissionId),
        CONSTRAINT FK_RolePermissions_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(RoleId),
        CONSTRAINT FK_RolePermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(PermissionId)
    );
END;

IF OBJECT_ID(N'dbo.UserPermissions', N'U') IS NULL
BEGIN
    DECLARE @UserIdSqlType NVARCHAR(20);

    SELECT @UserIdSqlType = UPPER(DATA_TYPE)
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Users'
      AND COLUMN_NAME = 'UserId';

    IF @UserIdSqlType NOT IN ('INT', 'BIGINT', 'SMALLINT')
        THROW 50001, 'Users.UserId must be INT, BIGINT, or SMALLINT.', 1;

    EXEC(N'
        CREATE TABLE dbo.UserPermissions
        (
            UserId ' + @UserIdSqlType + N' NOT NULL,
            PermissionId INT NOT NULL,
            IsGranted BIT NOT NULL CONSTRAINT DF_UserPermissions_IsGranted DEFAULT 1,
            CONSTRAINT PK_UserPermissions PRIMARY KEY (UserId, PermissionId),
            CONSTRAINT FK_UserPermissions_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
            CONSTRAINT FK_UserPermissions_Permissions FOREIGN KEY (PermissionId) REFERENCES dbo.Permissions(PermissionId)
        );');
END;

INSERT INTO dbo.Permissions (PermissionName, Description)
SELECT v.PermissionName, v.Description
FROM (VALUES
    ('AMLRecords.View',   'View AML records and reports'),
    ('AMLRecords.Create', 'Create new AML records'),
    ('AMLRecords.Edit',   'Edit existing AML records'),
    ('AMLRecords.Delete', 'Soft delete AML records'),
    ('AMLRecords.ViewClient', 'View all AML records for the current client'),
    ('AMLRecords.ViewAll', 'Platform access to AML records across all clients'),
    ('AuditLogs.View',      'View the Audit Logs menu'),
    ('Users.View',        'View portal users'),
    ('Users.Create',      'Create portal users'),
    ('Roles.Manage',      'Create roles and assign permissions'),
    ('Subscription.View',   'View subscription plans'),
    ('Subscription.Create', 'Create subscription plans'),
    ('Subscription.Edit',   'Edit subscription plans'),
    ('Subscription.Delete', 'Delete subscription plans'),
    ('ComplianceRules.View',   'View compliance rules'),
    ('ComplianceRules.Create', 'Create compliance rules'),
    ('ComplianceRules.Edit',   'Edit compliance rules'),
    ('ComplianceRules.Delete', 'Delete compliance rules'),
    ('Customers.View',       'View customer/KYC profiles'),
    ('Transactions.View',   'View compliance transactions'),
    ('Alerts.View',         'View compliance alerts'),
    ('Alerts.Manage',       'Assign and resolve compliance alerts'),
    ('Cases.View',          'View compliance cases'),
    ('Cases.Manage',        'Investigate and manage compliance cases'),
    ('AmlSource.View',      'View AML data sources'),
    ('AmlSource.Sync',      'Synchronize AML data sources'),
    ('RiskScoring.View',    'View risk scoring configuration'),
    ('RiskScoring.Manage',  'Edit risk factor scores and risk bands')
) v (PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Permissions p WHERE p.PermissionName = v.PermissionName
);

-- These functions belong to the separately hosted API or were never implemented.
UPDATE dbo.Permissions
SET IsActive = 0
WHERE PermissionName IN
(
    'Customers.Create',
    'Customers.Edit',
    'Customers.Delete',
    'Transactions.Create',
    'Transactions.Evaluate',
    'SearchScreening.View',
    'Reports.View',
    'Settings.View'
);

INSERT INTO dbo.Roles (RoleName, IsActive, CreatedDate)
SELECT v.RoleName, 1, SYSUTCDATETIME()
FROM (VALUES ('AML User'), ('Viewer')) v (RoleName)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Roles r WHERE r.RoleName = v.RoleName
);

-- Admin always starts with every permission.
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM dbo.Roles r
CROSS JOIN dbo.Permissions p
WHERE r.RoleName = 'Admin'
  AND p.IsActive = 1
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

-- AML User can view, create, and edit records.
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM dbo.Roles r
INNER JOIN dbo.Permissions p ON p.PermissionName IN
    ('AMLRecords.View', 'AMLRecords.Create', 'AMLRecords.Edit')
WHERE r.RoleName = 'AML User'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
);

-- Viewer can only view AML records.
INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM dbo.Roles r
INNER JOIN dbo.Permissions p ON p.PermissionName = 'AMLRecords.View'
WHERE r.RoleName = 'Viewer'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RolePermissions rp
      WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId
  );

SELECT r.RoleName, p.PermissionName
FROM dbo.RolePermissions rp
INNER JOIN dbo.Roles r ON r.RoleId = rp.RoleId
INNER JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
ORDER BY r.RoleName, p.PermissionName;
