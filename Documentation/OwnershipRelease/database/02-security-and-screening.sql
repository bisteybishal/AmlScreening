/* Run after 01-schema.sql in the maintenance window. No grants are implicit. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER FUNCTION dbo.fn_HasPermission(@UserId BIGINT, @PermissionName NVARCHAR(100))
RETURNS BIT
AS
BEGIN
 DECLARE @Granted BIT=0;
 IF EXISTS(SELECT 1 FROM dbo.Users WHERE UserId=@UserId AND IsActive=1)
 AND EXISTS
 (
   SELECT 1 FROM dbo.Permissions p WHERE p.PermissionName=@PermissionName AND p.IsActive=1
   AND NOT EXISTS(SELECT 1 FROM dbo.UserPermissions up WHERE up.UserId=@UserId AND up.PermissionId=p.PermissionId AND up.IsGranted=0)
   AND (EXISTS(SELECT 1 FROM dbo.UserPermissions up WHERE up.UserId=@UserId AND up.PermissionId=p.PermissionId AND up.IsGranted=1)
     OR EXISTS(SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId=ur.RoleId AND r.IsActive=1
       JOIN dbo.RolePermissions rp ON rp.RoleId=r.RoleId WHERE ur.UserId=@UserId AND rp.PermissionId=p.PermissionId))
 ) SET @Granted=1;
 RETURN @Granted;
END;
GO
CREATE OR ALTER FUNCTION dbo.fn_IsSystemAdmin(@UserId BIGINT)
RETURNS BIT
AS
BEGIN
 RETURN CASE WHEN dbo.fn_HasPermission(@UserId,N'AMLRecords.ViewAll')=1
 AND EXISTS(SELECT 1 FROM dbo.UserRoles ur JOIN dbo.Roles r ON r.RoleId=ur.RoleId
   WHERE ur.UserId=@UserId AND r.IsActive=1 AND r.RoleName=N'Admin') THEN 1 ELSE 0 END;
END;
GO
CREATE OR ALTER FUNCTION dbo.fn_VisibleAmlRecordIds(@UserId BIGINT)
RETURNS TABLE
AS RETURN
 SELECT a.Id
 FROM dbo.AmlRecords a
 WHERE EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Clients c ON c.Id=u.ClientId
   WHERE u.UserId=@UserId AND u.IsActive=1 AND c.IsActive=1 AND c.DeleteFg='N'
   AND (dbo.fn_IsSystemAdmin(@UserId)=1
     OR (a.IsShared=0 AND a.OwnerUserId=@UserId AND a.ClientId=u.ClientId)
     OR (a.IsShared=1 AND EXISTS(SELECT 1 FROM dbo.UserAmlSourceAccess g
         JOIN dbo.AmlSources s ON s.Id=g.SourceId AND s.IsActive=1
         WHERE g.UserId=@UserId AND g.SourceId=a.SourceId))));
GO
CREATE OR ALTER PROCEDURE dbo.spa_CustomerScreening_Check
 @FullName NVARCHAR(200), @ClientId INT, @UserId BIGINT
AS
BEGIN
 SET NOCOUNT ON;
 SET @FullName=LTRIM(RTRIM(@FullName));
 IF NULLIF(@FullName,N'') IS NULL THROW 51201,'Customer name is required.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Users u JOIN dbo.Clients c ON c.Id=u.ClientId
   WHERE u.UserId=@UserId AND u.ClientId=@ClientId AND u.IsActive=1 AND c.IsActive=1 AND c.DeleteFg='N')
   THROW 51202,'Authenticated owner is unavailable.',1;
 IF dbo.fn_HasPermission(@UserId,N'Screening.View')<>1 THROW 51203,'Screening is not permitted.',1;
 -- A missing or incomplete source grant is not a CLEAR result.
 -- API screening uses explicitly granted sources, even for system-admin accounts.
 IF NOT EXISTS(SELECT 1 FROM dbo.UserAmlSourceAccess g JOIN dbo.AmlSources s ON s.Id=g.SourceId
   WHERE g.UserId=@UserId AND s.IsActive=1)
   THROW 51204,'No screening sources are configured for this owner.',1;
 IF EXISTS(SELECT 1 FROM dbo.UserAmlSourceAccess g JOIN dbo.AmlSources s ON s.Id=g.SourceId
   WHERE g.UserId=@UserId AND (s.IsActive=0 OR NOT EXISTS
    (SELECT 1 FROM dbo.AmlRecords a WHERE a.SourceId=s.Id AND a.IsShared=1 AND a.IsActive=1 AND a.DeleteFg='N')))
   THROW 51205,'An assigned screening source is unavailable or empty.',1;

 SELECT CAST(ISNULL(MAX(CASE WHEN s.SourceCode=N'UN' THEN 1 ELSE 0 END),0) AS BIT) AS IsUnMatch,
   CAST(ISNULL(MAX(CASE WHEN s.SourceCode=N'EU' THEN 1 ELSE 0 END),0) AS BIT) AS IsEuMatch,
   CAST(ISNULL(MAX(CASE WHEN s.SourceCode=N'PEP' THEN 1 ELSE 0 END),0) AS BIT) AS IsPep,
   CAST(ISNULL(MAX(CASE WHEN a.IsShared=0 THEN 1 ELSE 0 END),0) AS BIT) AS IsPrivateMatch,
   CAST(ISNULL(MAX(CASE WHEN a.IsShared=1 AND s.SourceCode NOT IN(N'UN',N'EU',N'PEP') THEN 1 ELSE 0 END),0) AS BIT) AS IsOtherSourceMatch,
   CASE WHEN COUNT(*)>0 THEN 100 ELSE 0 END AS HighestMatchScore
 FROM dbo.AmlRecords a LEFT JOIN dbo.AmlSources s ON s.Id=a.SourceId
 WHERE a.FullName=@FullName AND a.IsActive=1 AND a.DeleteFg='N'
 AND ((a.IsShared=0 AND a.OwnerUserId=@UserId AND a.ClientId=@ClientId)
   OR (a.IsShared=1 AND s.IsActive=1 AND EXISTS
       (SELECT 1 FROM dbo.UserAmlSourceAccess g WHERE g.UserId=@UserId AND g.SourceId=a.SourceId)));
END;
GO
CREATE OR ALTER PROCEDURE dbo.spa_AML_Record_BulkInsert
 @RecordType VARCHAR(50), @FullName NVARCHAR(300), @DateOfBirth DATE=NULL,
 @Country VARCHAR(300)=NULL, @Nationality VARCHAR(300)=NULL,
 @ReferenceNo VARCHAR(300), @SourceName VARCHAR(100), @Remarks NVARCHAR(MAX)=NULL,
 @ClientId INT, @OwnerUserId BIGINT=NULL, @IsShared BIT=1, @CreatedBy NVARCHAR(100)
AS
BEGIN
 SET NOCOUNT ON;
 SET XACT_ABORT ON;
 IF @IsShared<>1 OR NULLIF(LTRIM(RTRIM(@ReferenceNo)),'') IS NULL OR NULLIF(LTRIM(RTRIM(@FullName)),N'') IS NULL
   THROW 51210,'Invalid shared source record.',1;
 IF NOT EXISTS(SELECT 1 FROM dbo.Clients WHERE Id=@ClientId AND IsActive=1 AND DeleteFg='N')
   THROW 51211,'Configure an active storage client for source synchronization.',1;
 DECLARE @SourceId INT=(SELECT Id FROM dbo.AmlSources WHERE SourceName=@SourceName AND IsActive=1);
 IF @SourceId IS NULL THROW 51212,'Unknown or disabled official source.',1;
 BEGIN TRY
  BEGIN TRANSACTION;
  UPDATE dbo.AmlRecords WITH(UPDLOCK,SERIALIZABLE)
  SET RecordType=@RecordType,FullName=LTRIM(RTRIM(@FullName)),DateOfBirth=@DateOfBirth,
      Country=@Country,Nationality=@Nationality,Remarks=@Remarks,IsActive=1,
      UpdatedBy=@CreatedBy,UpdatedDate=SYSUTCDATETIME(),LastSyncDate=SYSUTCDATETIME(),
      SourceUpdatedDate=SYSUTCDATETIME(),DeleteFg='N',DeleteDate=NULL,DeleteBy=NULL
  WHERE IsShared=1 AND SourceId=@SourceId AND ReferenceNo=@ReferenceNo;
  IF @@ROWCOUNT=0
   INSERT dbo.AmlRecords(ClientId,OwnerUserId,SourceId,IsShared,RecordType,FullName,DateOfBirth,Country,
       Nationality,ReferenceNo,SourceName,Remarks,IsActive,CreatedDate,CreatedBy,LastSyncDate,SourceUpdatedDate,DeleteFg)
   VALUES(@ClientId,NULL,@SourceId,1,@RecordType,LTRIM(RTRIM(@FullName)),@DateOfBirth,@Country,
       @Nationality,@ReferenceNo,@SourceName,@Remarks,1,SYSUTCDATETIME(),@CreatedBy,SYSUTCDATETIME(),SYSUTCDATETIME(),'N');
  COMMIT;
 END TRY
 BEGIN CATCH
  IF @@TRANCOUNT>0 ROLLBACK;
  THROW;
 END CATCH;
END;
GO
