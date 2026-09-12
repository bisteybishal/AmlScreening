/* After deployment, use real test-account IDs. Read-only checks, no fixtures inserted.
   Seed A/B private records through the UI/API first, not directly into these tables. */
SET NOCOUNT ON;
DECLARE @UserA BIGINT=0,@UserB BIGINT=0,@SystemAdmin BIGINT=0;
IF @UserA<=0 OR @UserB<=0 OR @UserA=@UserB OR dbo.fn_IsSystemAdmin(@SystemAdmin)<>1
 THROW 51400,'Supply distinct A/B test users and a verified system administrator.',1;
IF NOT EXISTS(SELECT 1 FROM dbo.AmlRecords WHERE IsShared=0 AND OwnerUserId=@UserA AND DeleteFg='N')
 THROW 51401,'Create a private record through User A before testing; empty data is not a passing test.',1;
IF EXISTS(SELECT 1 FROM dbo.fn_VisibleAmlRecordIds(@UserB) v JOIN dbo.AmlRecords a ON a.Id=v.Id
 WHERE a.IsShared=0 AND a.OwnerUserId=@UserA)
 THROW 51402,'FAIL: B can see A private records.',1;
IF EXISTS(SELECT 1 FROM dbo.AmlRecords a WHERE a.OwnerUserId=@UserA AND a.IsShared=0
 AND NOT EXISTS(SELECT 1 FROM dbo.fn_VisibleAmlRecordIds(@UserA) v WHERE v.Id=a.Id))
 THROW 51403,'FAIL: A cannot see their own records.',1;
IF EXISTS(SELECT 1 FROM dbo.AmlRecords a WHERE NOT EXISTS
 (SELECT 1 FROM dbo.fn_VisibleAmlRecordIds(@SystemAdmin) v WHERE v.Id=a.Id))
 THROW 51404,'FAIL: system admin cannot inspect all records.',1;
SELECT N'PASS: AML visibility function checks' AS Result;
SELECT UserId,SourceId FROM dbo.UserAmlSourceAccess WHERE UserId IN(@UserA,@UserB);
SELECT Id,ClientId,OwnerUserId,CustomerReference FROM dbo.Customers WHERE OwnerUserId IN(@UserA,@UserB);
SELECT Id,CustomerId,OwnerUserId,ApiApplicationId,SubmissionChannel,RiskScore,RiskLevel,Decision,CalculatedAtUtc
 FROM dbo.CustomerKycResults WHERE OwnerUserId IN(@UserA,@UserB);
SELECT TOP(20) SourceCode,Status,RecordCount,StartedAtUtc,CompletedAtUtc FROM dbo.AmlSyncRuns ORDER BY Id DESC;

-- Also REQUIRED via HTTP, not simulated by these SELECTs:
-- B requests A customer/detail/history ID -> 404; A -> 200; system admin -> 200.
-- B submits A's customerReference -> separate B customer; A's row remains unchanged.
-- A submits a private blocked name -> potential match; B -> no private match disclosure.
-- No token/wrong token/wrong audience/expired token -> 401; unmapped app -> 403.
-- Revoke application/source/permission -> next request denied or that source unavailable.
-- Force result-save failure on a disposable TEST DB -> customer update rolled back too.
-- Sync a reference equal to a private reference -> private row/owner/sharing unchanged.
-- Run the migration twice on a restored TEST DB; second run makes no duplicate objects.
-- Never run deliberate failure injection against the live database.
-- A starts with CanAccessPortal=0, linked active API app: API permitted, portal sign-in denied.
-- Enable A portal access through AccountAccess; same UserId sees earlier API history.
-- Disable A portal access: existing portal session invalid, API mapping still works.
-- Disable A API application: new API calls denied, portal flag unchanged.
-- Attempt to map A's issuer/subject to B: rejected, mapping ownership unchanged.
-- Log out then sign in immediately: succeeds; second concurrent browser is denied.
