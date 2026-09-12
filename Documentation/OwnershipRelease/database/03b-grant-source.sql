-- Grant ONE existing user ONE approved source. Run as deployment DBA after admin review.
SET XACT_ABORT ON;
DECLARE @Reviewed BIT=0,@AdministratorUserId BIGINT=0,@UserId BIGINT=0,@SourceCode NVARCHAR(20)=N'UN';
IF @Reviewed<>1 OR dbo.fn_IsSystemAdmin(@AdministratorUserId)<>1
 THROW 51310,'Supply the acting system administrator and confirm review.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE UserId=@UserId AND IsActive=1)
 THROW 51311,'Choose the existing owner account.',1;
DECLARE @SourceId INT=(SELECT Id FROM dbo.AmlSources WHERE SourceCode=@SourceCode AND IsActive=1);
IF @SourceId IS NULL THROW 51312,'Choose an active source.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF NOT EXISTS(SELECT 1 FROM dbo.UserAmlSourceAccess WHERE UserId=@UserId AND SourceId=@SourceId)
  INSERT dbo.UserAmlSourceAccess(UserId,SourceId) VALUES(@UserId,@SourceId);
 INSERT dbo.OwnershipConfigurationAudit(ChangedByUserId,ChangeType,TargetUserId,Details)
 VALUES(@AdministratorUserId,N'Grant source',@UserId,CONCAT(N'Granted ',@SourceCode));
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
