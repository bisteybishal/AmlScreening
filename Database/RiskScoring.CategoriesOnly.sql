/*
 One-time MANUAL local/UAT setup for missing risk tabs.
 Review @ClientId before running in SSMS.
 Adds category definitions only, not country/occupation scores.
 Does not approve a model or change existing bands/categories.
 Weights are UAT templates from RiskScoring.sql, not approved production policy.
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
DECLARE @ClientId int = 1; -- Confirm the intended client.
IF NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE Id=@ClientId AND IsActive=1 AND DeleteFg='N')
    THROW 50320, 'The selected client is missing or inactive.', 1;
IF OBJECT_ID('dbo.RiskFactors','U') IS NULL
    THROW 50321, 'RiskFactors table is missing. Complete schema setup first.', 1;
BEGIN TRANSACTION;
INSERT dbo.RiskFactors
    (ClientId,FactorCode,FactorName,ScoreCategory,ValueType,WeightPercent,DisplayOrder,
     IsActive,CreatedDate,CreatedBy,DeleteFg)
SELECT @ClientId, seed.Code,seed.Name,seed.Category,seed.ValueType,seed.Weight,seed.SortOrder,
       1,SYSUTCDATETIME(),N'Manual UAT category setup','N'
FROM (VALUES
    ('AMOUNT',N'Amount','TRANSACTION','NUMBER',30,10),
    ('RECEIVING_COUNTRY',N'Receiving country','TRANSACTION','TEXT',25,20),
    ('OCCUPATION',N'Occupation','CUSTOMER','TEXT',30,30),
    ('PURPOSE',N'Purpose','TRANSACTION','TEXT',20,40),
    ('RECEIVER_COUNT',N'Number of registered receivers','TRANSACTION','NUMBER',15,50),
    ('RELATIONSHIP',N'Relation','TRANSACTION','TEXT',10,60),
    ('CUSTOMER_PEP',N'PEP status','CUSTOMER','BOOLEAN',50,70),
    ('SOURCE_OF_FUNDS',N'Source of funds','CUSTOMER','TEXT',20,80)
) seed(Code,Name,Category,ValueType,Weight,SortOrder)
WHERE NOT EXISTS
    (SELECT 1 FROM dbo.RiskFactors existing WITH (UPDLOCK,HOLDLOCK)
     WHERE existing.ClientId=@ClientId AND existing.FactorCode=seed.Code AND existing.DeleteFg='N');
COMMIT;
SELECT FactorCode,FactorName,WeightPercent,IsActive FROM dbo.RiskFactors
WHERE ClientId=@ClientId AND DeleteFg='N' ORDER BY DisplayOrder;
PRINT 'Categories ready for manual entry. A valid approved model and complete ranges are still required for calculation.';
