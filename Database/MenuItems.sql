SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

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
('RiskScoring',    'Compliance', N'Risk Scores',         'MANAGEMENT', 'RiskScoring',      'Index', 'bi-speedometer2',        'RiskScoring.View',       35),
('RiskScoreSetup', 'Compliance', N'Risk Score Setup',    'MANAGEMENT', 'RiskScoring',      'Setup', 'bi-sliders',             'RiskScoring.View',       36),
('ComplianceAlerts','Compliance',N'Alerts',              'MANAGEMENT', 'Alerts',           'Index', 'bi-exclamation-triangle','Alerts.View',          50),
('ComplianceCases','Compliance', N'Cases',               'MANAGEMENT', 'Cases',            'Index', 'bi-folder2-open',        'Cases.View',             60),
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

SELECT MenuId, ParentMenuId, DisplayName, ControllerName, ActionName,
       PermissionCode, SortOrder, IsActive
FROM dbo.MenuItems
ORDER BY SectionName, ParentMenuId, SortOrder;
