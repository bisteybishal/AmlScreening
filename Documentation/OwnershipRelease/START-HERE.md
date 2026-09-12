# Portal + API code — 12 September 2026

This update uses your current projects. It fixes account ownership without allocating a new ClientId for each user. Code is supplied for you to apply; your working projects/database have not been changed.

## What is included

- Complete replacement/addition files for portal and API, including controllers, services, interfaces, repositories, entities, views and JavaScript.
- A **Portal / API access** page for the existing account. Portal sign-in is controlled by `Users.CanAccessPortal`; API access by the active application mapping. Roles still supply module permissions.
- `Customers.OwnerUserId`, owner-scoped transactions, saved `CustomerKycResults`, approved risk rules/bands, and consistent API results.
- Shared-source permissions and private user-owned records, including protected photos.
- Session logout release, single-browser sign-in, five-minute idle expiration and eight-hour maximum session. Idle means no authenticated requests; background polling can keep a session active.
- Search for one, two or three complete names; expandable cards; no numbered result pagination.
- Database migrations, configuration examples, file map and repeatable build/unit checks.

## 1. Choose how to use the code

**Updating your current projects:** use the `OwnershipRelease-2026-09-12.zip` update archive. Each file in `portal/` and `api/` has its exact destination in `FILE-MAP.md`. Files not listed stay unchanged.

**Opening complete project copies:** use `PortalAndApi-FullSource-2026-09-12.zip`. Open `portal/AmlScreening/AmlScreening.Portal.csproj` and `api/ScreeningApi/ScreeningApi.csproj` in separate IDE windows. It contains current project code with these updates already merged. It does not contain database backups, uploads, secrets, compiled binaries, or your real appsettings files.

Do not overwrite newer local changes blindly. The 12 September photo fields and photo endpoint are preserved in this update.

## 2. Back up and test separately

Back up both projects and the database. Restore the database to a test instance. Stop apps/scheduled imports while applying the migration there. Confirm the database name before each script. No existing user/customer owner is guessed.

## 3. Run the database scripts in order

1. `database/00-preflight-readonly.sql` — inspect the existing schema.
2. `database/01-schema.sql` — add missing tables/columns/indexes. Existing intended portal roles get `CanAccessPortal=1` once; API-only roles remain disabled for portal until granted.
3. `database/02-security-and-screening.sql` — owner-aware visibility, screening and shared-feed import.
4. `database/02a-user-session.sql` — START / VALIDATE / END, now also checks portal access and active client.
5. `database/03-reviewed-setup.sql` — inspect and explicitly confirm the existing official shared records. Do not classify private entries as shared.
6. `database/03b-grant-source.sql` — grant the approved sources to each account. Enter the actual UserId, administrator ID and source code.
7. Only for older customer data with no owner: `database/05-assign-reviewed-legacy-owner.sql`, after verifying its owner.

The review/setup scripts intentionally stop until you supply real IDs and confirm review. Do not run old seed/reset/procedure scripts afterward. Repeat the migration on the test copy to verify it is safe to rerun.

## 4. Configure and build both projects

You can use **one normal appsettings.json per project**. Separate Development/UAT settings files are not required.

- Portal: merge `settings/portal.appsettings.example.json` with your existing settings, keeping existing provider URLs/other sections.
- API: merge `settings/api.appsettings.example.json` with your existing settings.
- Replace the server/host/identity-provider placeholders. Both apps must target the same upgraded database. Do not commit production credentials.
- Give the portal service account access to the configured data-protection directory. Use a valid SQL TLS certificate and the intended service account.
- Keep source synchronization disabled until its provider configuration has been tested. `StorageClientId=1` is an example, not an automatically assigned user owner.
- Build the portal and API projects. The API is an OAuth-protected resource API, **not a token server**. A real issuer/token endpoint must be configured; a client secret or Base64 header alone is not an access token. The included API uses your existing JWT Bearer token validation; opaque tokens need a different validator.

## 5. Create user A once

In **Users → Add User**, enter the name, username, password and role.

- For API-only initially, leave **Allow portal sign-in** off.
- For portal access now, turn it on.
- Keep the account active.

The server assigns the creating administrator's current ClientId, as in your same-client scenario. There is no organization dropdown for separating users. Changing a role or enabling portal access does not change ClientId or UserId.

## 6. Link A's API application

In **Users → Portal / API access** beside A:

1. Enter the exact verified OAuth issuer (`iss`) and application subject (`sub`).
2. Confirm that you verified this application belongs to A.
3. Click **Link to this account**.
4. Use existing **Manage module permissions** to grant `Customers.Manage`, `Customers.View` and `Screening.View` as required.
5. Grant approved loaded sources through `03b-grant-source.sql`.

This stores `ApiApplications.OwnerUserId = A.UserId`. It neither issues a token nor stores the application's password. The manual equivalent is `03c-map-api-application.sql`. Do not use both to create duplicate mappings.

## 7. Call the API

`POST /api/customer-kyc` with `Authorization: Bearer <real access token>`:

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

The response includes your `clientReference`, `screeningStatus`, `isUnMatch`, `isEuMatch`, `isPep`, `highestMatchScore`, `customerRiskScore`, `riskLevel`, `decision`, `reasons` and `correlationId`, plus `customerId`, `resultId` and `calculatedAtUtc` for history. Scores come from approved configuration, not a hardcoded 20.

The same transaction saves/updates A's customer, screens authorized records, calculates risk, saves history and commits. If history/calculation fails, it does not commit the customer change. Each successful call creates a new history row; idempotent retry keys are not implemented.

## 8. Give A portal access later

1. Find the **same A account** in Users.
2. Open **Portal / API access**.
3. Turn on **Allow this account to sign in to the portal** and save.
4. Grant the necessary portal module permissions on that account.
5. A signs in with that account's username/password and opens `/CustomerKycHistory/Index`.

No second account, ClientId change, role replacement, customer transfer or API relinking is needed. API access continues through the same mapping. User B cannot see A's records. An authorized system administrator can inspect all records.

Add the KYC history menu through your existing menu setup: controller `CustomerKycHistory`, action `Index`, permission `Customers.View`.

## 9. Verify before switching traffic

Use `database/04-verify-readonly.sql` and its HTTP checklist with distinct A, B and administrator accounts. Test A's API submission followed by A's portal view, B's direct-ID rejection, same-reference A/B isolation, source grants, private photos, logout/relogin, concurrent browser login, access revocation and history-save rollback on the test database.

`VERIFICATION.md` records the checks performed here and the integration tests still required. Keep approved risk rules and maker-checker logic; this update does not change the business scoring formula. Existing UN/EU providers are retained; PEP/CIB providers and fuzzy matching are not newly implemented.
