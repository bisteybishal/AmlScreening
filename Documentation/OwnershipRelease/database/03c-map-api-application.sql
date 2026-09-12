-- Map a VERIFIED signed OAuth issuer/subject to the SAME account used for portal login.
-- No tokens or secrets are stored by this mapping. Run as deployment DBA after admin review.
SET XACT_ABORT ON;
DECLARE @Reviewed BIT=0,@AdministratorUserId BIGINT=0,@UserId BIGINT=0;
DECLARE @Issuer NVARCHAR(300)=N'',@Subject NVARCHAR(300)=N'';
IF @Reviewed<>1 OR dbo.fn_IsSystemAdmin(@AdministratorUserId)<>1
 THROW 51320,'Supply the acting system administrator and confirm review.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.Users WHERE UserId=@UserId AND IsActive=1)
 THROW 51321,'Choose the existing owner account.',1;
IF @Issuer=N'' OR @Subject=N'' THROW 51322,'Supply real issuer and application subject.',1;
BEGIN TRY
 BEGIN TRANSACTION;
 IF EXISTS(SELECT 1 FROM dbo.ApiApplications WITH(UPDLOCK,HOLDLOCK)
   WHERE Issuer=@Issuer AND Subject=@Subject AND OwnerUserId<>@UserId)
  THROW 51323,'Application belongs to another owner. Do not silently reassign it.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.ApiApplications WHERE Issuer=@Issuer AND Subject=@Subject)
  INSERT dbo.ApiApplications(Issuer,Subject,OwnerUserId,IsActive) VALUES(@Issuer,@Subject,@UserId,1);
 INSERT dbo.OwnershipConfigurationAudit(ChangedByUserId,ChangeType,TargetUserId,Details)
 VALUES(@AdministratorUserId,N'Map API application',@UserId,N'Verified issuer and subject mapping.');
 COMMIT;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK;
 THROW;
END CATCH;
