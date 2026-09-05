SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AmlTransactions', N'U') IS NULL OR
   OBJECT_ID(N'dbo.RiskAssessments', N'U') IS NULL
    THROW 50300, 'Run ClientIsolation.sql and PortalAmlWorkflow.sql before RiskScoring.sql.', 1;

/* Inputs used by the risk-score screen and future partner API. */
IF COL_LENGTH(N'dbo.AmlTransactions', N'PurposeCode') IS NULL
    ALTER TABLE dbo.AmlTransactions ADD PurposeCode NVARCHAR(50) NOT NULL
        CONSTRAINT DF_AmlTransactions_PurposeCode DEFAULT N'UNKNOWN';
IF COL_LENGTH(N'dbo.AmlTransactions', N'RegisteredReceiverCount') IS NULL
    ALTER TABLE dbo.AmlTransactions ADD RegisteredReceiverCount INT NOT NULL
        CONSTRAINT DF_AmlTransactions_ReceiverCount DEFAULT 0;
IF COL_LENGTH(N'dbo.AmlTransactions', N'RelationshipCode') IS NULL
    ALTER TABLE dbo.AmlTransactions ADD RelationshipCode NVARCHAR(50) NOT NULL
        CONSTRAINT DF_AmlTransactions_RelationshipCode DEFAULT N'UNKNOWN';

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.AmlTransactions')
      AND name = N'CK_AmlTransactions_ReceiverCount'
)
    ALTER TABLE dbo.AmlTransactions ADD CONSTRAINT CK_AmlTransactions_ReceiverCount
        CHECK (RegisteredReceiverCount >= 0);

