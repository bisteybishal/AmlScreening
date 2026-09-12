-- Run AFTER 01-schema.sql. Same interface as your existing IUserSessionRepository.
-- One active browser session per UserId; 5-minute idle lease; 8-hour absolute lifetime.
CREATE OR ALTER PROCEDURE dbo.spa_UserSession
    @Action VARCHAR(10), @UserId BIGINT, @ClientId INT, @SessionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET @Action=UPPER(LTRIM(RTRIM(@Action)));
    DECLARE @Now DATETIME2(7)=SYSUTCDATETIME(), @Rows INT=0;
    IF @Action IS NULL OR @Action NOT IN ('START','VALIDATE','END')
        OR ISNULL(@UserId,0)<=0 OR ISNULL(@ClientId,0)<=0 OR @SessionId IS NULL
    BEGIN
        SELECT '01' AS Code,'Invalid session request.' AS Message,CAST(0 AS BIT) AS Success;
        RETURN;
    END;

    IF @Action='END'
    BEGIN
        -- Match the actual session: an old browser must not terminate a newer login.
        UPDATE dbo.Users SET ActiveSessionId=NULL,SessionStartedAtUtc=NULL,SessionExpiresAtUtc=NULL
        WHERE UserId=@UserId AND ClientId=@ClientId AND ActiveSessionId=@SessionId;
        SELECT '00' AS Code,'Signed out.' AS Message,CAST(1 AS BIT) AS Success;
        RETURN;
    END;

    IF @Action='START'
    BEGIN
        -- A single conditional UPDATE is atomic even when two browsers log in together.
        UPDATE u SET ActiveSessionId=@SessionId,SessionStartedAtUtc=@Now,
            SessionExpiresAtUtc=DATEADD(MINUTE,5,@Now),LastLoginDate=@Now
        FROM dbo.Users u
        WHERE u.UserId=@UserId AND u.ClientId=@ClientId AND u.IsActive=1 AND u.CanAccessPortal=1
            AND EXISTS(SELECT 1 FROM dbo.Clients c WHERE c.Id=u.ClientId AND c.IsActive=1 AND c.DeleteFg='N')
            AND (ActiveSessionId IS NULL OR SessionExpiresAtUtc IS NULL OR SessionExpiresAtUtc<=@Now
                OR SessionStartedAtUtc IS NULL OR DATEADD(HOUR,8,SessionStartedAtUtc)<=@Now);
        SET @Rows=@@ROWCOUNT;
    END
    ELSE
    BEGIN
        UPDATE u SET SessionExpiresAtUtc=CASE WHEN DATEADD(MINUTE,5,@Now)<DATEADD(HOUR,8,SessionStartedAtUtc)
            THEN DATEADD(MINUTE,5,@Now) ELSE DATEADD(HOUR,8,SessionStartedAtUtc) END
        FROM dbo.Users u
        WHERE u.UserId=@UserId AND u.ClientId=@ClientId AND u.IsActive=1 AND u.CanAccessPortal=1
            AND EXISTS(SELECT 1 FROM dbo.Clients c WHERE c.Id=u.ClientId AND c.IsActive=1 AND c.DeleteFg='N')
            AND ActiveSessionId=@SessionId AND SessionExpiresAtUtc>@Now
            AND DATEADD(HOUR,8,SessionStartedAtUtc)>@Now;
        SET @Rows=@@ROWCOUNT;
    END;
    SELECT CASE WHEN @Rows=1 THEN '00' ELSE '03' END AS Code,
        CASE WHEN @Rows=1 THEN 'Session accepted.' ELSE 'Session expired, already active, or account access is unavailable.' END AS Message,
        CAST(CASE WHEN @Rows=1 THEN 1 ELSE 0 END AS BIT) AS Success;
END;
GO
