/* Read-only database inventory and post-restore verification. */

SET NOCOUNT ON;

SELECT schema_name(schema_id) AS SchemaName,
       name AS TableName,
       create_date AS CreatedDate,
       modify_date AS ModifiedDate
FROM sys.tables
WHERE is_ms_shipped = 0
ORDER BY SchemaName, TableName;

SELECT schema_name(schema_id) AS SchemaName,
       name AS ProcedureName,
       create_date AS CreatedDate,
       modify_date AS ModifiedDate
FROM sys.procedures
WHERE is_ms_shipped = 0
ORDER BY SchemaName, ProcedureName;

SELECT fk.name AS ForeignKeyName,
       object_schema_name(fk.parent_object_id) + N'.' +
           object_name(fk.parent_object_id) AS ChildTable,
       col_name(fkc.parent_object_id, fkc.parent_column_id) AS ChildColumn,
       object_schema_name(fk.referenced_object_id) + N'.' +
           object_name(fk.referenced_object_id) AS ParentTable,
       col_name(fkc.referenced_object_id, fkc.referenced_column_id) AS ParentColumn
FROM sys.foreign_keys AS fk
INNER JOIN sys.foreign_key_columns AS fkc
    ON fkc.constraint_object_id = fk.object_id
ORDER BY ChildTable, ForeignKeyName;

SELECT object_schema_name(indexes.object_id) + N'.' +
           object_name(indexes.object_id) AS TableName,
       indexes.name AS IndexName,
       indexes.is_primary_key AS IsPrimaryKey,
       indexes.is_unique AS IsUnique,
       indexes.is_disabled AS IsDisabled
FROM sys.indexes AS indexes
INNER JOIN sys.tables AS tables
    ON tables.object_id = indexes.object_id
WHERE indexes.name IS NOT NULL
  AND tables.is_ms_shipped = 0
ORDER BY TableName, IndexName;

SELECT N'dbo.Clients' AS TableName, COUNT_BIG(*) AS RowCount FROM dbo.Clients
UNION ALL SELECT N'dbo.Users', COUNT_BIG(*) FROM dbo.Users
UNION ALL SELECT N'dbo.AmlRecords', COUNT_BIG(*) FROM dbo.AmlRecords
UNION ALL SELECT N'dbo.ComplianceRules', COUNT_BIG(*) FROM dbo.ComplianceRules
UNION ALL SELECT N'dbo.Customers', COUNT_BIG(*) FROM dbo.Customers
UNION ALL SELECT N'dbo.AmlTransactions', COUNT_BIG(*) FROM dbo.AmlTransactions
UNION ALL SELECT N'dbo.ScreeningMatches', COUNT_BIG(*) FROM dbo.ScreeningMatches
UNION ALL SELECT N'dbo.RiskAssessments', COUNT_BIG(*) FROM dbo.RiskAssessments
UNION ALL SELECT N'dbo.RiskFactors', COUNT_BIG(*) FROM dbo.RiskFactors
UNION ALL SELECT N'dbo.RiskFactorRules', COUNT_BIG(*) FROM dbo.RiskFactorRules
UNION ALL SELECT N'dbo.RiskBands', COUNT_BIG(*) FROM dbo.RiskBands
UNION ALL SELECT N'dbo.RiskModels', COUNT_BIG(*) FROM dbo.RiskModels
UNION ALL SELECT N'dbo.RiskAssessmentFactors', COUNT_BIG(*) FROM dbo.RiskAssessmentFactors
UNION ALL SELECT N'dbo.AmlDecisions', COUNT_BIG(*) FROM dbo.AmlDecisions
UNION ALL SELECT N'dbo.ComplianceAlerts', COUNT_BIG(*) FROM dbo.ComplianceAlerts
UNION ALL SELECT N'dbo.ComplianceCases', COUNT_BIG(*) FROM dbo.ComplianceCases
UNION ALL SELECT N'dbo.AuditLogs', COUNT_BIG(*) FROM dbo.AuditLogs;
