# Ownership correction package — start here

Start with [START-HERE.md](START-HERE.md) for the short installation guide. This is the updated manual package dated 12 September 2026. It replaces the earlier organization-dropdown advice for this requirement. It is **not deployed**. The running source projects and database were not modified.

## The fixed rule

1. Official UN/EU/PEP/CIB reference records are shared only with users granted that source.
2. Manually added blocked records are private to their owner, including manual records added by an administrator.
3. API credentials map to the SAME existing UserId used for portal access. A later portal account must not be created under a different identity.
4. Customers, transactions and saved KYC results are private to that owner. A system administrator can inspect all records.
5. System administrator means an active `Admin` role AND effective `AMLRecords.ViewAll`. Relevant module permissions still apply; view-all does not automatically mean edit/delete-all.

ClientId remains an organization boundary. It does not distinguish Portal/API/Admin. No per-user ClientId allocation and no Organization dropdown is needed for this update.

## Why the two database changes are needed

`Customers.OwnerUserId` says **whose customer this is**. The customer key becomes `(ClientId, OwnerUserId, CustomerReference)`. A and B may use the same reference without updating each other's customer.

`CustomerKycResults` says **what happened on each screening**. It stores the score, band, decision, factor breakdown, AML result, original request snapshot, owning user and API application. `Customers` stores the latest customer state; history rows preserve previous assessments after that state changes.

Both are saved in one transaction. If calculation or history saving fails, the customer insert/update is rolled back. No failure is returned as CLEAR.

## 1. Back up and prepare

- Back up the database and both projects. Confirm that a restore works on a separate test instance.
- Confirm BOTH connection strings point to the intended database; this review used local `AMLScreening`.
- Run `database/00-preflight-readonly.sql` there. It reads schema/ownership, not passwords.
- Stop portal, API and any sync job for migration/deployment. Do not run old code against the partly upgraded schema/procedures.
- Never rerun old UAT seed/reset/stored-procedure scripts after this package: they can restore owner-blind behaviour.

## 2. Apply database scripts in this order

First rehearse on a restored TEST database, including running the migration twice.

1. `01-schema.sql`: adds owner fields, source access/API identity mapping, result history and indexes. It replaces the three known owner-blind unique indexes. It does not delete customer records or assign unknown owners.
2. `02-security-and-screening.sql`: creates common visibility/permission functions and replaces the unsafe screening/import procedures.
3. `02a-user-session.sql`: replaces START/VALIDATE/END with active-client and portal-access checks, five-minute idle and eight-hour absolute expiration.
4. `03-reviewed-setup.sql`: review the displayed existing shared-source records, then set `@Reviewed=1` and the actual acting administrator ID to classify them. It does not publish private records. Unknown source names need explicit reconciliation, not a guessed mapping.
5. `03b-grant-source.sql`: set the reviewed existing UserId and source code; grant only the source(s) approved for that user. Repeat per approved source. This also defines an API system-admin account's requested screening coverage; admin inspection itself can see all sources.
6. `03c-map-api-application.sql`: enter the actual validated OAuth `iss` and `sub` values and the EXISTING owner's UserId. No access token/secret belongs in this table.
7. If legacy customers exist, use `05-assign-reviewed-legacy-owner.sql` only after confirming their owner. Null-owner legacy customers/history stay admin-only. Already-owned records cannot be transferred by this script.

Keep the ID placeholders at zero until you have verified each account. There are no hidden grants to every user. The configuration scripts record the reviewing administrator in an audit table.

## 3. Copy the portal replacement files

Every file inside `portal/` maps directly under:

