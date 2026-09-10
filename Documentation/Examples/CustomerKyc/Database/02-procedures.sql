SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE kyc.GetPartner @OAuthClientId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.ClientId,p.AllowClearDecisions
    FROM kyc.PartnerAccess p JOIN dbo.Clients c ON c.Id=p.ClientId
    WHERE p.OAuthClientId=@OAuthClientId AND p.IsActive=1 AND c.IsActive=1;
END;
GO
CREATE OR ALTER PROCEDURE kyc.LockClient @ClientId INT,@OAuthClientId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    IF @@TRANCOUNT=0 THROW 51000,'A transaction is required.',1;
    DECLARE @result INT,@resource NVARCHAR(255)=CONCAT('KycClient-',@ClientId);
    EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',
        @LockOwner='Transaction',@LockTimeout=10000;
    IF @result<0 THROW 51000,'Client workflow busy.',1;
    SET @resource=CONCAT('AML-RiskConfiguration-',@ClientId);
    EXEC @result=sys.sp_getapplock @Resource=@resource,@LockMode='Shared',
        @LockOwner='Transaction',@LockTimeout=10000;
    IF @result<0 THROW 51000,'Risk configuration busy.',1;
    IF NOT EXISTS(SELECT 1 FROM kyc.PartnerAccess p WITH(HOLDLOCK)
        JOIN dbo.Clients c WITH(HOLDLOCK) ON c.Id=p.ClientId
        WHERE p.OAuthClientId=@OAuthClientId AND p.ClientId=@ClientId AND p.IsActive=1 AND c.IsActive=1)
        THROW 51001,'Partner access denied.',1;
    SELECT AllowClearDecisions FROM kyc.PartnerAccess
    WHERE OAuthClientId=@OAuthClientId AND ClientId=@ClientId;
END;
GO
CREATE OR ALTER PROCEDURE kyc.GetRetry @ClientId INT,@IdempotencyKey NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT RequestHash,ResponseJson FROM kyc.Assessments
    WHERE ClientId=@ClientId AND IdempotencyKey=@IdempotencyKey;
END;
GO
CREATE OR ALTER PROCEDURE kyc.ScreenName @ClientId INT,@FullName NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    -- Explicit client and sharing boundary. Never read other users' private lists.
    -- Supply official lists under this tenant or design a separate global-list entitlement.
    SELECT Id,SourceName,ReferenceNo
    FROM dbo.AmlRecords
    WHERE ClientId=@ClientId AND IsShared=1 AND IsActive=1 AND DeleteFg='N'
      AND SourceName IN ('UN Sanctions','EU Sanctions','PEP','PEP Local','PEP International')
      AND UPPER(LTRIM(RTRIM(FullName)))=UPPER(LTRIM(RTRIM(@FullName)));
END;
GO
CREATE OR ALTER PROCEDURE kyc.ReadRisk @ClientId INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Now DATETIME2(7)=SYSUTCDATETIME();
    SELECT Id,FactorCode,WeightPercent FROM dbo.RiskFactors
    WHERE ClientId=@ClientId AND ScoreCategory='CUSTOMER' AND IsActive=1 AND DeleteFg='N';
    -- Individual approval: DO NOT filter by max RuleVersion across a category.
    SELECT r.Id,r.RiskFactorId,r.ValueCode,r.Score,r.RuleVersion
    FROM dbo.RiskFactorRules r JOIN dbo.RiskFactors f
      ON f.Id=r.RiskFactorId AND f.ClientId=r.ClientId
    WHERE r.ClientId=@ClientId AND f.ScoreCategory='CUSTOMER'
      AND f.IsActive=1 AND f.DeleteFg='N'
      AND r.IsActive=1 AND r.DeleteFg='N' AND r.ApprovalStatus='APPROVED'
      AND r.EffectiveFrom<=@Now AND (r.EffectiveTo IS NULL OR r.EffectiveTo>@Now);
    SELECT Id,BandVersion,RiskLevel,MinimumScore,MaximumScore,Decision FROM dbo.RiskBands
    WHERE ClientId=@ClientId AND IsActive=1 AND DeleteFg='N' AND ApprovalStatus='APPROVED';
