# UAT deployment

## Before deployment

1. Back up the UAT database.
2. Create a dedicated UAT SQL Server database and login. Do not reuse the development or production database.
3. Set the portal environment to `UAT`.
4. Provide the connection string through `ConnectionStrings__AMLDatabase` in the host environment or secret store.
5. Set `AllowedHosts` to the real UAT DNS name. The application refuses wildcard and placeholder host values outside Development.
6. Set `DataProtection__KeysPath` to a fully-qualified persistent directory and grant write access only to the application identity.
7. Send JSON console logs to the UAT host's centralized log collector. Do not store secrets in log configuration.

## Database script order

Run the scripts against the UAT database in this order:

1. Existing base schema scripts, where the database is new.
2. `AmlRecords.SoftDelete.sql`
3. `ClientIsolation.sql`
4. `AuditLogs.sql`
5. `ComplianceRules.sql`
6. `RolesPermissions.sql`
7. `MenuItems.sql`
8. `PortalAmlWorkflow.sql`
9. `RiskScoring.AlignExisting.sql`, then `RiskScoring.sql`, then `RiskScoring.SplitMenus.sql`
10. `PlatformHardening.sql`
11. `UAT-Stabilization.sql`

Have the DBA review and execute each script manually in SQL Server Management
Studio against the UAT database. Record the script name, execution date,
operator and result in the deployment record.

`ClientIsolation.sql` assigns existing rows to a default migration client. Review that client and the migrated ownership assignments before granting UAT access.

`UAT-Stabilization.sql` is the final alignment script for an existing UAT
database. It adds shared AML-record support, removes redundant user permission
overrides, deactivates obsolete permissions, removes duplicate AML Sources menu
rows and replaces the unsafe cross-client AML import procedure. It must be
reviewed and run by the DBA; the portal never runs schema changes on startup.

## Application deployment

1. Publish and deploy the portal with `ASPNETCORE_ENVIRONMENT=UAT`.
2. Restart the application after the database scripts have completed.
3. Have all users sign out and sign in again so their authentication cookie contains the new `ClientId` claim.

## UAT verification

Use two clients and at least two ordinary users in one client:

- An ordinary user sees only AML records that they own.
- An ordinary user also sees shared UN/EU or system-administrator records for their own client, but cannot edit or delete them without wider access.
- A user with `AMLRecords.ViewClient` sees all AML records for their own client, but none from another client.
- Only a platform administrator with `AMLRecords.ViewAll` can cross client boundaries or manage global role definitions.
- Create, edit, and delete actions are denied when the corresponding permission is absent.
- Client-facing forms do not accept a client identifier.
- AML, compliance, subscription, user-access, login, and logout activity appears in Audit Logs.
- Risk Scoring displays the active model, eight factor tabs and four complete, non-overlapping risk bands.
- A completed transaction evaluation stores factor evidence in `RiskAssessmentFactors` and displays it on Transaction Details.

Record the result of each verification item in the UAT deployment checklist.

## Risk CRUD upgrade

Run the read-only `RiskScoring.CrudReadiness.sql` before deploying the CRUD upgrade.
Follow `Documentation/RiskScoring-CRUD-Guide.md` to test both risk menus: add/edit/delete
save to draft, and activation is allowed only for a valid complete factor/band set.
`RiskScoring.Manage` currently permits both editing and activation; independent approval
is not implemented. Existing UAT scores require business approval before live use.
