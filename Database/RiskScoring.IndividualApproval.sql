-- Manual deployment: back up the database and stop portal writes first.
-- Run against the SAME database used by the portal, before deploying the new build.
SET XACT_ABORT ON;
IF OBJECT_ID(N'dbo.RiskFactorRules', N'U') IS NULL
    THROW 51000, 'RiskFactorRules does not exist in this database.', 1;
IF COL_LENGTH('dbo.RiskFactorRules', 'MakerUserId') IS NULL
    THROW 51000, 'Run RiskScoring.MakerChecker.sql first.', 1;
IF COL_LENGTH('dbo.RiskFactorRules', 'ReplacesRuleId') IS NULL
    ALTER TABLE dbo.RiskFactorRules ADD ReplacesRuleId INT NULL;
GO

-- Preview old, unchanged pending copies. Nothing below is changed by default.
-- Select the correct client and inspect the preview before setting @ArchiveCopies = 1.
-- Changed legacy drafts are NOT automatically linked or approved: review them manually.
SET XACT_ABORT ON;
DECLARE @ClientId INT = 1;
DECLARE @ArchiveCopies BIT = 0;
BEGIN TRANSACTION;

SELECT p.Id AS PendingCopyId, a.Id AS ExistingApprovedId, p.RowVersion AS PendingRowVersion,
       p.ClientId, p.RiskFactorId, p.ValueCode, p.ValueName, p.Score
INTO #UnchangedCopies
FROM dbo.RiskFactorRules p WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.RiskFactorRules a WITH (UPDLOCK, HOLDLOCK)
  ON a.ClientId = p.ClientId AND a.RiskFactorId = p.RiskFactorId
WHERE p.ClientId = @ClientId AND p.ApprovalStatus IN ('DRAFT', 'PENDING')
  AND p.ReplacesRuleId IS NULL AND p.DeleteFg = 'N'
  AND a.ApprovalStatus = 'APPROVED' AND a.IsActive = 1 AND a.DeleteFg = 'N'
  AND EXISTS
  (
      SELECT p.ValueCode, p.ValueName, p.MinimumValue, p.MaximumValue, p.Score, p.Remarks
      INTERSECT
      SELECT a.ValueCode, a.ValueName, a.MinimumValue, a.MaximumValue, a.Score, a.Remarks
  );

SELECT * FROM #UnchangedCopies ORDER BY PendingCopyId;
IF @ArchiveCopies = 1
BEGIN
    -- Retain the rows as history; do not delete them or change approved rows.
    UPDATE p SET ApprovalStatus = 'ARCHIVED', IsActive = 0,
                 UpdatedBy = N'Manual duplicate-copy cleanup', UpdatedDate = SYSUTCDATETIME()
    OUTPUT inserted.Id AS ArchivedCopyId, deleted.ApprovalStatus AS PreviousStatus
    FROM dbo.RiskFactorRules p
    WHERE EXISTS (SELECT 1 FROM #UnchangedCopies c
                  WHERE c.PendingCopyId = p.Id AND c.PendingRowVersion = p.RowVersion);
END;
DROP TABLE #UnchangedCopies;
COMMIT;