IF OBJECT_ID(N'dbo.RiskFactors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskFactors
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskFactors PRIMARY KEY,
        ClientId INT NOT NULL,
        FactorCode NVARCHAR(50) NOT NULL,
        FactorName NVARCHAR(100) NOT NULL,
        ScoreCategory NVARCHAR(20) NOT NULL,
        ValueType NVARCHAR(20) NOT NULL,
        WeightPercent DECIMAL(5,2) NOT NULL,
        DisplayOrder INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskFactors_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskFactors_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        UpdatedDate DATETIME2(0) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskFactors_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL,
        DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskFactors_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT CK_RiskFactors_Category CHECK (ScoreCategory IN (N'CUSTOMER', N'TRANSACTION')),
        CONSTRAINT CK_RiskFactors_ValueType CHECK (ValueType IN (N'TEXT', N'BOOLEAN', N'NUMBER')),
        CONSTRAINT CK_RiskFactors_Weight CHECK (WeightPercent > 0 AND WeightPercent <= 100),
        CONSTRAINT CK_RiskFactors_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RiskFactors') AND name = N'UX_RiskFactors_ClientCode')
    CREATE UNIQUE INDEX UX_RiskFactors_ClientCode
        ON dbo.RiskFactors(ClientId, FactorCode) WHERE DeleteFg = 'N';

IF OBJECT_ID(N'dbo.RiskFactorRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskFactorRules
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskFactorRules PRIMARY KEY,
        ClientId INT NOT NULL,
        RiskFactorId INT NOT NULL,
        ValueCode NVARCHAR(100) NULL,
        ValueName NVARCHAR(200) NULL,
        MinimumValue DECIMAL(18,2) NULL,
        MaximumValue DECIMAL(18,2) NULL,
        Score INT NOT NULL,
        Remarks NVARCHAR(1000) NULL,
        RuleVersion INT NOT NULL CONSTRAINT DF_RiskFactorRules_Version DEFAULT 1,
        ApprovalStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_RiskFactorRules_Approval DEFAULT N'DRAFT',
        ApprovedBy NVARCHAR(100) NULL,
        ApprovedDate DATETIME2(0) NULL,
        EffectiveFrom DATETIME2(0) NOT NULL CONSTRAINT DF_RiskFactorRules_EffectiveFrom DEFAULT SYSUTCDATETIME(),
        EffectiveTo DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskFactorRules_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskFactorRules_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        UpdatedDate DATETIME2(0) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskFactorRules_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL,
        DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskFactorRules_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_RiskFactorRules_Factors FOREIGN KEY (RiskFactorId) REFERENCES dbo.RiskFactors(Id),
        CONSTRAINT CK_RiskFactorRules_Score CHECK (Score BETWEEN 0 AND 100),
        CONSTRAINT CK_RiskFactorRules_Version CHECK (RuleVersion > 0),
        CONSTRAINT CK_RiskFactorRules_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom),
        CONSTRAINT CK_RiskFactorRules_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RiskFactorRules') AND name = N'IX_RiskFactorRules_Evaluation')
    CREATE INDEX IX_RiskFactorRules_Evaluation
        ON dbo.RiskFactorRules(ClientId, RiskFactorId, RuleVersion, ApprovalStatus, IsActive);

IF OBJECT_ID(N'dbo.RiskBands', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskBands
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskBands PRIMARY KEY,
        ClientId INT NOT NULL,
        RiskLevel NVARCHAR(20) NOT NULL,
        MinimumScore INT NOT NULL,
        MaximumScore INT NULL,
        Decision NVARCHAR(20) NOT NULL,
        Remarks NVARCHAR(1000) NULL,
        BandVersion INT NOT NULL CONSTRAINT DF_RiskBands_Version DEFAULT 1,
        ApprovalStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_RiskBands_Approval DEFAULT N'DRAFT',
        ApprovedBy NVARCHAR(100) NULL,
        ApprovedDate DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskBands_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskBands_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        UpdatedDate DATETIME2(0) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskBands_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL,
        DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskBands_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT CK_RiskBands_Range CHECK
            (MinimumScore BETWEEN 0 AND 100 AND
             (MaximumScore IS NULL OR (MaximumScore BETWEEN MinimumScore AND 100))),
        CONSTRAINT CK_RiskBands_Decision CHECK (Decision IN (N'ALLOW', N'REVIEW', N'BLOCK')),
        CONSTRAINT CK_RiskBands_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RiskBands') AND name = N'IX_RiskBands_Evaluation')
    CREATE INDEX IX_RiskBands_Evaluation
        ON dbo.RiskBands(ClientId, BandVersion, ApprovalStatus, IsActive, MinimumScore);

IF OBJECT_ID(N'dbo.RiskModels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskModels
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskModels PRIMARY KEY,
        ClientId INT NOT NULL,
        ModelCode NVARCHAR(50) NOT NULL,
        ModelVersion INT NOT NULL,
        CustomerWeightPercent DECIMAL(5,2) NOT NULL,
        TransactionWeightPercent DECIMAL(5,2) NOT NULL,
        ScreeningWeightPercent DECIMAL(5,2) NOT NULL,
        ApprovalStatus NVARCHAR(30) NOT NULL CONSTRAINT DF_RiskModels_Approval DEFAULT N'DRAFT',
        ApprovedBy NVARCHAR(100) NULL,
        ApprovedDate DATETIME2(0) NULL,
        EffectiveFrom DATETIME2(0) NOT NULL CONSTRAINT DF_RiskModels_EffectiveFrom DEFAULT SYSUTCDATETIME(),
        EffectiveTo DATETIME2(0) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskModels_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskModels_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        UpdatedDate DATETIME2(0) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskModels_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL,
        DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskModels_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT CK_RiskModels_Weights CHECK
            (CustomerWeightPercent + TransactionWeightPercent + ScreeningWeightPercent = 100),
        CONSTRAINT CK_RiskModels_Dates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom),
        CONSTRAINT CK_RiskModels_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RiskModels') AND name = N'UX_RiskModels_ClientVersion')
    CREATE UNIQUE INDEX UX_RiskModels_ClientVersion
        ON dbo.RiskModels(ClientId, ModelCode, ModelVersion) WHERE DeleteFg = 'N';

IF OBJECT_ID(N'dbo.RiskAssessmentFactors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RiskAssessmentFactors
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RiskAssessmentFactors PRIMARY KEY,
        ClientId INT NOT NULL,
        RiskAssessmentId INT NOT NULL,
        FactorCode NVARCHAR(50) NOT NULL,
        InputValue NVARCHAR(500) NOT NULL,
        RawScore INT NOT NULL,
        WeightPercent DECIMAL(5,2) NOT NULL,
        WeightedScore DECIMAL(8,2) NOT NULL,
        Explanation NVARCHAR(1000) NOT NULL,
        RuleVersion INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_RiskAssessmentFactors_IsActive DEFAULT 1,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_RiskAssessmentFactors_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NULL,
        UpdatedDate DATETIME2(0) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        DeleteFg CHAR(1) NOT NULL CONSTRAINT DF_RiskAssessmentFactors_DeleteFg DEFAULT 'N',
        DeleteDate DATETIME2(0) NULL,
        DeleteBy NVARCHAR(100) NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_RiskAssessmentFactors_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id),
        CONSTRAINT FK_RiskAssessmentFactors_Assessment FOREIGN KEY (RiskAssessmentId) REFERENCES dbo.RiskAssessments(Id),
        CONSTRAINT CK_RiskAssessmentFactors_Score CHECK (RawScore BETWEEN 0 AND 100),
        CONSTRAINT CK_RiskAssessmentFactors_DeleteFg CHECK (DeleteFg IN ('N','Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.RiskAssessmentFactors') AND name = N'IX_RiskAssessmentFactors_Assessment')
    CREATE INDEX IX_RiskAssessmentFactors_Assessment
        ON dbo.RiskAssessmentFactors(RiskAssessmentId, FactorCode);

/* UAT starting model. Compliance must review these scores before Production. */
DECLARE @Factors TABLE
(
    FactorCode NVARCHAR(50), FactorName NVARCHAR(100),
    ScoreCategory NVARCHAR(20), ValueType NVARCHAR(20),
    WeightPercent DECIMAL(5,2), DisplayOrder INT
);
INSERT INTO @Factors VALUES
    (N'CUSTOMER_PEP', N'PEP status', N'CUSTOMER', N'BOOLEAN', 50, 10),
    (N'OCCUPATION', N'Occupation', N'CUSTOMER', N'TEXT', 30, 20),
    (N'SOURCE_OF_FUNDS', N'Source of funds', N'CUSTOMER', N'TEXT', 20, 30),
    (N'AMOUNT', N'Amount', N'TRANSACTION', N'NUMBER', 30, 40),
    (N'RECEIVING_COUNTRY', N'Receiving country', N'TRANSACTION', N'TEXT', 25, 50),
    (N'PURPOSE', N'Purpose', N'TRANSACTION', N'TEXT', 20, 60),
    (N'RECEIVER_COUNT', N'Number of registered receivers', N'TRANSACTION', N'NUMBER', 15, 70),
    (N'RELATIONSHIP', N'Relationship', N'TRANSACTION', N'TEXT', 10, 80);

INSERT INTO dbo.RiskFactors
    (ClientId, FactorCode, FactorName, ScoreCategory, ValueType,
     WeightPercent, DisplayOrder, IsActive, CreatedDate, CreatedBy)
SELECT client.Id, factor.FactorCode, factor.FactorName, factor.ScoreCategory,
       factor.ValueType, factor.WeightPercent, factor.DisplayOrder,
       1, SYSUTCDATETIME(), N'RiskScoring.sql'
FROM dbo.Clients client
CROSS JOIN @Factors factor
WHERE client.IsActive = 1 AND client.DeleteFg = 'N'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RiskFactors existing
      WHERE existing.ClientId = client.Id
        AND existing.FactorCode = factor.FactorCode
        AND existing.DeleteFg = 'N'
  );

DECLARE @Rules TABLE
(
    FactorCode NVARCHAR(50), ValueCode NVARCHAR(100) NULL,
    ValueName NVARCHAR(200) NULL, MinimumValue DECIMAL(18,2) NULL,
    MaximumValue DECIMAL(18,2) NULL, Score INT, Remarks NVARCHAR(1000)
);
INSERT INTO @Rules VALUES
    (N'CUSTOMER_PEP', N'NO', N'Not a PEP', NULL, NULL, 0, N'No PEP indicator.'),
    (N'CUSTOMER_PEP', N'YES', N'PEP identified', NULL, NULL, 100, N'PEP status requires enhanced due diligence.'),
    (N'OCCUPATION', N'CASINO', N'Casino', NULL, NULL, 80, N'Cash-intensive occupation.'),
    (N'OCCUPATION', N'MONEY_SERVICE_BUSINESS', N'Money service business', NULL, NULL, 80, N'Financial crime exposure is elevated.'),
    (N'OCCUPATION', N'PUBLIC_OFFICIAL', N'Public official', NULL, NULL, 70, N'Public role requires enhanced due diligence.'),
    (N'OCCUPATION', N'UNEMPLOYED', N'Unemployed', NULL, NULL, 40, N'Validate source of funds.'),
    (N'OCCUPATION', N'DEFAULT', N'Other occupation', NULL, NULL, 10, N'Standard occupation risk.'),
    (N'SOURCE_OF_FUNDS', N'SALARY', N'Salary', NULL, NULL, 0, N'Regular salary source.'),
    (N'SOURCE_OF_FUNDS', N'BUSINESS_INCOME', N'Business income', NULL, NULL, 10, N'Business income requires supporting evidence.'),
    (N'SOURCE_OF_FUNDS', N'CASH', N'Cash', NULL, NULL, 60, N'Cash source has reduced traceability.'),
    (N'SOURCE_OF_FUNDS', N'UNKNOWN', N'Unknown', NULL, NULL, 80, N'Source of funds is missing or unknown.'),
    (N'SOURCE_OF_FUNDS', N'DEFAULT', N'Other source', NULL, NULL, 30, N'Validate the declared source of funds.'),
    (N'AMOUNT', NULL, NULL, 0, 100000, 0, N'Normal transaction amount.'),
    (N'AMOUNT', NULL, NULL, 100000.01, 500000, 25, N'Elevated transaction amount.'),
    (N'AMOUNT', NULL, NULL, 500000.01, 1000000, 60, N'High transaction amount.'),
    (N'AMOUNT', NULL, NULL, 1000000.01, NULL, 90, N'Very high transaction amount.'),
    (N'RECEIVING_COUNTRY', N'NEPAL', N'Nepal', NULL, NULL, 0, N'Domestic destination.'),
    (N'RECEIVING_COUNTRY', N'INDIA', N'India', NULL, NULL, 10, N'Standard destination risk.'),
    (N'RECEIVING_COUNTRY', N'AFGHANISTAN', N'Afghanistan', NULL, NULL, 80, N'Enhanced country risk review required.'),
    (N'RECEIVING_COUNTRY', N'IRAN', N'Iran', NULL, NULL, 90, N'Sanctions and country risk review required.'),
    (N'RECEIVING_COUNTRY', N'NORTH_KOREA', N'North Korea', NULL, NULL, 100, N'Sanctions exposure.'),
    (N'RECEIVING_COUNTRY', N'SYRIA', N'Syria', NULL, NULL, 90, N'Sanctions and country risk review required.'),
    (N'RECEIVING_COUNTRY', N'DEFAULT', N'Other country', NULL, NULL, 10, N'Standard country risk pending formal country matrix.'),
    (N'PURPOSE', N'FAMILY_SUPPORT', N'Family support', NULL, NULL, 5, N'Common remittance purpose.'),
    (N'PURPOSE', N'EDUCATION', N'Education', NULL, NULL, 5, N'Common documented purpose.'),
    (N'PURPOSE', N'MEDICAL', N'Medical', NULL, NULL, 5, N'Common documented purpose.'),
    (N'PURPOSE', N'TRADE', N'Trade', NULL, NULL, 30, N'Validate trade documents.'),
    (N'PURPOSE', N'INVESTMENT', N'Investment', NULL, NULL, 35, N'Validate investment purpose and source.'),
    (N'PURPOSE', N'DEFAULT', N'Other purpose', NULL, NULL, 20, N'Purpose requires review against evidence.'),
    (N'RECEIVER_COUNT', NULL, NULL, 0, 2, 0, N'Normal receiver count.'),
    (N'RECEIVER_COUNT', NULL, NULL, 3, 5, 25, N'Multiple receivers.'),
    (N'RECEIVER_COUNT', NULL, NULL, 6, 10, 50, N'High receiver count.'),
    (N'RECEIVER_COUNT', NULL, NULL, 11, NULL, 80, N'Very high receiver count.'),
    (N'RELATIONSHIP', N'SELF', N'Self', NULL, NULL, 0, N'Self transfer.'),
    (N'RELATIONSHIP', N'FAMILY', N'Family', NULL, NULL, 5, N'Declared family relationship.'),
    (N'RELATIONSHIP', N'BUSINESS_PARTNER', N'Business partner', NULL, NULL, 20, N'Validate business relationship.'),
    (N'RELATIONSHIP', N'UNKNOWN', N'Unknown', NULL, NULL, 60, N'Relationship is unknown.'),
    (N'RELATIONSHIP', N'DEFAULT', N'Other relationship', NULL, NULL, 30, N'Validate relationship to receiver.');

INSERT INTO dbo.RiskFactorRules
    (ClientId, RiskFactorId, ValueCode, ValueName, MinimumValue, MaximumValue,
     Score, Remarks, RuleVersion, ApprovalStatus, ApprovedBy, ApprovedDate,
     EffectiveFrom, IsActive, CreatedDate, CreatedBy)
SELECT factor.ClientId, factor.Id, seedRule.ValueCode, seedRule.ValueName,
       seedRule.MinimumValue, seedRule.MaximumValue, seedRule.Score, seedRule.Remarks,
       1, N'APPROVED', N'UAT Seed', SYSUTCDATETIME(),
       SYSUTCDATETIME(), 1, SYSUTCDATETIME(), N'RiskScoring.sql'
FROM dbo.RiskFactors factor
INNER JOIN @Rules seedRule ON seedRule.FactorCode = factor.FactorCode
WHERE factor.DeleteFg = 'N'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RiskFactorRules existing
      WHERE existing.RiskFactorId = factor.Id
        AND existing.RuleVersion = 1
        AND ISNULL(existing.ValueCode, N'') = ISNULL(seedRule.ValueCode, N'')
        AND ISNULL(existing.MinimumValue, -1) = ISNULL(seedRule.MinimumValue, -1)
        AND ISNULL(existing.MaximumValue, -1) = ISNULL(seedRule.MaximumValue, -1)
        AND existing.DeleteFg = 'N'
  );

