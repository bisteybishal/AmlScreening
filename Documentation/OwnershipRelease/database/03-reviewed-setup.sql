/* REVIEW legacy shared-source classification only. Grants/mappings use 03b/03c.
   No passwords, tokens, or client secrets here. */
SET XACT_ABORT ON;
SET NOCOUNT ON;
DECLARE @Reviewed BIT=0, @AdministratorUserId BIGINT=0;
IF @Reviewed<>1 OR dbo.fn_IsSystemAdmin(@AdministratorUserId)<>1
 THROW 51300,'Review this script and supply the acting system-admin UserId first.',1;

-- 1. Classify the existing imported shared records. This does not publish private rows.
SELECT a.Id,a.SourceName,a.ReferenceNo,a.OwnerUserId,a.IsShared,s.Id AS ProposedSourceId
FROM dbo.AmlRecords a JOIN dbo.AmlSources s ON s.SourceName=a.SourceName
WHERE a.IsShared=1 AND a.SourceId IS NULL;
-- Review duplicates before executing the UPDATE: the unique index rejects them.
BEGIN TRANSACTION;
UPDATE a SET SourceId=s.Id,OwnerUserId=NULL
FROM dbo.AmlRecords a JOIN dbo.AmlSources s ON s.SourceName=a.SourceName
WHERE a.IsShared=1 AND a.SourceId IS NULL;
INSERT dbo.OwnershipConfigurationAudit(ChangedByUserId,ChangeType,Details)
VALUES(@AdministratorUserId,N'Classify sources',N'Reviewed legacy shared records assigned to official source IDs.');
COMMIT;

-- Existing unknown customer owners are NOT assigned by this script.
