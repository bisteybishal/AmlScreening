SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clients PRIMARY KEY,
        ClientCode    NVARCHAR(50) NOT NULL,
        ClientName    NVARCHAR(200) NOT NULL,
        IsActive      BIT NOT NULL CONSTRAINT DF_Clients_IsActive DEFAULT 1,
        CreatedDate   DATETIME2(0) NOT NULL CONSTRAINT DF_Clients_CreatedDate DEFAULT SYSUTCDATETIME(),
        CreatedBy     NVARCHAR(100) NULL,
        UpdatedDate   DATETIME2(0) NULL,
        UpdatedBy     NVARCHAR(100) NULL,
        DeleteFg      CHAR(1) NOT NULL CONSTRAINT DF_Clients_DeleteFg DEFAULT 'N',
        DeleteDate    DATETIME2(0) NULL,
        DeleteBy      NVARCHAR(100) NULL,
        CONSTRAINT UQ_Clients_ClientCode UNIQUE (ClientCode),
        CONSTRAINT CK_Clients_DeleteFg CHECK (DeleteFg IN ('N', 'Y'))
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE ClientCode = N'DEFAULT')
BEGIN
    INSERT INTO dbo.Clients (ClientCode, ClientName, CreatedBy)
    VALUES (N'DEFAULT', N'Default Migration Client', N'System Migration');
END;

DECLARE @DefaultClientId INT =
    (SELECT TOP (1) Id FROM dbo.Clients WHERE ClientCode = N'DEFAULT');

IF COL_LENGTH(N'dbo.Users', N'ClientId') IS NULL
    ALTER TABLE dbo.Users ADD ClientId INT NULL;

EXEC sys.sp_executesql
    N'UPDATE dbo.Users SET ClientId = @ClientId WHERE ClientId IS NULL;',
    N'@ClientId INT',
    @ClientId = @DefaultClientId;
EXEC(N'ALTER TABLE dbo.Users ALTER COLUMN ClientId INT NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Clients')
    EXEC(N'ALTER TABLE dbo.Users ADD CONSTRAINT FK_Users_Clients
        FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Users_ClientId' AND object_id = OBJECT_ID(N'dbo.Users'))
    EXEC(N'CREATE INDEX IX_Users_ClientId ON dbo.Users(ClientId);');

IF NOT EXISTS (SELECT 1 FROM dbo.Users)
    THROW 50010, 'At least one user is required before AML record ownership can be migrated.', 1;

IF COL_LENGTH(N'dbo.AmlRecords', N'ClientId') IS NULL
    ALTER TABLE dbo.AmlRecords ADD ClientId INT NULL;

-- Users.UserId is BIGINT in the existing portal schema.
IF COL_LENGTH(N'dbo.AmlRecords', N'OwnerUserId') IS NULL
    ALTER TABLE dbo.AmlRecords ADD OwnerUserId BIGINT NULL;

EXEC sys.sp_executesql N'
    UPDATE record
    SET ClientId = COALESCE(owner.ClientId, @DefaultClientId),
        OwnerUserId = COALESCE(owner.UserId, fallbackUser.UserId)
    FROM dbo.AmlRecords record
    OUTER APPLY
    (
        SELECT TOP (1) u.UserId, u.ClientId
        FROM dbo.Users u
        WHERE u.Username = record.CreatedBy
        ORDER BY u.UserId
    ) owner
    CROSS APPLY
    (
        SELECT TOP (1) u.UserId
        FROM dbo.Users u
        WHERE u.ClientId = COALESCE(owner.ClientId, @DefaultClientId)
        ORDER BY CASE WHEN u.Username = N''admin'' THEN 0 ELSE 1 END, u.UserId
    ) fallbackUser
    WHERE record.ClientId IS NULL OR record.OwnerUserId IS NULL;',
    N'@DefaultClientId INT',
    @DefaultClientId = @DefaultClientId;

EXEC(N'ALTER TABLE dbo.AmlRecords ALTER COLUMN ClientId INT NOT NULL;');
EXEC(N'ALTER TABLE dbo.AmlRecords ALTER COLUMN OwnerUserId BIGINT NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AmlRecords_Clients')
    EXEC(N'ALTER TABLE dbo.AmlRecords ADD CONSTRAINT FK_AmlRecords_Clients
        FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id);');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AmlRecords_OwnerUser')
    EXEC(N'ALTER TABLE dbo.AmlRecords ADD CONSTRAINT FK_AmlRecords_OwnerUser
        FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(UserId);');

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_AmlRecords_ReferenceNo')
    ALTER TABLE dbo.AmlRecords DROP CONSTRAINT UQ_AmlRecords_ReferenceNo;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_AmlRecords_Client_Reference' AND object_id = OBJECT_ID(N'dbo.AmlRecords'))
    EXEC(N'CREATE UNIQUE INDEX UX_AmlRecords_Client_Reference ON dbo.AmlRecords(ClientId, ReferenceNo);');

IF OBJECT_ID(N'dbo.ComplianceRules', N'U') IS NOT NULL
BEGIN
    UPDATE dbo.ComplianceRules SET ClientId = @DefaultClientId WHERE ClientId IS NULL;

    IF EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.ComplianceRules')
          AND name = N'UQ_ComplianceRules_Code_Client'
    )
        ALTER TABLE dbo.ComplianceRules DROP CONSTRAINT UQ_ComplianceRules_Code_Client;

    ALTER TABLE dbo.ComplianceRules ALTER COLUMN ClientId INT NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.ComplianceRules')
          AND name = N'UQ_ComplianceRules_Code_Client'
    )
        ALTER TABLE dbo.ComplianceRules ADD CONSTRAINT UQ_ComplianceRules_Code_Client
            UNIQUE (ClientId, RuleCode);

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ComplianceRules_Clients')
        ALTER TABLE dbo.ComplianceRules ADD CONSTRAINT FK_ComplianceRules_Clients
            FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id);
END;

COMMIT TRANSACTION;