INSERT INTO dbo.RiskModels
    (ClientId, ModelCode, ModelVersion, CustomerWeightPercent,
     TransactionWeightPercent, ScreeningWeightPercent, ApprovalStatus,
     ApprovedBy, ApprovedDate, EffectiveFrom, IsActive, CreatedDate, CreatedBy)
SELECT client.Id, N'AML-RISK', 1, 40, 40, 20, N'APPROVED',
       N'UAT Seed', SYSUTCDATETIME(), SYSUTCDATETIME(), 1,
       SYSUTCDATETIME(), N'RiskScoring.sql'
FROM dbo.Clients client
WHERE client.IsActive = 1 AND client.DeleteFg = 'N'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RiskModels existing
      WHERE existing.ClientId = client.Id
        AND existing.ModelCode = N'AML-RISK'
        AND existing.ModelVersion = 1
        AND existing.DeleteFg = 'N'
  );

DECLARE @Bands TABLE
(
    RiskLevel NVARCHAR(20), MinimumScore INT, MaximumScore INT,
    Decision NVARCHAR(20), Remarks NVARCHAR(1000)
);
INSERT INTO @Bands VALUES
    (N'LOW', 0, 29, N'ALLOW', N'Normal monitoring.'),
    (N'MEDIUM', 30, 59, N'REVIEW', N'Compliance review is required.'),
    (N'HIGH', 60, 79, N'REVIEW', N'Enhanced due diligence is required.'),
    (N'CRITICAL', 80, 100, N'BLOCK', N'Block and escalate for investigation.');

