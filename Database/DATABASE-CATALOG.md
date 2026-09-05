# AML Screening database catalog

This document is the long-term database reference for the AML Screening portal.
It was compared with the local `AMLScreening` SQL Server database on
2026-09-02. That snapshot contained 19 application tables and 4 stored
procedures. `RiskScoring.sql` adds 5 risk-configuration/evidence tables,
bringing the expected application-table total to 24 after the upgrade.

The SQL Server `.bak` file is the authoritative backup of schema and data. The
SQL files in this folder are the version-controlled description of how the
application expects that database to behave. Keep both.

## Data ownership rules

- `ClientId` separates one client's data from another client's data.
- `OwnerUserId` applies to AML records that belong to a particular portal user.
- `IsShared = 1` makes administrator/imported watchlist data visible to ordinary users in the same client.
- A user with `AMLRecords.ViewClient` can view records for the user's client; it does not grant edit or delete ownership.
- A platform user with `AMLRecords.ViewAll` can cross client boundaries.
- `DeleteFg = 'Y'` means a row is soft deleted. The row remains available for
  audit and recovery.
- `RowVersion` is SQL Server concurrency data. It prevents one user from
  silently overwriting another user's changes.
- Application dates are normally stored in UTC and converted for display.

## Table catalog

### Client, login and authorization tables

| Table | Main key | Purpose | Important relationships |
|---|---|---|---|
| `dbo.Clients` | `Id` | Defines each organization/tenant using the portal. | Parent of users and all client-owned AML/compliance data. |
| `dbo.Users` | `UserId` | Login identity, password hash, name, active status and last login. | `ClientId -> Clients.Id`; linked to roles and permission overrides. |
| `dbo.Roles` | `RoleId` | Role names such as Admin, AML User and Viewer. | Linked to users and permissions. |
| `dbo.UserRoles` | `(UserId, RoleId)` | Assigns one or more roles to a user. | Bridge between `Users` and `Roles`. |
| `dbo.Permissions` | `PermissionId` | Central list of permission codes used by controller policies and menus. | Parent of role and user permission assignments. |
| `dbo.RolePermissions` | `(RoleId, PermissionId)` | Grants permissions to a role. | Bridge between `Roles` and `Permissions`. |
| `dbo.UserPermissions` | `(UserId, PermissionId)` | Per-user grant or deny override using `IsGranted`. | Overrides the effective role permission for one user. |
| `dbo.MenuItems` | `MenuId` | Database-driven portal menu, route, icon, order and required permission. | `ParentMenuId -> MenuItems.MenuId` creates submenus. |
| `dbo.AuditLogs` | `AuditLogId` | Records login, logout and business actions with user, entity and details. | Optional `ClientId -> Clients.Id`. Do not routinely delete this table. |

### AML data and subscription tables

| Table | Main key | Purpose | Important relationships |
|---|---|---|---|
| `dbo.AmlRecords` | `Id` | PEP, UN, EU and other AML/watchlist records used for screening. | `ClientId -> Clients.Id`; `OwnerUserId -> Users.UserId`; referenced by screening matches. |
| `dbo.AML_Subscription_Plan` | `PlanId` | Defines available source subscriptions, duration and price. | Currently a portal administration table and is not client-owned. |

Important AML record identifiers are `SourceName`, `ReferenceNo`, `ClientId`
and `OwnerUserId`. The intended uniqueness rule is
`(ClientId, SourceName, ReferenceNo)`.

### Compliance workflow tables

| Table | Main key | Purpose | Important relationships |
|---|---|---|---|
| `dbo.ComplianceRules` | `Id` | Client-specific amount and velocity rules, risk scores, actions and effective dates. | `ClientId -> Clients.Id`. |
| `dbo.Customers` | `Id` | Client KYC/customer profile and calculated customer risk. | `ClientId -> Clients.Id`; parent of transactions. |
| `dbo.AmlTransactions` | `Id` | Transaction submitted for screening and its final status, score and decision. | `ClientId -> Clients.Id`; `CustomerId -> Customers.Id`. |
| `dbo.ScreeningMatches` | `Id` | Evidence of sender/receiver matches against AML records. | Links transaction, AML record and client. |
| `dbo.RiskAssessments` | `Id` | Customer, transaction, screening and overall risk components. | One active assessment per transaction. |
| `dbo.RiskFactors` | `Id` | Client-specific scoring factors such as PEP, amount, country, occupation, purpose, receiver count and relationship. | `ClientId -> Clients.Id`; parent of factor rules. |
| `dbo.RiskFactorRules` | `Id` | Score assigned to each factor value or numeric range, including version and approval data. | Links to `RiskFactors` and `Clients`. |
| `dbo.RiskBands` | `Id` | Maps the final 0–100 score to LOW, MEDIUM, HIGH or CRITICAL and an ALLOW/REVIEW/BLOCK recommendation. | `ClientId -> Clients.Id`. |
| `dbo.RiskModels` | `Id` | Versioned weights for customer, transaction and screening scores. | `ClientId -> Clients.Id`. |
| `dbo.RiskAssessmentFactors` | `Id` | Immutable factor-level evidence explaining how an assessment was calculated. | `RiskAssessmentId -> RiskAssessments.Id`; `ClientId -> Clients.Id`. |
| `dbo.AmlDecisions` | `Id` | Persisted `ALLOW`, `REVIEW` or `BLOCK` result and its reasons/versions. | Links transaction and risk assessment; correlation ID is unique per client. |
| `dbo.ComplianceAlerts` | `Id` | Review queue item generated for REVIEW or BLOCK decisions. | Links transaction, risk assessment, client and assigned user. |
| `dbo.ComplianceCases` | `Id` | Investigation case, notes and final disposition for an alert. | Links alert, transaction, client and assigned user. |

