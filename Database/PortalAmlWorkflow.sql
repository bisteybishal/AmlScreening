SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
    THROW 50100, 'Run ClientIsolation.sql before PortalAmlWorkflow.sql.', 1;

/* Synchronize columns already required by the current portal code. */
IF COL_LENGTH(N'dbo.AmlRecords', N'RowVersion') IS NULL
    ALTER TABLE dbo.AmlRecords ADD RowVersion ROWVERSION NOT NULL;

IF COL_LENGTH(N'dbo.ComplianceRules', N'RowVersion') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD RowVersion ROWVERSION NOT NULL;

IF COL_LENGTH(N'dbo.UserPermissions', N'IsGranted') IS NULL
    ALTER TABLE dbo.UserPermissions ADD IsGranted BIT NOT NULL
        CONSTRAINT DF_UserPermissions_IsGranted DEFAULT 1;

IF OBJECT_ID(N'dbo.AML_Subscription_plan', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteFg') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_AMLSubscription_DeleteFg DEFAULT 'N';
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteDate') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteDate DATETIME2(0) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'DeleteBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD DeleteBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'CreatedBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD CreatedBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'UpdatedDate') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD UpdatedDate DATETIME2(0) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'UpdatedBy') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD UpdatedBy NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AML_Subscription_plan', N'RowVersion') IS NULL
        ALTER TABLE dbo.AML_Subscription_plan ADD RowVersion ROWVERSION NOT NULL;
END;

IF COL_LENGTH(N'dbo.ComplianceRules', N'RuleVersion') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD RuleVersion INT NOT NULL CONSTRAINT DF_ComplianceRules_RuleVersion DEFAULT 1;
IF COL_LENGTH(N'dbo.ComplianceRules', N'Priority') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD Priority INT NOT NULL CONSTRAINT DF_ComplianceRules_Priority DEFAULT 100;
IF COL_LENGTH(N'dbo.ComplianceRules', N'ApprovalStatus') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD ApprovalStatus NVARCHAR(20) NOT NULL CONSTRAINT DF_ComplianceRules_ApprovalStatus DEFAULT N'Active';
IF COL_LENGTH(N'dbo.ComplianceRules', N'ApprovedBy') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD ApprovedBy NVARCHAR(100) NULL;
IF COL_LENGTH(N'dbo.ComplianceRules', N'ApprovedDate') IS NULL
    ALTER TABLE dbo.ComplianceRules ADD ApprovedDate DATETIME2(0) NULL;

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
        ClientId INT NOT NULL,
        CustomerReference NVARCHAR(100) NOT NULL,
        FullName NVARCHAR(200) NOT NULL,
        DateOfBirth DATE NULL,
        Country NVARCHAR(100) NOT NULL CONSTRAINT DF_Customers_Country DEFAULT N'',
        Nationality NVARCHAR(100) NOT NULL CONSTRAINT DF_Customers_Nationality DEFAULT N'',
        Occupation NVARCHAR(150) NOT NULL CONSTRAINT DF_Customers_Occupation DEFAULT N'',
        SourceOfFunds NVARCHAR(150) NOT NULL CONSTRAINT DF_Customers_SourceOfFunds DEFAULT N'',
        IsPep BIT NOT NULL CONSTRAINT DF_Customers_IsPep DEFAULT 0,
        RiskScore INT NOT NULL CONSTRAINT DF_Customers_RiskScore DEFAULT 0,
        RiskLevel NVARCHAR(20) NOT NULL CONSTRAINT DF_Customers_RiskLevel DEFAULT N'LOW',
        IsActive BIT NOT NULL CONSTRAINT DF_Customers_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_Customers_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_Customers_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_Customers_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT CK_Customers_RiskScore CHECK (RiskScore BETWEEN 0 AND 100),
        CONSTRAINT CK_Customers_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
    CREATE UNIQUE INDEX UX_Customers_ClientReference ON dbo.Customers(ClientId, CustomerReference) WHERE DeleteFg = 'N';
END;