INSERT INTO dbo.RiskBands
    (ClientId, RiskLevel, MinimumScore, MaximumScore, Decision, Remarks,
     BandVersion, ApprovalStatus, ApprovedBy, ApprovedDate,
     IsActive, CreatedDate, CreatedBy)
SELECT client.Id, band.RiskLevel, band.MinimumScore, band.MaximumScore,
       band.Decision, band.Remarks, 1, N'APPROVED', N'UAT Seed',
       SYSUTCDATETIME(), 1, SYSUTCDATETIME(), N'RiskScoring.sql'
FROM dbo.Clients client
CROSS JOIN @Bands band
WHERE client.IsActive = 1 AND client.DeleteFg = 'N'
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RiskBands existing
      WHERE existing.ClientId = client.Id
        AND existing.BandVersion = 1
        AND existing.RiskLevel = band.RiskLevel
        AND existing.DeleteFg = 'N'
  );

/* Portal permissions and menu. */
IF OBJECT_ID(N'dbo.Permissions', N'U') IS NULL OR
   OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL OR
   OBJECT_ID(N'dbo.MenuItems', N'U') IS NULL
    THROW 50301, 'Run RolesPermissions.sql and MenuItems.sql before RiskScoring.sql.', 1;

INSERT INTO dbo.Permissions(PermissionName, Description)
SELECT source.PermissionName, source.Description
FROM (VALUES
    ('RiskScoring.View', 'View risk scoring configuration'),
    ('RiskScoring.Manage', 'Edit risk factor scores and risk bands')
) source(PermissionName, Description)
WHERE NOT EXISTS
(
    SELECT 1 FROM dbo.Permissions existing
    WHERE existing.PermissionName = source.PermissionName
);

