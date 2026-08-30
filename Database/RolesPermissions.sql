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
    ('SearchScreening.View', 'View the Search Screening menu'),
    ('Reports.View',        'View the Reports menu'),
    ('AuditLogs.View',      'View the Audit Logs menu'),
    ('Settings.View',       'View the Settings menu'),
    ('Users.View',        'View portal users'),
    ('Users.Create',      'Create portal users'),
    ('Roles.Manage',      'Create roles and assign permissions')
) v (PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Permissions p WHERE p.PermissionName = v.PermissionName
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

-- Copy the role defaults to every existing user once. After this, permissions
-- can be changed independently for each user from the Users page.
INSERT INTO dbo.UserPermissions (UserId, PermissionId)
SELECT ur.UserId, rp.PermissionId
FROM dbo.UserRoles ur
INNER JOIN dbo.RolePermissions rp ON rp.RoleId = ur.RoleId
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.UserPermissions up
    WHERE up.UserId = ur.UserId
);

SELECT r.RoleName, p.PermissionName
FROM dbo.RolePermissions rp
INNER JOIN dbo.Roles r ON r.RoleId = rp.RoleId
INNER JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
ORDER BY r.RoleName, p.PermissionName;
