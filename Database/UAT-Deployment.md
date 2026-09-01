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
2. `ClientIsolation.sql`
3. `AuditLogs.sql`
4. `RolesPermissions.sql`
5. `PlatformHardening.sql`

Apply a script with the checked-in migrator (the connection string is read from an environment variable and is never printed):

```powershell
$env:AML_MIGRATION_CONNECTION = '<UAT connection string from the secret store>'
dotnet run --project AmlScreening.DatabaseMigrator -- Database/PlatformHardening.sql
```

`ClientIsolation.sql` assigns existing rows to a default migration client. Review that client and the migrated ownership assignments before granting UAT access.

## Application deployment

1. Publish and deploy the portal with `ASPNETCORE_ENVIRONMENT=UAT`.
2. Restart the application after the database scripts have completed.
3. Have all users sign out and sign in again so their authentication cookie contains the new `ClientId` claim.

## UAT verification

Use two clients and at least two ordinary users in one client:

- An ordinary user sees only AML records that they own.
- A user with `AMLRecords.ViewClient` sees all AML records for their own client, but none from another client.
- Only a platform administrator with `AMLRecords.ViewAll` can cross client boundaries or manage global role definitions.
- Create, edit, and delete actions are denied when the corresponding permission is absent.
- Client-facing forms do not accept a client identifier.
- AML, compliance, subscription, user-access, login, and logout activity appears in Audit Logs.

Set `AML_SQLSERVER_TEST_CONNECTION` to the dedicated UAT test database, then run `dotnet test AmlScreening.IntegrationTests/AmlScreening.IntegrationTests.csproj` as part of the deployment pipeline. SQL tests create uniquely named rows and clean them up.
