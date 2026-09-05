/* Read-only preflight for the portal CRUD upgrade. Run on UAT before deployment.
   No migrator and no new tables/SPs are needed by this upgrade.
   Existing installations first need RiskScoring.AlignExisting.sql, RiskScoring.sql,
   then RiskScoring.SplitMenus.sql. Review scripts before using a live database.
   RiskScoring.sql contains EXAMPLE UAT scoring values, not approved production policy.
*/
SET NOCOUNT ON;

DECLARE @Required TABLE(TableName sysname, ColumnName sysname);
INSERT @Required VALUES
('RiskFactorRules','RuleVersion'), ('RiskFactorRules','ApprovalStatus'),
('RiskFactorRules','ApprovedBy'), ('RiskFactorRules','ApprovedDate'),
('RiskFactorRules','EffectiveFrom'), ('RiskFactorRules','EffectiveTo'),
('RiskFactorRules','DeleteFg'), ('RiskFactorRules','DeleteDate'),
('RiskFactorRules','DeleteBy'), ('RiskFactorRules','RowVersion'),
('RiskBands','BandVersion'), ('RiskBands','ApprovalStatus'),
('RiskBands','ApprovedBy'), ('RiskBands','ApprovedDate'),
('RiskBands','DeleteFg'), ('RiskBands','DeleteDate'),
('RiskBands','DeleteBy'), ('RiskBands','RowVersion'),
('AuditLogs','ClientId'), ('AuditLogs','Username'), ('AuditLogs','EntityType'),
('AuditLogs','EntityId'), ('AuditLogs','Details');

SELECT TableName, ColumnName AS MissingColumn FROM @Required
WHERE COL_LENGTH('dbo.' + TableName, ColumnName) IS NULL;

IF EXISTS (SELECT 1 FROM @Required WHERE COL_LENGTH('dbo.' + TableName, ColumnName) IS NULL)
    THROW 50310, 'Risk CRUD schema is incomplete. Review the alignment script before deployment.', 1;

SELECT PermissionName, IsActive FROM dbo.Permissions
WHERE PermissionName IN ('RiskScoring.View', 'RiskScoring.Manage');

SELECT MenuKey, DisplayName, ControllerName, ActionName, PermissionCode, IsActive
FROM dbo.MenuItems WHERE MenuKey IN ('RiskScoring', 'RiskScoreSetup');

SELECT ClientId, RiskFactorId, RuleVersion, ApprovalStatus, COUNT(*) AS RowsIncludingDeleted
FROM dbo.RiskFactorRules GROUP BY ClientId, RiskFactorId, RuleVersion, ApprovalStatus
ORDER BY ClientId, RiskFactorId, RuleVersion;

SELECT ClientId, BandVersion, ApprovalStatus, COUNT(*) AS RowsIncludingDeleted
FROM dbo.RiskBands GROUP BY ClientId, BandVersion, ApprovalStatus
ORDER BY ClientId, BandVersion;

/* The application DB user must be able to obtain transaction-owned application locks
   (sys.sp_getapplock) and write risk rows and AuditLogs in one transaction. */
