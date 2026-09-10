-- MANUAL ONLY. Back up the database. No changes to existing risk/transaction schemas.
-- Requires your existing dbo.Clients, Customers, AmlRecords, RiskFactors,
-- RiskFactorRules and RiskBands with the columns used in 02-procedures.sql.
SET XACT_ABORT ON;
GO
IF SCHEMA_ID(N'kyc') IS NULL EXEC(N'CREATE SCHEMA kyc');
GO
IF OBJECT_ID(N'kyc.PartnerAccess') IS NULL
CREATE TABLE kyc.PartnerAccess
(
    OAuthClientId NVARCHAR(100) COLLATE Latin1_General_100_BIN2 NOT NULL PRIMARY KEY,
    ClientId INT NOT NULL REFERENCES dbo.Clients(Id),
    IsActive BIT NOT NULL DEFAULT 1,
    AllowClearDecisions BIT NOT NULL DEFAULT 0
);
GO
IF OBJECT_ID(N'kyc.Assessments') IS NULL
CREATE TABLE kyc.Assessments
(
    Id BIGINT IDENTITY PRIMARY KEY,
    ClientId INT NOT NULL REFERENCES dbo.Clients(Id),
    CustomerId INT NOT NULL REFERENCES dbo.Customers(Id),
    OAuthClientId NVARCHAR(100) NOT NULL,
    ClientReference NVARCHAR(100) NOT NULL,
    IdempotencyKey NVARCHAR(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
    RequestHash BINARY(32) NOT NULL,
    CorrelationId VARCHAR(32) NOT NULL UNIQUE,
    RiskScore INT NULL CHECK (RiskScore BETWEEN 0 AND 100),
    RiskLevel NVARCHAR(20) NULL,
    CalculationStatus VARCHAR(20) NOT NULL,
    Decision VARCHAR(20) NOT NULL CHECK (Decision IN ('ALLOW','REVIEW','BLOCK')),
    BandId INT NULL, BandVersion INT NULL,
    ResponseJson NVARCHAR(MAX) NOT NULL CHECK (ISJSON(ResponseJson)=1),
    InputSnapshot NVARCHAR(MAX) NOT NULL, -- Encrypted by ASP.NET Core Data Protection.
    CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UQ_Kyc_Client_Retry UNIQUE(ClientId,IdempotencyKey)
);
GO
IF OBJECT_ID(N'kyc.AssessmentFactors') IS NULL
CREATE TABLE kyc.AssessmentFactors
(
    Id BIGINT IDENTITY PRIMARY KEY,
    AssessmentId BIGINT NOT NULL REFERENCES kyc.Assessments(Id),
    FactorCode NVARCHAR(100) NOT NULL,
    InputValue NVARCHAR(200) NOT NULL,
    RuleId INT NULL, RuleVersion INT NULL, RawScore INT NULL,
    WeightPercent DECIMAL(9,4) NOT NULL,
    WeightedScore DECIMAL(18,6) NULL
);
GO
IF OBJECT_ID(N'kyc.AssessmentMatches') IS NULL
CREATE TABLE kyc.AssessmentMatches
(
    Id BIGINT IDENTITY PRIMARY KEY,
    AssessmentId BIGINT NOT NULL REFERENCES kyc.Assessments(Id),
    AmlRecordId INT NOT NULL,
    SourceName NVARCHAR(150) NOT NULL,
    ReferenceNo NVARCHAR(200) NULL,
    MatchMethod VARCHAR(30) NOT NULL DEFAULT 'EXACT_NAME',
    MatchScore INT NOT NULL DEFAULT 100
);
GO
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('kyc.Assessments') AND name='IX_Kyc_Customer')
CREATE INDEX IX_Kyc_Customer ON kyc.Assessments(ClientId,CustomerId,CreatedAt);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('kyc.AssessmentFactors') AND name='IX_Kyc_Factors')
CREATE INDEX IX_Kyc_Factors ON kyc.AssessmentFactors(AssessmentId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('kyc.AssessmentMatches') AND name='IX_Kyc_Matches')
CREATE INDEX IX_Kyc_Matches ON kyc.AssessmentMatches(AssessmentId);
GO
