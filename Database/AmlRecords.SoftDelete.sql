/* Add soft-delete audit columns without removing existing records. */
IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteFg') IS NULL
BEGIN
    ALTER TABLE dbo.AmlRecords
    ADD DeleteFg CHAR(1) NOT NULL
        CONSTRAINT DF_AmlRecords_DeleteFg DEFAULT 'N' WITH VALUES;
END;

UPDATE dbo.AmlRecords
SET DeleteFg = 'N'
WHERE DeleteFg IS NULL;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.AmlRecords')
      AND name = N'DeleteFg'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.AmlRecords ALTER COLUMN DeleteFg CHAR(1) NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c ON c.default_object_id = dc.object_id
    WHERE c.object_id = OBJECT_ID(N'dbo.AmlRecords')
      AND c.name = N'DeleteFg'
)
BEGIN
    ALTER TABLE dbo.AmlRecords
    ADD CONSTRAINT DF_AmlRecords_DeleteFg DEFAULT 'N' FOR DeleteFg;
END;

IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteDate') IS NULL
BEGIN
    ALTER TABLE dbo.AmlRecords ADD DeleteDate DATETIME2(0) NULL;
END;

IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteBy') IS NULL
BEGIN
    ALTER TABLE dbo.AmlRecords ADD DeleteBy NVARCHAR(100) NULL;
END;

/* Store the authenticated username consistently in every audit field. */
IF COL_LENGTH(N'dbo.AmlRecords', N'CreatedBy') IS NOT NULL
    ALTER TABLE dbo.AmlRecords ALTER COLUMN CreatedBy NVARCHAR(100) NULL;

IF COL_LENGTH(N'dbo.AmlRecords', N'UpdatedBy') IS NOT NULL
    ALTER TABLE dbo.AmlRecords ALTER COLUMN UpdatedBy NVARCHAR(100) NULL;

IF COL_LENGTH(N'dbo.AmlRecords', N'DeleteBy') IS NOT NULL
    ALTER TABLE dbo.AmlRecords ALTER COLUMN DeleteBy NVARCHAR(100) NULL;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_AmlRecords_DeleteFg'
)
BEGIN
    ALTER TABLE dbo.AmlRecords
    ADD CONSTRAINT CK_AmlRecords_DeleteFg CHECK (DeleteFg IN ('N', 'Y'));
END;
