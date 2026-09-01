SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
    THROW 50020, 'Run ClientIsolation.sql before AuditLogs.sql.', 1;

DECLARE @DefaultClientId INT =
    (SELECT TOP (1) Id FROM dbo.Clients WHERE ClientCode = N'DEFAULT');

IF OBJECT_ID(N'dbo.AuditLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLogs
    (
        AuditLogId  BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLogs PRIMARY KEY,
        ClientId    INT NOT NULL,
        UserId      BIGINT NOT NULL,
        Username    NVARCHAR(100) NOT NULL,
        Action      NVARCHAR(100) NOT NULL,
        EntityType  NVARCHAR(100) NOT NULL,
        EntityId    NVARCHAR(100) NULL,
        Details     NVARCHAR(2000) NULL,
        CreatedDate DATETIME2(0) NOT NULL CONSTRAINT DF_AuditLogs_CreatedDate DEFAULT SYSUTCDATETIME()
    );
END;
ELSE
BEGIN
    -- Preserve the legacy Module, Description and IpAddress columns.
    IF COL_LENGTH(N'dbo.AuditLogs', N'ClientId') IS NULL
        ALTER TABLE dbo.AuditLogs ADD ClientId INT NULL;
    IF COL_LENGTH(N'dbo.AuditLogs', N'Username') IS NULL
        ALTER TABLE dbo.AuditLogs ADD Username NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AuditLogs', N'EntityType') IS NULL
        ALTER TABLE dbo.AuditLogs ADD EntityType NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AuditLogs', N'EntityId') IS NULL
        ALTER TABLE dbo.AuditLogs ADD EntityId NVARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.AuditLogs', N'Details') IS NULL
        ALTER TABLE dbo.AuditLogs ADD Details NVARCHAR(2000) NULL;

    EXEC sys.sp_executesql N'
        UPDATE audit
        SET ClientId = COALESCE(audit.ClientId, users.ClientId, @DefaultClientId),
            Username = COALESCE(audit.Username, users.Username, N''System''),
            EntityType = COALESCE(audit.EntityType, audit.Module, N''Legacy''),
            Details = COALESCE(audit.Details, audit.Description)
        FROM dbo.AuditLogs audit
        LEFT JOIN dbo.Users users ON users.UserId = audit.UserId
        WHERE audit.ClientId IS NULL OR audit.Username IS NULL OR audit.EntityType IS NULL;',
        N'@DefaultClientId INT',
        @DefaultClientId = @DefaultClientId;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AuditLogs_Clients')
    EXEC(N'ALTER TABLE dbo.AuditLogs ADD CONSTRAINT FK_AuditLogs_Clients
        FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Client_CreatedDate' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    EXEC(N'CREATE INDEX IX_AuditLogs_Client_CreatedDate ON dbo.AuditLogs(ClientId, CreatedDate DESC);');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLogs_Entity' AND object_id = OBJECT_ID(N'dbo.AuditLogs'))
    EXEC(N'CREATE INDEX IX_AuditLogs_Entity ON dbo.AuditLogs(EntityType, EntityId, CreatedDate DESC);');

COMMIT TRANSACTION;
