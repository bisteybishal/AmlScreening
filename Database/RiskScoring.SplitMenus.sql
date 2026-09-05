SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @ParentId int = (SELECT MenuId FROM dbo.MenuItems WHERE MenuKey='Compliance');
IF @ParentId IS NULL THROW 50303, 'Compliance menu is missing.', 1;
UPDATE dbo.MenuItems SET DisplayName=N'Risk Scores', ControllerName='RiskScores', ActionName='Index', SortOrder=35
WHERE MenuKey='RiskScoring';
IF NOT EXISTS (SELECT 1 FROM dbo.MenuItems WHERE MenuKey='RiskScoreSetup')
    INSERT dbo.MenuItems
    (ParentMenuId,MenuKey,DisplayName,SectionName,ControllerName,ActionName,IconClass,PermissionCode,SortOrder,IsActive)
    VALUES (@ParentId,'RiskScoreSetup',N'Risk Score Setup','MANAGEMENT','RiskScoreSetup','Index','bi-sliders','RiskScoring.View',36,1);
ELSE
    UPDATE dbo.MenuItems SET ParentMenuId=@ParentId, DisplayName=N'Risk Score Setup',
    ControllerName='RiskScoreSetup',ActionName='Index',PermissionCode='RiskScoring.View',SortOrder=36,IsActive=1
    WHERE MenuKey='RiskScoreSetup';
COMMIT;