## Main compliance relationship

```text
Clients
  -> Customers
      -> AmlTransactions
          -> ScreeningMatches -> AmlRecords
          -> RiskAssessments
              -> RiskAssessmentFactors
              -> AmlDecisions
              -> ComplianceAlerts
                  -> ComplianceCases
```

## Stored-procedure catalog

The current definitions are preserved in
`StoredProcedures.CurrentSnapshot.sql`.

| Stored procedure | Used by current code? | Purpose |
|---|---|---|
| `dbo.spa_AML_Record_BulkInsert` | Yes | Called by `AmlImportRepository`. Inserts a new UN/EU record or updates an existing source/reference record during synchronization. Despite its name, the repository invokes it once for every row; it is not a true table-valued bulk insert. |
| `dbo.spa_AML_SubscriptionPlan_GetAll` | No | Legacy procedure returning active subscription plans. The current `SubscriptionRepository` uses inline parameterized SQL. |
| `dbo.usp_User_GetForLogin` | No | Legacy login lookup joining users, user roles and roles. The current `UserRepository` performs its own parameterized query and also loads permissions. |
| `dbo.usp_User_UpdateLastLogin` | No | Legacy update of `Users.LastLoginDate`. The current repository performs an inline UTC update. |

### Stored-procedure warning

The snapshot version of `spa_AML_Record_BulkInsert` checks an existing record
without `ClientId` and is unsafe for UAT. `UAT-Stabilization.sql` replaces it
with a client-scoped upsert and marks imported sanctions records as shared.

## Differences between the local database and checked-in scripts

1. The four stored procedures exist in the local database, but their definitions
   were not previously present in the `Database` folder.
2. Base creation scripts for `Users`, `Roles`, `UserRoles` and
   `AML_Subscription_Plan` are not currently checked in.
3. `AmlRecords` has both legacy `DeletedDate` and current `DeleteDate` columns.
   The application uses `DeleteDate`; `DeletedDate` should be retired only after
   verifying that no external process uses it.
4. Local `AmlRecords.CreatedBy`, `UpdatedBy` and `DeleteBy` are `varchar(50)`,
   while newer scripts expect Unicode/a larger length.
5. Local `AmlRecords` includes `LastSyncDate` and `SourceUpdatedDate`, but the
   base table script does not create them.
6. `AmlSource.View` and an AML Sources menu row are defined by application intent
   but are not currently inserted by the checked-in role/menu scripts.

Do not delete or rename columns merely to make the lists match. First take a
full backup, identify external dependencies, write an explicit migration, test
the restore, and then deploy the change.

## Long-term backup policy

Keep three different artifacts:

1. **Full `.bak` database backup** — contains schema, data, indexes, constraints
   and stored procedures.
2. **Version-controlled SQL scripts** — explain and reproduce intended schema
   changes and stored-procedure definitions.
3. **Application release/tag** — identifies exactly which code version matches
   the database backup.

Recommended minimum schedule for UAT:

- Full backup before every database deployment.
- Nightly full backup while UAT is active.
- Retain deployment backups for the lifetime of the UAT release.
- Copy backups to storage outside the SQL Server machine.
- Perform a test restore regularly; a backup is not proven until it restores.

For production, use full, differential and transaction-log backups according to
the approved recovery-point objective. Encrypt production backups and restrict
access because user records and AML investigation data are sensitive.

## Manual backup procedure

1. Edit only the database name and backup directory in
   `BackupDatabase.Template.sql`.
2. Ensure the SQL Server service account can write to that directory.
3. Execute the script in SQL Server Management Studio.
4. Confirm that `RESTORE VERIFYONLY` succeeds.
5. Record the application commit/tag, database name, backup filename, operator
   and verification result.
6. Copy the `.bak` file to protected secondary storage.

Never keep the only backup on the same drive as the live database.

## Restore validation checklist

After restoring into a separate test database, verify:

- All 24 expected application tables are present after `RiskScoring.sql`.
- All 4 stored procedures are present.
- Foreign keys and unique indexes are present.
- User login succeeds without changing password hashes.
- Admin and ordinary-user menus match permissions.
- One client cannot read another client's records.
- Soft-deleted records do not appear in normal lists.
- AML source synchronization is tested with non-production data.
- Audit events are written for login and business changes.
- Record counts and recent audit timestamps match the source backup.

Do not test a restore by overwriting the active UAT or production database.
