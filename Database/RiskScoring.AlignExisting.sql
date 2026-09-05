-- Upgrade the earlier, simplified risk tables before running RiskScoring.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @Tables TABLE (Name sysname);
INSERT INTO @Tables VALUES ('RiskFactors'),('RiskFactorRules'),('RiskBands'),('RiskModels'),('RiskAssessmentFactors');
DECLARE @Columns TABLE (Name sysname, Definition nvarchar(500));
INSERT INTO @Columns VALUES
('IsActive','bit NOT NULL DEFAULT 1 WITH VALUES'),
('CreatedDate','datetime2 NOT NULL DEFAULT SYSUTCDATETIME() WITH VALUES'),
('CreatedBy','nvarchar(100) NULL'),
('UpdatedDate','datetime2 NULL'),('UpdatedBy','nvarchar(100) NULL'),
('DeleteFg','char(1) NOT NULL DEFAULT ''N'' WITH VALUES'),
('DeleteDate','datetime2 NULL'),('DeleteBy','nvarchar(100) NULL'),
('RowVersion','rowversion NOT NULL');
DECLARE @table sysname, @column sysname, @definition nvarchar(500), @sql nvarchar(max);
DECLARE missing CURSOR LOCAL FAST_FORWARD FOR
SELECT t.Name,c.Name,c.Definition FROM @Tables t CROSS JOIN @Columns c
WHERE OBJECT_ID('dbo.'+t.Name,'U') IS NOT NULL AND COL_LENGTH('dbo.'+t.Name,c.Name) IS NULL;
OPEN missing;
FETCH NEXT FROM missing INTO @table,@column,@definition;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ADD '+QUOTENAME(@column)+N' '+@definition;
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM missing INTO @table,@column,@definition;
END;
CLOSE missing;
DEALLOCATE missing;
DECLARE @Specific TABLE (TableName sysname, ColumnName sysname, Definition nvarchar(500));
INSERT INTO @Specific VALUES
('RiskFactorRules','RuleVersion','int NOT NULL DEFAULT 1 WITH VALUES'),
('RiskFactorRules','ApprovalStatus','nvarchar(30) NOT NULL DEFAULT ''DRAFT'' WITH VALUES'),
('RiskFactorRules','ApprovedBy','nvarchar(100) NULL'),
('RiskFactorRules','ApprovedDate','datetime2 NULL'),
('RiskFactorRules','EffectiveFrom','datetime2 NOT NULL DEFAULT SYSUTCDATETIME() WITH VALUES'),
('RiskFactorRules','EffectiveTo','datetime2 NULL'),
('RiskBands','BandVersion','int NOT NULL DEFAULT 1 WITH VALUES'),
('RiskBands','ApprovalStatus','nvarchar(30) NOT NULL DEFAULT ''DRAFT'' WITH VALUES'),
('RiskBands','ApprovedBy','nvarchar(100) NULL'),
('RiskBands','ApprovedDate','datetime2 NULL'),
('RiskModels','ModelVersion','int NOT NULL DEFAULT 1 WITH VALUES'),
('RiskModels','ApprovalStatus','nvarchar(30) NOT NULL DEFAULT ''DRAFT'' WITH VALUES'),
('RiskModels','ApprovedBy','nvarchar(100) NULL'),
('RiskModels','ApprovedDate','datetime2 NULL'),
('RiskModels','EffectiveFrom','datetime2 NOT NULL DEFAULT SYSUTCDATETIME() WITH VALUES'),
('RiskModels','EffectiveTo','datetime2 NULL'),
('RiskAssessmentFactors','RuleVersion','int NOT NULL DEFAULT 1 WITH VALUES');
DECLARE specifics CURSOR LOCAL FAST_FORWARD FOR
SELECT TableName,ColumnName,Definition FROM @Specific
WHERE OBJECT_ID('dbo.'+TableName,'U') IS NOT NULL AND COL_LENGTH('dbo.'+TableName,ColumnName) IS NULL;
OPEN specifics;
FETCH NEXT FROM specifics INTO @table,@column,@definition;
WHILE @@FETCH_STATUS=0
BEGIN
    SET @sql=N'ALTER TABLE dbo.'+QUOTENAME(@table)+N' ADD '+QUOTENAME(@column)+N' '+@definition;
    EXEC sys.sp_executesql @sql;
    FETCH NEXT FROM specifics INTO @table,@column,@definition;
END;
CLOSE specifics;
DEALLOCATE specifics;
IF OBJECT_ID('dbo.RiskFactorRules','U') IS NOT NULL
    ALTER TABLE dbo.RiskFactorRules ALTER COLUMN Remarks nvarchar(1000) NULL;
IF OBJECT_ID('dbo.RiskBands','U') IS NOT NULL
BEGIN
    ALTER TABLE dbo.RiskBands ALTER COLUMN Remarks nvarchar(1000) NULL;
    ALTER TABLE dbo.RiskBands ALTER COLUMN MaximumScore int NULL;
END;
IF OBJECT_ID('dbo.RiskAssessmentFactors','U') IS NOT NULL
BEGIN
    UPDATE dbo.RiskAssessmentFactors SET Explanation=N'' WHERE Explanation IS NULL;
    ALTER TABLE dbo.RiskAssessmentFactors ALTER COLUMN Explanation nvarchar(1000) NOT NULL;
END;
COMMIT;