UPDATE dbo.Permissions SET IsActive = 1
WHERE PermissionName IN ('RiskScoring.View', 'RiskScoring.Manage');

INSERT INTO dbo.RolePermissions(RoleId, PermissionId)
SELECT role.RoleId, permission.PermissionId
FROM dbo.Roles role
CROSS JOIN dbo.Permissions permission
WHERE role.RoleName = 'Admin'
  AND permission.PermissionName IN ('RiskScoring.View', 'RiskScoring.Manage')
  AND NOT EXISTS
  (
      SELECT 1 FROM dbo.RolePermissions existing
      WHERE existing.RoleId = role.RoleId
        AND existing.PermissionId = permission.PermissionId
  );

DECLARE @ComplianceMenuId INT =
    (SELECT TOP (1) MenuId FROM dbo.MenuItems WHERE MenuKey = 'Compliance');
IF @ComplianceMenuId IS NULL
    THROW 50302, 'The Compliance parent menu is missing. Run MenuItems.sql first.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE MenuKey = 'RiskScoring')
BEGIN
    INSERT INTO dbo.MenuItems
        (ParentMenuId, MenuKey, DisplayName, SectionName, ControllerName,
         ActionName, IconClass, PermissionCode, SortOrder, IsActive)
    VALUES
        (@ComplianceMenuId, 'RiskScoring', N'Risk Scoring', 'MANAGEMENT',
         'RiskScoring', 'Index', 'bi-speedometer2', 'RiskScoring.View', 40, 1);
END;
ELSE
BEGIN
    UPDATE dbo.MenuItems
    SET ParentMenuId = @ComplianceMenuId,
        DisplayName = N'Risk Scoring',
        ControllerName = 'RiskScoring',
        ActionName = 'Index',
        IconClass = 'bi-speedometer2',
        PermissionCode = 'RiskScoring.View',
        SortOrder = 40,
        IsActive = 1
    WHERE MenuKey = 'RiskScoring';
END;

COMMIT TRANSACTION;

SELECT N'Risk scoring schema and UAT seed completed.' AS Result;
SELECT FactorCode, FactorName, ScoreCategory, ValueType, WeightPercent
FROM dbo.RiskFactors WHERE DeleteFg = 'N' ORDER BY ClientId, DisplayOrder;
