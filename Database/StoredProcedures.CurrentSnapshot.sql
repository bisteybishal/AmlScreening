/*
    Stored procedure snapshot from the local AMLScreening database.
    Captured: 2026-09-02

    This file preserves the current database definitions. Review the ownership
    warning in DATABASE-CATALOG.md before running the AML import procedure in UAT.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.spa_AML_Record_BulkInsert
    @RecordType VARCHAR(50),
    @FullName NVARCHAR(300),
    @DateOfBirth DATE = NULL,
    @Country VARCHAR(300) = NULL,
    @Nationality VARCHAR(300) = NULL,
    @ReferenceNo VARCHAR(300),
    @SourceName VARCHAR(100),
    @Remarks NVARCHAR(MAX) = NULL,
    @ClientId INT,
    @OwnerUserId BIGINT,
    @CreatedBy NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.AmlRecords
        WHERE SourceName = @SourceName
          AND ReferenceNo = @ReferenceNo
    )
    BEGIN
        UPDATE dbo.AmlRecords
        SET RecordType = @RecordType,
            FullName = @FullName,
            DateOfBirth = @DateOfBirth,
            Country = @Country,
            Nationality = @Nationality,
            Remarks = @Remarks,
            IsActive = 1,
            LastSyncDate = SYSUTCDATETIME(),
            ClientId = @ClientId,
            OwnerUserId = @OwnerUserId,
            UpdatedBy = @CreatedBy,
            UpdatedDate = SYSUTCDATETIME()
        WHERE SourceName = @SourceName
          AND ReferenceNo = @ReferenceNo;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.AmlRecords
        (
            RecordType,
            FullName,
            DateOfBirth,
            Country,
            Nationality,
            ReferenceNo,
            SourceName,
            Remarks,
            IsActive,
            CreatedDate,
            LastSyncDate,
            ClientId,
            OwnerUserId,
            CreatedBy,
            SourceUpdatedDate
        )
        VALUES
        (
            @RecordType,
            @FullName,
            @DateOfBirth,
            @Country,
            @Nationality,
            @ReferenceNo,
            @SourceName,
            @Remarks,
            1,
            SYSUTCDATETIME(),
            SYSUTCDATETIME(),
            @ClientId,
            @OwnerUserId,
            @CreatedBy,
            SYSUTCDATETIME()
        );
    END;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spa_AML_SubscriptionPlan_GetAll
AS
BEGIN
    SET NOCOUNT ON;

    SELECT PlanId,
           SourceCode,
           SourceName,
           DurationMonths,
           Rate,
           IsActive
    FROM dbo.AML_Subscription_Plan
    WHERE IsActive = 1
    ORDER BY PlanId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_GetForLogin
    @Username VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT u.UserId,
           u.Username,
           u.FullName,
           u.PasswordHash,
           u.IsActive,
           r.RoleId,
           r.RoleName
    FROM dbo.Users AS u
    INNER JOIN dbo.UserRoles AS ur ON u.UserId = ur.UserId
    INNER JOIN dbo.Roles AS r ON ur.RoleId = r.RoleId
    WHERE u.Username = @Username
      AND u.IsActive = 1
      AND r.IsActive = 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_User_UpdateLastLogin
    @UserId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Users
    SET LastLoginDate = GETDATE()
    WHERE UserId = @UserId;
END;
GO