IF OBJECT_ID(N'dbo.AmlTransactions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AmlTransactions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AmlTransactions PRIMARY KEY,
        ClientId INT NOT NULL, TransactionReference NVARCHAR(100) NOT NULL, CustomerId INT NOT NULL,
        SenderName NVARCHAR(200) NOT NULL, ReceiverName NVARCHAR(200) NOT NULL,
        Amount DECIMAL(18,2) NOT NULL, Currency NVARCHAR(3) NOT NULL,
        OriginCountry NVARCHAR(100) NOT NULL, DestinationCountry NVARCHAR(100) NOT NULL,
        Channel NVARCHAR(50) NOT NULL, TransactionDate DATETIME2(0) NOT NULL,
        PurposeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_AmlTransactions_PurposeCode DEFAULT N'UNKNOWN',
        RegisteredReceiverCount INT NOT NULL CONSTRAINT DF_AmlTransactions_ReceiverCount DEFAULT 0,
        RelationshipCode NVARCHAR(50) NOT NULL CONSTRAINT DF_AmlTransactions_RelationshipCode DEFAULT N'UNKNOWN',
        Status NVARCHAR(30) NOT NULL CONSTRAINT DF_AmlTransactions_Status DEFAULT N'NEW',
        CorrelationId NVARCHAR(100) NULL, Decision NVARCHAR(20) NULL, RiskScore INT NULL,
        RiskLevel NVARCHAR(20) NULL, ScreeningStatus NVARCHAR(30) NULL,
        DecisionReasons NVARCHAR(2000) NULL, EvaluatedDate DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AmlTransactions_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AmlTransactions_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_AmlTransactions_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_AmlTransactions_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_AmlTransactions_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id),
        CONSTRAINT CK_AmlTransactions_Amount CHECK (Amount > 0),
        CONSTRAINT CK_AmlTransactions_ReceiverCount CHECK (RegisteredReceiverCount >= 0),
        CONSTRAINT CK_AmlTransactions_RiskScore CHECK (RiskScore IS NULL OR RiskScore BETWEEN 0 AND 100),
        CONSTRAINT CK_AmlTransactions_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
    CREATE UNIQUE INDEX UX_AmlTransactions_ClientReference ON dbo.AmlTransactions(ClientId, TransactionReference) WHERE DeleteFg = 'N';
    CREATE INDEX IX_AmlTransactions_ClientDate ON dbo.AmlTransactions(ClientId, TransactionDate DESC);
END;

IF OBJECT_ID(N'dbo.ScreeningMatches', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ScreeningMatches
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ScreeningMatches PRIMARY KEY,
        ClientId INT NOT NULL, TransactionId INT NOT NULL, AmlRecordId INT NOT NULL,
        SubjectName NVARCHAR(200) NOT NULL, MatchedName NVARCHAR(200) NOT NULL,
        MatchScore DECIMAL(5,2) NOT NULL, MatchStatus NVARCHAR(30) NOT NULL,
        SourceName NVARCHAR(150) NOT NULL, ListVersion NVARCHAR(50) NOT NULL,
        ComparedFields NVARCHAR(500) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ScreeningMatches_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ScreeningMatches_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_ScreeningMatches_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_ScreeningMatches_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_ScreeningMatches_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.AmlTransactions(Id),
        CONSTRAINT FK_ScreeningMatches_AmlRecords FOREIGN KEY (AmlRecordId) REFERENCES dbo.AmlRecords(Id)
    );
    CREATE INDEX IX_ScreeningMatches_Transaction ON dbo.ScreeningMatches(TransactionId, MatchScore DESC);
END;

IF OBJECT_ID(N'dbo.RiskAssessments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskAssessments
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskAssessments PRIMARY KEY,
        ClientId INT NOT NULL, TransactionId INT NOT NULL,
        CustomerRiskScore INT NOT NULL, TransactionRiskScore INT NOT NULL,
        ScreeningRiskScore INT NOT NULL, OverallRiskScore INT NOT NULL,
        RiskLevel NVARCHAR(20) NOT NULL, ModelVersion NVARCHAR(50) NOT NULL,
        Factors NVARCHAR(2000) NOT NULL, CalculatedAt DATETIME2(0) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskAssessments_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskAssessments_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskAssessments_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskAssessments_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_RiskAssessments_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.AmlTransactions(Id),
        CONSTRAINT CK_RiskAssessments_Scores CHECK (CustomerRiskScore BETWEEN 0 AND 100 AND TransactionRiskScore BETWEEN 0 AND 100 AND ScreeningRiskScore BETWEEN 0 AND 100 AND OverallRiskScore BETWEEN 0 AND 100)
    );
    CREATE UNIQUE INDEX UX_RiskAssessments_Transaction ON dbo.RiskAssessments(TransactionId) WHERE DeleteFg = 'N';
END;

IF OBJECT_ID(N'dbo.AmlDecisions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AmlDecisions
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AmlDecisions PRIMARY KEY,
        ClientId INT NOT NULL, TransactionId INT NOT NULL, RiskAssessmentId INT NOT NULL,
        CorrelationId NVARCHAR(100) NOT NULL, Decision NVARCHAR(20) NOT NULL,
        Reasons NVARCHAR(2000) NOT NULL, RuleVersions NVARCHAR(1000) NOT NULL,
        ModelVersion NVARCHAR(50) NOT NULL, ListVersions NVARCHAR(500) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_AmlDecisions_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AmlDecisions_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_AmlDecisions_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_AmlDecisions_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_AmlDecisions_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.AmlTransactions(Id),
        CONSTRAINT FK_AmlDecisions_RiskAssessments FOREIGN KEY (RiskAssessmentId) REFERENCES dbo.RiskAssessments(Id)
    );
    CREATE UNIQUE INDEX UX_AmlDecisions_Transaction ON dbo.AmlDecisions(TransactionId) WHERE DeleteFg = 'N';
    CREATE UNIQUE INDEX UX_AmlDecisions_Correlation ON dbo.AmlDecisions(ClientId, CorrelationId) WHERE DeleteFg = 'N';
END;