END;
GO
CREATE OR ALTER PROCEDURE kyc.SaveResult
    @ClientId INT,@OAuthClientId NVARCHAR(100),@IdempotencyKey NVARCHAR(100),@RequestHash BINARY(32),
    @ClientReference NVARCHAR(100),@FullName NVARCHAR(200),@DateOfBirth DATE=NULL,
    @Country NVARCHAR(100),@Nationality NVARCHAR(100),@Occupation NVARCHAR(150),@SourceOfFunds NVARCHAR(150),
    @CorrelationId VARCHAR(32),@RiskScore INT=NULL,@RiskLevel NVARCHAR(20)=NULL,
    @CalculationStatus VARCHAR(20),@Decision VARCHAR(20),@BandId INT=NULL,@BandVersion INT=NULL,
    @ResponseJson NVARCHAR(MAX),@InputSnapshot NVARCHAR(MAX),@FactorJson NVARCHAR(MAX),@MatchJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    IF @@TRANCOUNT=0 THROW 51000,'A transaction is required.',1;
    IF NOT EXISTS(SELECT 1 FROM kyc.PartnerAccess
        WHERE OAuthClientId=@OAuthClientId AND ClientId=@ClientId AND IsActive=1)
        THROW 51001,'Partner access denied.',1;

    DECLARE @CustomerId INT;
    SELECT @CustomerId=Id FROM dbo.Customers WITH(UPDLOCK,HOLDLOCK)
    WHERE ClientId=@ClientId AND CustomerReference=@ClientReference AND DeleteFg='N';
    IF @CustomerId IS NOT NULL AND EXISTS
        (SELECT 1 FROM dbo.Customers WHERE Id=@CustomerId AND ClientId=@ClientId AND IsActive=0)
        THROW 51002,'Customer is inactive; do not reactivate automatically.',1;

    IF @CustomerId IS NULL
    BEGIN
        -- Legacy Customers.RiskScore is NOT NULL: zero is a storage placeholder only
        -- when RiskLevel=NOT_CALCULATED. The nullable assessment is authoritative.
        INSERT dbo.Customers(ClientId,CustomerReference,FullName,DateOfBirth,Country,Nationality,
            Occupation,SourceOfFunds,RiskScore,RiskLevel,CreatedBy)
        VALUES(@ClientId,@ClientReference,@FullName,@DateOfBirth,@Country,@Nationality,
            @Occupation,@SourceOfFunds,COALESCE(@RiskScore,0),COALESCE(@RiskLevel,'NOT_CALCULATED'),'KYC API');
        SET @CustomerId=SCOPE_IDENTITY();
    END
    ELSE
        UPDATE dbo.Customers SET FullName=@FullName,DateOfBirth=@DateOfBirth,Country=@Country,
            Nationality=@Nationality,Occupation=@Occupation,SourceOfFunds=@SourceOfFunds,
            RiskScore=COALESCE(@RiskScore,0),RiskLevel=COALESCE(@RiskLevel,'NOT_CALCULATED'),
            UpdatedBy='KYC API',UpdatedDate=SYSUTCDATETIME()
        WHERE Id=@CustomerId AND ClientId=@ClientId;

    -- Do not overwrite confirmed Customers.IsPep with a potential exact-name match.
    INSERT kyc.Assessments(ClientId,CustomerId,OAuthClientId,ClientReference,IdempotencyKey,RequestHash,
        CorrelationId,RiskScore,RiskLevel,CalculationStatus,Decision,BandId,BandVersion,ResponseJson,InputSnapshot)
    VALUES(@ClientId,@CustomerId,@OAuthClientId,@ClientReference,@IdempotencyKey,@RequestHash,
        @CorrelationId,@RiskScore,@RiskLevel,@CalculationStatus,@Decision,@BandId,@BandVersion,@ResponseJson,@InputSnapshot);
    DECLARE @AssessmentId BIGINT=SCOPE_IDENTITY();
    INSERT kyc.AssessmentFactors(AssessmentId,FactorCode,InputValue,RuleId,RuleVersion,RawScore,WeightPercent,WeightedScore)
    SELECT @AssessmentId,FactorCode,InputValue,RuleId,RuleVersion,RawScore,WeightPercent,WeightedScore
    FROM OPENJSON(@FactorJson) WITH
    (
        FactorCode NVARCHAR(100) '$.factorCode',InputValue NVARCHAR(200) '$.inputValue',
        RuleId INT '$.ruleId',RuleVersion INT '$.ruleVersion',RawScore INT '$.rawScore',
        WeightPercent DECIMAL(9,4) '$.weightPercent',WeightedScore DECIMAL(18,6) '$.weightedScore'
    );
    INSERT kyc.AssessmentMatches(AssessmentId,AmlRecordId,SourceName,ReferenceNo)
    SELECT @AssessmentId,Id,SourceName,ReferenceNo
    FROM OPENJSON(@MatchJson) WITH
    (Id INT '$.id',SourceName NVARCHAR(150) '$.sourceName',ReferenceNo NVARCHAR(200) '$.referenceNo');
END;
GO
