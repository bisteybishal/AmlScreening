/* MANUAL MIGRATION, maintenance window only. Take a tested backup first.
   Single transaction: a failure rolls back this script. No user/source grants,
   no guessed owner assignment, no deletion of business records.
   Target schema: SQL Server 2022 and this repository's PortalAmlWorkflow.sql.
   Run 00-preflight first. Stop portal/API/scheduler before changing schema. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;

BEGIN TRY
 BEGIN TRANSACTION;
 IF OBJECT_ID(N'dbo.Users',N'U') IS NULL OR OBJECT_ID(N'dbo.Clients',N'U') IS NULL
    OR OBJECT_ID(N'dbo.AmlRecords',N'U') IS NULL OR OBJECT_ID(N'dbo.Customers',N'U') IS NULL
    OR OBJECT_ID(N'dbo.AmlTransactions',N'U') IS NULL
    THROW 51100,'Required existing tables are missing. Stop and review preflight.',1;
 IF TYPE_NAME((SELECT system_type_id FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'UserId'))<>N'bigint'
    THROW 51101,'This migration expects Users.UserId BIGINT. Adapt foreign-key types before running.',1;
 IF COL_LENGTH(N'dbo.AmlRecords',N'OwnerUserId') IS NULL
    OR COL_LENGTH(N'dbo.AmlRecords',N'IsShared') IS NULL
    OR COL_LENGTH(N'dbo.UserPermissions',N'IsGranted') IS NULL
    THROW 51102,'Existing ownership/permission columns are missing. Stop and reconcile schema.',1;

 IF OBJECT_ID(N'dbo.AmlSources',N'U') IS NULL
 CREATE TABLE dbo.AmlSources
 (
   Id INT IDENTITY PRIMARY KEY,
   SourceCode NVARCHAR(20) NOT NULL CONSTRAINT UQ_Ownership_AmlSources_Code UNIQUE,
   SourceName NVARCHAR(150) NOT NULL CONSTRAINT UQ_Ownership_AmlSources_Name UNIQUE,
   IsActive BIT NOT NULL CONSTRAINT DF_Ownership_AmlSources_Active DEFAULT(1)
 );
 IF COL_LENGTH(N'dbo.AmlSources',N'Id') IS NULL OR COL_LENGTH(N'dbo.AmlSources',N'SourceCode') IS NULL
    OR COL_LENGTH(N'dbo.AmlSources',N'SourceName') IS NULL OR COL_LENGTH(N'dbo.AmlSources',N'IsActive') IS NULL
    THROW 51103,'Existing AmlSources has a different schema. Do not overwrite it.',1;
 EXEC(N'INSERT dbo.AmlSources(SourceCode,SourceName,IsActive)
 SELECT v.Code,v.Name,1 FROM (VALUES(N''UN'',N''UN Sanctions''),(N''EU'',N''EU Sanctions''),
 (N''PEP'',N''PEP''),(N''CIB'',N''CIB''))v(Code,Name)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.AmlSources s WHERE s.SourceCode=v.Code);');

 IF OBJECT_ID(N'dbo.UserAmlSourceAccess',N'U') IS NULL
 CREATE TABLE dbo.UserAmlSourceAccess
 (
   UserId BIGINT NOT NULL REFERENCES dbo.Users(UserId),
   SourceId INT NOT NULL REFERENCES dbo.AmlSources(Id),
   CONSTRAINT PK_UserAmlSourceAccess PRIMARY KEY(UserId,SourceId)
 );
 IF OBJECT_ID(N'dbo.ApiApplications',N'U') IS NULL
 CREATE TABLE dbo.ApiApplications
 (
   Id INT IDENTITY PRIMARY KEY,
   Issuer NVARCHAR(300) COLLATE Latin1_General_100_BIN2 NOT NULL,
   Subject NVARCHAR(300) COLLATE Latin1_General_100_BIN2 NOT NULL,
   OwnerUserId BIGINT NOT NULL REFERENCES dbo.Users(UserId),
   IsActive BIT NOT NULL CONSTRAINT DF_Ownership_ApiApplications_Active DEFAULT(1),
   CONSTRAINT UQ_ApiApplications_Identity UNIQUE(Issuer,Subject)
 );
 IF COL_LENGTH(N'dbo.ApiApplications',N'Issuer') IS NULL OR COL_LENGTH(N'dbo.ApiApplications',N'Subject') IS NULL
    OR COL_LENGTH(N'dbo.ApiApplications',N'OwnerUserId') IS NULL
    THROW 51104,'Existing ApiApplications differs from the supplied API mapping contract.',1;

 IF OBJECT_ID(N'dbo.AmlSyncRuns',N'U') IS NULL
 CREATE TABLE dbo.AmlSyncRuns
 (
   Id BIGINT IDENTITY PRIMARY KEY,
   SourceCode NVARCHAR(20) NOT NULL,
   StartedAtUtc DATETIME2 NOT NULL,
   CompletedAtUtc DATETIME2 NULL,
   Status NVARCHAR(20) NOT NULL,
   RecordCount INT NOT NULL CONSTRAINT DF_AmlSyncRuns_Count DEFAULT(0),
   Error NVARCHAR(500) NULL
 );
 IF OBJECT_ID(N'dbo.OwnershipConfigurationAudit',N'U') IS NULL
 CREATE TABLE dbo.OwnershipConfigurationAudit
 (
   Id BIGINT IDENTITY PRIMARY KEY,
   ChangedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_OwnershipConfigAudit_Date DEFAULT SYSUTCDATETIME(),
   ChangedByUserId BIGINT NOT NULL REFERENCES dbo.Users(UserId),
   ChangeType NVARCHAR(50) NOT NULL,
   TargetUserId BIGINT NULL REFERENCES dbo.Users(UserId),
   Details NVARCHAR(2000) NOT NULL
 );

 IF COL_LENGTH(N'dbo.AmlRecords',N'SourceId') IS NULL
   ALTER TABLE dbo.AmlRecords ADD SourceId INT NULL;
 IF COL_LENGTH(N'dbo.AmlRecords',N'Photo') IS NULL
   ALTER TABLE dbo.AmlRecords ADD Photo VARBINARY(MAX) NULL;
 IF COL_LENGTH(N'dbo.AmlRecords',N'PhotoContentType') IS NULL
   ALTER TABLE dbo.AmlRecords ADD PhotoContentType NVARCHAR(100) NULL;
 IF COL_LENGTH(N'dbo.Users',N'CanAccessPortal') IS NULL
 BEGIN
   ALTER TABLE dbo.Users ADD CanAccessPortal BIT NOT NULL CONSTRAINT DF_Users_CanAccessPortal DEFAULT(0);
   -- Preserve the existing intended portal accounts. API-only accounts require an explicit grant.
   -- This backfill runs only when adding the column, never on subsequent migration runs.
   EXEC(N'UPDATE u SET CanAccessPortal=1 FROM dbo.Users u WHERE EXISTS
     (SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId=ur.RoleId
      WHERE ur.UserId=u.UserId AND r.IsActive=1 AND REPLACE(r.RoleName,N'' '',N'''')<>N''APIUser'');');
 END;
 IF COL_LENGTH(N'dbo.Users',N'ActiveSessionId') IS NULL
   ALTER TABLE dbo.Users ADD ActiveSessionId UNIQUEIDENTIFIER NULL;
 IF COL_LENGTH(N'dbo.Users',N'SessionStartedAtUtc') IS NULL
   ALTER TABLE dbo.Users ADD SessionStartedAtUtc DATETIME2(7) NULL;
 IF COL_LENGTH(N'dbo.Users',N'SessionExpiresAtUtc') IS NULL
   ALTER TABLE dbo.Users ADD SessionExpiresAtUtc DATETIME2(7) NULL;
 IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.AmlRecords') AND name=N'OwnerUserId' AND is_nullable=0)
   ALTER TABLE dbo.AmlRecords ALTER COLUMN OwnerUserId BIGINT NULL;
 IF COL_LENGTH(N'dbo.Customers',N'OwnerUserId') IS NULL
   ALTER TABLE dbo.Customers ADD OwnerUserId BIGINT NULL;
 IF COL_LENGTH(N'dbo.Customers',N'IdentificationType') IS NULL
   ALTER TABLE dbo.Customers ADD IdentificationType NVARCHAR(50) NULL;
 IF COL_LENGTH(N'dbo.Customers',N'IdentificationNumber') IS NULL
   ALTER TABLE dbo.Customers ADD IdentificationNumber NVARCHAR(100) NULL;
 IF COL_LENGTH(N'dbo.AmlTransactions',N'OwnerUserId') IS NULL
   ALTER TABLE dbo.AmlTransactions ADD OwnerUserId BIGINT NULL;

 IF OBJECT_ID(N'dbo.CustomerKycResults',N'U') IS NULL
 CREATE TABLE dbo.CustomerKycResults
 (
   Id BIGINT IDENTITY PRIMARY KEY,
   ClientId INT NOT NULL REFERENCES dbo.Clients(Id),
   OwnerUserId BIGINT NULL REFERENCES dbo.Users(UserId),
   ApiApplicationId INT NULL REFERENCES dbo.ApiApplications(Id),
   SubmissionChannel NVARCHAR(20) NOT NULL CONSTRAINT DF_CustomerKycResults_Channel DEFAULT(N'API'),
   CustomerId INT NOT NULL REFERENCES dbo.Customers(Id),
   CustomerReference NVARCHAR(100) NOT NULL,
   RiskScore INT NOT NULL CHECK(RiskScore BETWEEN 0 AND 100),
   RiskLevel NVARCHAR(20) NOT NULL,
   Decision NVARCHAR(20) NOT NULL,
   RiskBandId INT NOT NULL REFERENCES dbo.RiskBands(Id),
   BandVersion INT NOT NULL,
   CalculatedAtUtc DATETIME2 NOT NULL,
   FactorsJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(FactorsJson)=1),
   RequestJson NVARCHAR(MAX) NULL CHECK(RequestJson IS NULL OR ISJSON(RequestJson)=1),
   AmlResultJson NVARCHAR(MAX) NOT NULL CHECK(ISJSON(AmlResultJson)=1),
   CorrelationId NVARCHAR(100) NOT NULL
 );
 ELSE
 BEGIN
   IF COL_LENGTH(N'dbo.CustomerKycResults',N'RequestJson') IS NULL
     ALTER TABLE dbo.CustomerKycResults ADD RequestJson NVARCHAR(MAX) NULL;
   IF COL_LENGTH(N'dbo.CustomerKycResults',N'OwnerUserId') IS NULL
     ALTER TABLE dbo.CustomerKycResults ADD OwnerUserId BIGINT NULL;
   IF COL_LENGTH(N'dbo.CustomerKycResults',N'ApiApplicationId') IS NULL
     ALTER TABLE dbo.CustomerKycResults ADD ApiApplicationId INT NULL;
   IF COL_LENGTH(N'dbo.CustomerKycResults',N'SubmissionChannel') IS NULL
     ALTER TABLE dbo.CustomerKycResults ADD SubmissionChannel NVARCHAR(20) NULL;
 END;

 /* Indexes below deliberately replace ONLY the known old owner-blind indexes.
    Unexpected unique indexes are not dropped: preflight must reconcile those.
    Dropping an index does not delete any records. The transaction restores it on failure. */
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AmlRecords') AND name=N'UX_AmlRecords_Client_Source_Reference' AND is_unique_constraint=0)
    DROP INDEX UX_AmlRecords_Client_Source_Reference ON dbo.AmlRecords;
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Customers') AND name=N'UX_Customers_ClientReference' AND is_unique_constraint=0)
    DROP INDEX UX_Customers_ClientReference ON dbo.Customers;
 IF EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AmlTransactions') AND name=N'UX_AmlTransactions_ClientReference' AND is_unique_constraint=0)
    DROP INDEX UX_AmlTransactions_ClientReference ON dbo.AmlTransactions;
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AmlRecords') AND name=N'UX_AmlRecords_PrivateOwnerReference')
    EXEC(N'CREATE UNIQUE INDEX UX_AmlRecords_PrivateOwnerReference ON dbo.AmlRecords(ClientId,OwnerUserId,SourceName,ReferenceNo) WHERE IsShared=0 AND OwnerUserId IS NOT NULL;');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AmlRecords') AND name=N'UX_AmlRecords_SharedSourceReference')
    EXEC(N'CREATE UNIQUE INDEX UX_AmlRecords_SharedSourceReference ON dbo.AmlRecords(SourceId,ReferenceNo) WHERE IsShared=1 AND SourceId IS NOT NULL;');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Customers') AND name=N'UX_Customers_OwnerReference')
    EXEC(N'CREATE UNIQUE INDEX UX_Customers_OwnerReference ON dbo.Customers(ClientId,OwnerUserId,CustomerReference) WHERE OwnerUserId IS NOT NULL;');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AmlTransactions') AND name=N'UX_AmlTransactions_OwnerReference')
    EXEC(N'CREATE UNIQUE INDEX UX_AmlTransactions_OwnerReference ON dbo.AmlTransactions(ClientId,OwnerUserId,TransactionReference) WHERE OwnerUserId IS NOT NULL;');
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.CustomerKycResults') AND name=N'IX_CustomerKycResults_OwnerDate')
    EXEC(N'CREATE INDEX IX_CustomerKycResults_OwnerDate ON dbo.CustomerKycResults(ClientId,OwnerUserId,CalculatedAtUtc DESC) INCLUDE(CustomerId,CustomerReference,RiskScore,RiskLevel,Decision);');

 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_AmlRecords_Source')
    EXEC(N'ALTER TABLE dbo.AmlRecords WITH CHECK ADD CONSTRAINT FK_Ownership_AmlRecords_Source FOREIGN KEY(SourceId) REFERENCES dbo.AmlSources(Id);');
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_Customers_User')
    EXEC(N'ALTER TABLE dbo.Customers WITH CHECK ADD CONSTRAINT FK_Ownership_Customers_User FOREIGN KEY(OwnerUserId) REFERENCES dbo.Users(UserId);');
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_AmlTransactions_User')
    EXEC(N'ALTER TABLE dbo.AmlTransactions WITH CHECK ADD CONSTRAINT FK_Ownership_AmlTransactions_User FOREIGN KEY(OwnerUserId) REFERENCES dbo.Users(UserId);');

 /* Composite FKs prevent linking a user's transaction/result to another owner's customer. */
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'UX_Ownership_Users_UserClient')
    CREATE UNIQUE INDEX UX_Ownership_Users_UserClient ON dbo.Users(UserId,ClientId);
 IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Customers') AND name=N'UX_Ownership_Customers_IdClientOwner')
    EXEC(N'CREATE UNIQUE INDEX UX_Ownership_Customers_IdClientOwner ON dbo.Customers(Id,ClientId,OwnerUserId);');
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_Customers_UserClient')
    EXEC(N'ALTER TABLE dbo.Customers WITH CHECK ADD CONSTRAINT FK_Ownership_Customers_UserClient FOREIGN KEY(OwnerUserId,ClientId) REFERENCES dbo.Users(UserId,ClientId);');
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_Transactions_CustomerOwner')
    EXEC(N'ALTER TABLE dbo.AmlTransactions WITH CHECK ADD CONSTRAINT FK_Ownership_Transactions_CustomerOwner FOREIGN KEY(CustomerId,ClientId,OwnerUserId) REFERENCES dbo.Customers(Id,ClientId,OwnerUserId);');
 IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ownership_KycResults_CustomerOwner')
    EXEC(N'ALTER TABLE dbo.CustomerKycResults WITH CHECK ADD CONSTRAINT FK_Ownership_KycResults_CustomerOwner FOREIGN KEY(CustomerId,ClientId,OwnerUserId) REFERENCES dbo.Customers(Id,ClientId,OwnerUserId);');

 INSERT dbo.Permissions(PermissionName,Description,IsActive)
 SELECT v.Code,v.Description,1
 FROM (VALUES(N'Customers.Manage',N'Create or update authorized customer records'),
             (N'Transactions.Manage',N'Create or evaluate authorized transactions'),
             (N'Screening.View',N'Run screening against authorized records')) v(Code,Description)
 WHERE NOT EXISTS(SELECT 1 FROM dbo.Permissions p WHERE p.PermissionName=v.Code);
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
