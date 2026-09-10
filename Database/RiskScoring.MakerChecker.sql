-- MANUAL upgrade: back up the intended database and run before deploying the code.
-- This script does not approve settings or guess the makers of existing drafts.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;
GO
IF OBJECT_ID(N'dbo.RiskFactorRules', N'U') IS NULL
   OR OBJECT_ID(N'dbo.RiskBands', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Permissions', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Roles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.RolePermissions', N'U') IS NULL
    THROW 51001, 'Risk scoring and role tables must already exist.', 1;
GO
BEGIN TRANSACTION;
IF COL_LENGTH('dbo.RiskFactorRules', 'MakerUserId') IS NULL
    ALTER TABLE dbo.RiskFactorRules ADD MakerUserId BIGINT NULL;
IF COL_LENGTH('dbo.RiskFactorRules', 'ApprovedByUserId') IS NULL
    ALTER TABLE dbo.RiskFactorRules ADD ApprovedByUserId BIGINT NULL;
IF COL_LENGTH('dbo.RiskBands', 'MakerUserId') IS NULL
    ALTER TABLE dbo.RiskBands ADD MakerUserId BIGINT NULL;
IF COL_LENGTH('dbo.RiskBands', 'ApprovedByUserId') IS NULL
    ALTER TABLE dbo.RiskBands ADD ApprovedByUserId BIGINT NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.Permissions WHERE PermissionName = N'RiskScoring.Approve')
    INSERT dbo.Permissions(PermissionName, Description)
    VALUES (N'RiskScoring.Approve', N'Approve risk scores and ranges made by another administrator');

-- Explicitly grants the new permission to the existing Admin role only.
INSERT dbo.RolePermissions(RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId
FROM dbo.Roles r CROSS JOIN dbo.Permissions p
WHERE r.RoleName = N'Admin' AND r.IsActive = 1
  AND p.PermissionName = N'RiskScoring.Approve' AND p.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp
                  WHERE rp.RoleId = r.RoleId AND rp.PermissionId = p.PermissionId);
COMMIT TRANSACTION;
GO
-- Review these sets manually. NULL maker IDs intentionally block edits/approval.
-- Do not backfill from the last editor alone: earlier contributors may exist.
SELECT ClientId, RiskFactorId, RuleVersion, MakerUserId, CreatedBy, UpdatedBy, ApprovalStatus
FROM dbo.RiskFactorRules
WHERE ApprovalStatus IN (N'DRAFT', N'PENDING');

SELECT ClientId, BandVersion, MakerUserId, CreatedBy, UpdatedBy, ApprovalStatus
FROM dbo.RiskBands
WHERE ApprovalStatus IN (N'DRAFT', N'PENDING');
GO