IF OBJECT_ID(N'dbo.ComplianceAlerts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplianceAlerts
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ComplianceAlerts PRIMARY KEY,
        ClientId INT NOT NULL, AlertReference NVARCHAR(100) NOT NULL,
        TransactionId INT NOT NULL, RiskAssessmentId INT NOT NULL,
        Severity NVARCHAR(20) NOT NULL, Reason NVARCHAR(2000) NOT NULL,
        Status NVARCHAR(30) NOT NULL, AssignedUserId BIGINT NULL,
        ResolvedDate DATETIME2(0) NULL, Resolution NVARCHAR(2000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ComplianceAlerts_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ComplianceAlerts_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_ComplianceAlerts_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_ComplianceAlerts_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_ComplianceAlerts_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.AmlTransactions(Id),
        CONSTRAINT FK_ComplianceAlerts_RiskAssessments FOREIGN KEY (RiskAssessmentId) REFERENCES dbo.RiskAssessments(Id),
        CONSTRAINT FK_ComplianceAlerts_AssignedUser FOREIGN KEY (AssignedUserId) REFERENCES dbo.Users(UserId)
    );
    CREATE UNIQUE INDEX UX_ComplianceAlerts_Reference ON dbo.ComplianceAlerts(ClientId, AlertReference) WHERE DeleteFg = 'N';
    CREATE INDEX IX_ComplianceAlerts_Queue ON dbo.ComplianceAlerts(ClientId, Status, CreatedDate DESC);
END;

IF OBJECT_ID(N'dbo.ComplianceCases', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplianceCases
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ComplianceCases PRIMARY KEY,
        ClientId INT NOT NULL, CaseReference NVARCHAR(100) NOT NULL,
        AlertId INT NOT NULL, TransactionId INT NOT NULL, Status NVARCHAR(30) NOT NULL,
        AssignedUserId BIGINT NULL, Disposition NVARCHAR(100) NULL, Notes NVARCHAR(4000) NULL,
        ClosedDate DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_ComplianceCases_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_ComplianceCases_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL, UpdatedDate DATETIME2(0) NULL, UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_ComplianceCases_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL, DeleteBy NVARCHAR(100) NULL, RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_ComplianceCases_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_ComplianceCases_Alerts FOREIGN KEY (AlertId) REFERENCES dbo.ComplianceAlerts(Id),
        CONSTRAINT FK_ComplianceCases_Transactions FOREIGN KEY (TransactionId) REFERENCES dbo.AmlTransactions(Id),
        CONSTRAINT FK_ComplianceCases_AssignedUser FOREIGN KEY (AssignedUserId) REFERENCES dbo.Users(UserId)
    );
    CREATE UNIQUE INDEX UX_ComplianceCases_Reference ON dbo.ComplianceCases(ClientId, CaseReference) WHERE DeleteFg = 'N';
    CREATE INDEX IX_ComplianceCases_Queue ON dbo.ComplianceCases(ClientId, Status, CreatedDate DESC);
END;

/* Permissions used by portal controllers. */
INSERT INTO dbo.Permissions (PermissionName, Description)
SELECT source.PermissionName, source.Description
FROM (VALUES
    ('Customers.View', 'View customer/KYC profiles'),
    ('Transactions.View', 'View compliance transactions')
) source(PermissionName, Description)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions existing WHERE existing.PermissionName = source.PermissionName);

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

INSERT INTO dbo.RolePermissions (RoleId, PermissionId)
SELECT role.RoleId, permission.PermissionId
FROM dbo.Roles role
CROSS JOIN dbo.Permissions permission
WHERE role.RoleName = 'Admin' AND permission.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions existing WHERE existing.RoleId = role.RoleId AND existing.PermissionId = permission.PermissionId);

/* Menu rows use ParentMenuId to build the Compliance dropdown. */
DECLARE @ComplianceMenuId INT = (SELECT MenuId FROM dbo.MenuItems WHERE MenuKey = 'Compliance');
IF @ComplianceMenuId IS NOT NULL
BEGIN
    INSERT INTO dbo.MenuItems (ParentMenuId, MenuKey, DisplayName, SectionName, ControllerName, ActionName, IconClass, PermissionCode, SortOrder, IsActive)
    SELECT @ComplianceMenuId, source.MenuKey, source.DisplayName, 'MANAGEMENT', source.ControllerName, 'Index', source.IconClass, source.PermissionCode, source.SortOrder, 1
    FROM (VALUES
        ('Customers', N'Customers', 'Customers', 'bi-person-vcard', 'Customers.View', 10),
        ('Transactions', N'Transactions', 'Transactions', 'bi-arrow-left-right', 'Transactions.View', 20),
        ('ComplianceAlerts', N'Alerts', 'Alerts', 'bi-exclamation-triangle', 'Alerts.View', 40),
        ('ComplianceCases', N'Cases', 'Cases', 'bi-folder2-open', 'Cases.View', 50)
    ) source(MenuKey, DisplayName, ControllerName, IconClass, PermissionCode, SortOrder)
    WHERE NOT EXISTS (SELECT 1 FROM dbo.MenuItems existing WHERE existing.MenuKey = source.MenuKey);
END;

COMMIT TRANSACTION;
