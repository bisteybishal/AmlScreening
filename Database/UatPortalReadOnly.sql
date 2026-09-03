SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
    THROW 50110, 'Run RolesPermissions.sql before UatPortalReadOnly.sql.', 1;

DECLARE @ObsoletePermissions TABLE (PermissionName VARCHAR(100) PRIMARY KEY);

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

IF OBJECT_ID(N'dbo.UserPermissions', N'U') IS NOT NULL
BEGIN
    DELETE userPermission
    FROM dbo.UserPermissions userPermission
    INNER JOIN dbo.Permissions permission
        ON permission.PermissionId = userPermission.PermissionId
    INNER JOIN @ObsoletePermissions obsolete
        ON obsolete.PermissionName = permission.PermissionName;
END;

IF OBJECT_ID(N'dbo.RolePermissions', N'U') IS NOT NULL
BEGIN
    DELETE rolePermission
    FROM dbo.RolePermissions rolePermission
    INNER JOIN dbo.Permissions permission
        ON permission.PermissionId = rolePermission.PermissionId
    INNER JOIN @ObsoletePermissions obsolete
        ON obsolete.PermissionName = permission.PermissionName;
END;

UPDATE permission
SET permission.IsActive = 0
FROM dbo.Permissions permission
INNER JOIN @ObsoletePermissions obsolete
    ON obsolete.PermissionName = permission.PermissionName;

COMMIT TRANSACTION;

SELECT PermissionName, IsActive
FROM dbo.Permissions
WHERE PermissionName IN (SELECT PermissionName FROM @ObsoletePermissions)
ORDER BY PermissionName;
