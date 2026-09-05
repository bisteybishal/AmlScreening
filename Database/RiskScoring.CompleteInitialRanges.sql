/* Complete the inspected, incomplete initial draft for Client 1.
   Uses the document's example 0–100 ranges; leaves them DRAFT for review.
   Preserves the previous row as inactive history. Does not create/approve a model.
   Refuses to overwrite an unexpected configuration. Safe to rerun after success.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @ClientId int = 1;
DECLARE @Actor nvarchar(100) = LEFT(ORIGINAL_LOGIN(),100);
DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Expected TABLE (RiskLevel nvarchar(20), MinimumScore int, MaximumScore int, Decision nvarchar(20));
INSERT @Expected VALUES ('LOW',0,29,'ALLOW'),('MEDIUM',30,59,'REVIEW'),
                        ('HIGH',60,79,'REVIEW'),('CRITICAL',80,100,'BLOCK');

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    DECLARE @Resource nvarchar(255) = CONCAT('AML-RiskConfiguration-',@ClientId);
    EXEC @LockResult = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive',
         @LockOwner='Transaction', @LockTimeout=10000;
    IF @LockResult < 0 THROW 50330, 'Risk configuration is busy. Please retry.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE Id=@ClientId AND IsActive=1 AND DeleteFg='N')
        THROW 50331, 'Client 1 is missing or inactive.', 1;
    DECLARE @Version int = ISNULL((SELECT MAX(BandVersion) FROM dbo.RiskBands WHERE ClientId=@ClientId),0);

    IF (SELECT COUNT(*) FROM dbo.RiskBands WHERE ClientId=@ClientId AND BandVersion=@Version AND IsActive=1 AND DeleteFg='N') = 4
       AND NOT EXISTS
       (SELECT 1 FROM @Expected e WHERE NOT EXISTS
           (SELECT 1 FROM dbo.RiskBands b WHERE b.ClientId=@ClientId AND b.BandVersion=@Version
            AND b.IsActive=1 AND b.DeleteFg='N' AND b.RiskLevel=e.RiskLevel
            AND b.MinimumScore=e.MinimumScore AND b.MaximumScore=e.MaximumScore AND b.Decision=e.Decision))
    BEGIN
        COMMIT;
        PRINT 'The four document ranges already exist. No changes made.';
        RETURN;
    END;

    IF (SELECT COUNT(*) FROM dbo.RiskBands WHERE ClientId=@ClientId AND BandVersion=@Version) <> 1
       OR NOT EXISTS
       (SELECT 1 FROM dbo.RiskBands WHERE ClientId=@ClientId AND BandVersion=@Version
        AND RiskLevel='MEDIUM' AND MinimumScore=0 AND MaximumScore=10 AND Decision='ALLOW'
        AND ApprovalStatus='DRAFT' AND IsActive=1 AND DeleteFg='N')
        THROW 50332, 'The draft differs from the inspected MEDIUM 0-10 entry. Review it before changing ranges.', 1;

    UPDATE dbo.RiskBands SET IsActive=0,UpdatedBy=@Actor,UpdatedDate=@Now
    WHERE ClientId=@ClientId AND BandVersion=@Version;

    INSERT dbo.RiskBands
        (ClientId,RiskLevel,MinimumScore,MaximumScore,Decision,Remarks,BandVersion,
         ApprovalStatus,IsActive,CreatedDate,CreatedBy,DeleteFg)
    SELECT @ClientId,RiskLevel,MinimumScore,MaximumScore,Decision,
           N'Document default range; pending review.',@Version+1,'DRAFT',1,@Now,@Actor,'N'
    FROM @Expected;

    INSERT dbo.AuditLogs
        (ClientId,UserId,Username,Action,EntityType,EntityId,Details,CreatedDate)
    VALUES (@ClientId,NULL,@Actor,'Configure','RiskBand',CONVERT(nvarchar(100),@ClientId),
        CONCAT('Completed initial risk ranges in draft version ',@Version+1,
        '. Preserved version ',@Version,' MEDIUM 0-10 ALLOW as inactive history. New ranges: LOW 0-29 ALLOW; MEDIUM 30-59 REVIEW; HIGH 60-79 REVIEW; CRITICAL 80-100 BLOCK. No model approved.'),@Now);
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;

SELECT Id,RiskLevel,MinimumScore,MaximumScore,Decision,BandVersion,ApprovalStatus
FROM dbo.RiskBands WHERE ClientId=@ClientId AND IsActive=1 AND DeleteFg='N'
ORDER BY MinimumScore;