`C:\Projects\AmlScreening\`

Example:

`portal/AmlScreening.Infrastructure/Repositories/AmlRecordRepository.cs`

replaces:

`C:\Projects\AmlScreening\AmlScreening.Infrastructure\Repositories\AmlRecordRepository.cs`

Copy only the files listed in `FILE-MAP.md`. They are complete files, not fragments. Existing files not in the map stay unchanged. Keep your own uncommitted changes backed up; compare/merge if you edited a listed file after this review.

Key changes:

- DbContext + AML authorization: shared database visibility function; owner filters for customers, transactions, alerts, cases, risk-assessment details and KYC history.
- Repository: no normal `ViewClient` bypass; manual records are private; edit/delete still require action permission.
- Audit service and dashboard: owner-safe reads and counts, not all records with ClientId 1.
- Cookie validation: reload active role/permissions on each request so permission revocation is not deferred until next login. `Users.CanAccessPortal` controls portal sign-in independently of role names. Enable it on the existing account when portal access is purchased; existing API mappings remain unchanged.
- KYC history: open `/CustomerKycHistory/Index` or `/CustomerKycHistory/Index?customerId=123`. Add a menu link to controller `CustomerKycHistory`, action `Index`, permission `Customers.View` using your existing menu management. No menu IDs are guessed by this package.
- Screening: one to three complete names, results grouped by name, first 50 matches per name with a refine-search notice, expandable cards, no numbered pagination.

The updated package also replaces `UsersController`, its creation form, `AccountController`, authentication and user repository files. Creation uses the administrator's current ClientId, not a user-supplied organization selection. The form has an explicit portal-access checkbox. `AccountAccessController` and its repository/view manage portal access and verified API mappings for the existing user. Logout releases the database session. `Program.cs` now calls its existing hosted-configuration validation; supply AllowedHosts, database configuration and the data-protection directory.

## 4. Configure source synchronization

Merge this section into your existing portal `appsettings.json`; do not replace connection strings or other settings:

```json
"SourceSync": {
  "Enabled": false,
  "StorageClientId": 1,
  "IntervalMinutes": 60,
  "Sources": ["UN", "EU"]
}
```

`StorageClientId=1` is an example: use your confirmed existing vendor/system storage client. It is not the private data owner. Imported rows have no personal owner and are visible by source grant.

Enable the worker only on ONE always-running deployment instance after a manual source test. IIS idle/recycling can pause a hosted worker; use an always-on worker/service host for a dependable schedule. The worker supports cancellation, bounded retries and transactional imports. Failed imports preserve existing rows. Successful imports are logged in `AmlSyncRuns`; failed fetches/imports are written to application logs. Monitor both last success and errors.

Only the existing UN and EU providers are included. PEP/CIB source entries do not implement those providers. Their real authorized endpoints, credentials, feed formats and update rules are still needed before enabling them. Do not grant a source as screening coverage until its provider has successfully populated it.

This update does NOT automatically remove/delist records absent from a feed. Define and test each provider's deletion/delta rules before adding that behaviour. The current parsers retain their existing field/alias coverage; this package does not certify completeness of the external lists.

## 5. Copy the API replacement files

Every file inside `api/` maps directly under:

`C:\Projects\ScreeningApi\`

Use `FILE-MAP.md`. Files missing in the destination are new files. Existing old screening classes may remain, but the old `/api/customer-screening` route is retired and authenticated; use `/api/customer-kyc`.

The API is a resource server, not an OAuth token issuer. Configure your real HTTPS OAuth/OIDC authority and this API's audience in the existing API settings:

```json
"Authentication": {
  "Authority": "https://YOUR-ACTUAL-IDENTITY-PROVIDER",
  "Audience": "YOUR-REGISTERED-API-AUDIENCE"
}
```

Those are placeholders, not a running provider. The API fails startup for missing/non-HTTPS authority settings. It validates issuer, signature, audience and expiry, then maps the signed issuer/subject to the active database application and owner. It does not trust ClientId/UserId supplied in the body or custom headers. Your provider must issue the expected `iss` and `sub` claims for the registered application. Disabled/unmapped applications are denied.

**There is no authentication bypass and no fabricated token server in this package.** If you do not yet have a real provider/token endpoint, that external dependency must be configured before API deployment.

## 6. Give permissions through your existing administration screen

| Action | Required permission |
|---|---|
| Portal name lookup | `Screening.View` plus source grants for official data |
| View AML records | `AMLRecords.View` |
| Add/edit/delete private AML records | Matching `AMLRecords.Create/Edit/Delete` permission |
| API KYC submission | `Customers.Manage` and `Screening.View`, active API mapping and source grants |
| View customer/KYC history | `Customers.View` |
| Transaction reads | `Transactions.View` |
| Transaction creation/evaluation service | `Transactions.Manage` |
| Audit list | `AuditLogs.View`; ordinary users see their own audit entries |
| System-wide inspection | Active `Admin` + `AMLRecords.ViewAll`, with the relevant module permissions |

Do not give `Roles.Manage` or `AMLRecords.ViewAll` to ordinary Portal/API accounts. Keep the existing maker-checker flow and approved client risk configuration; the ownership update does not redefine risk weights or bands. Transaction evaluation is limited to an owned transaction; admin viewing another user's transaction does not silently evaluate it using the admin's client settings.

## 7. Test the required request and history

POST `/api/customer-kyc` with a real Bearer access token:

```json
{
  "clientReference": "PARTNER-CUST-10001",
  "customer": {
    "fullName": "Ram Bahadur",
    "dateOfBirth": "1990-01-10",
    "nationality": "NPL",
    "country": "NPL",
    "occupation": "Business",
    "sourceOfFunds": "Business Income",
    "identificationType": "Citizenship",
    "identificationNumber": "12345678"
  }
}
```

Response retains your flat `clientReference`, screening flags, highestMatchScore, customerRiskScore, riskLevel, decision, reasons and correlationId, and adds resultId/customerId/calculatedAtUtc for history lookup. The numeric score comes from APPROVED configured rules; it is not hardcoded to 20. Exact-name screening is not fuzzy identity verification. CLEAR means no exact match within the account's configured permitted records, not universal regulatory clearance.

GET `/api/customer-kyc/{resultId}` and portal `/CustomerKycHistory/Details/{id}` use the same owner. A can view A's result; B gets 404; a properly authorized system admin can inspect it.

`CustomerKycResults` preserves a new immutable history row for each successful call; repeating a request creates another assessment. There is no idempotency-key contract yet. If a network timeout occurs after commit, reconcile the history/correlation rather than assuming no write happened.

## 8. Verification and deployment gate

See `VERIFICATION.md` for what was actually tested. `BUILD-VERIFICATION.ps1` creates isolated copies and builds/checks them without starting the apps or accessing the database.

Use `database/04-verify-readonly.sql` with A/B/admin TEST accounts AFTER creating test data through the updated apps. Complete its HTTP tests too. A successful build does not prove database deployment, OAuth configuration, source freshness or live integration.

Deploy the database and both app binaries together in maintenance mode only after the restored-database and HTTP tests pass. If migration/deployment fails, keep maintenance mode on; use the tested backup plus matching binaries to recover. Do not drop owner fields/history to force old code to run, and do not reopen an owner-blind API.

Protect database backups and the saved request snapshots (they contain sensitive KYC data), use least-privilege service database accounts, and never put tokens/passwords in application logs. Perform your normal deployment vulnerability scan with network access; it was unavailable during the isolated build here.
