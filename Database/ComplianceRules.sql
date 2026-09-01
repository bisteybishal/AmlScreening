SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.ComplianceRules', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComplianceRules
    (
        Id                INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ComplianceRules PRIMARY KEY,
        ClientId          INT NOT NULL,
        RuleCode          NVARCHAR(50) NOT NULL,
        RuleName          NVARCHAR(150) NOT NULL,
        RuleType          NVARCHAR(50) NOT NULL,
        ThresholdAmount   DECIMAL(18,2) NULL,
        Currency          NVARCHAR(3) NULL,
        LookbackMinutes   INT NULL,
        TransactionCount  INT NULL,
        RiskScore         INT NOT NULL,
        Action            NVARCHAR(20) NOT NULL,
        RuleVersion       INT NOT NULL CONSTRAINT DF_ComplianceRules_RuleVersion DEFAULT 1,
        Priority          INT NOT NULL CONSTRAINT DF_ComplianceRules_Priority DEFAULT 100,
        ApprovalStatus    NVARCHAR(20) NOT NULL CONSTRAINT DF_ComplianceRules_ApprovalStatus DEFAULT N'Active',
        ApprovedBy        NVARCHAR(100) NULL,
        ApprovedDate      DATETIME2(0) NULL,
        EffectiveFrom     DATETIME2(0) NOT NULL,
        EffectiveTo       DATETIME2(0) NULL,
        IsActive          BIT NOT NULL CONSTRAINT DF_ComplianceRules_IsActive DEFAULT 1,
        CreatedDate       DATETIME2(0) NOT NULL CONSTRAINT DF_ComplianceRules_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy         NVARCHAR(100) NULL,
        UpdatedDate       DATETIME2(0) NULL,
        UpdatedBy         NVARCHAR(100) NULL,
        DeleteFg          CHAR(1) NOT NULL CONSTRAINT DF_ComplianceRules_DeleteFg DEFAULT 'N',
        DeleteDate        DATETIME2(0) NULL,
        DeleteBy          NVARCHAR(100) NULL,
        CONSTRAINT CK_ComplianceRules_DeleteFg CHECK (DeleteFg IN ('N', 'Y')),
        CONSTRAINT CK_ComplianceRules_RiskScore CHECK (RiskScore BETWEEN 0 AND 100),
        CONSTRAINT CK_ComplianceRules_EffectiveDates CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    );

    CREATE INDEX IX_ComplianceRules_ActiveType
        ON dbo.ComplianceRules (IsActive, RuleType, EffectiveFrom);

    CREATE UNIQUE INDEX UX_ComplianceRules_ClientRuleCode
        ON dbo.ComplianceRules (ClientId, RuleCode)
        WHERE DeleteFg = 'N';
END;
