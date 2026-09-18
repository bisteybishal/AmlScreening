# Portal release: partner creation and isolation

## What changed

The updated source preserves the existing project changes and adds the requested isolation, authorization, session, import, screening, compliance-validation and error-logging corrections. This is the portal project, not an OAuth server or separate KYC API.

- ClientId identifies a partner/company/independent client, never a role. Client 1 has no special privilege.
- GLOBAL AML records can be read across clients. CLIENT records stay within their client. Soft-deleted records remain excluded. Only platform-authorized users can manage GLOBAL records.
- New manual records created by a platform administrator remain GLOBAL, matching the existing requested behavior. Ordinary client-created records are CLIENT scoped; server-side identity sets their owner and client.
- Platform access requires `Platform.CrossClientAccess`. Ordinary user creation cannot assign protected Admin/platform roles. Existing platform-administrator accounts are preserved.
- `Portal.Access` and `Api.Access` are independent permissions. A single account can have both. API username and a one-way API secret hash are stored on that same `Users` row; the raw secret is never stored or shown again. API-only accounts cannot sign into the portal.
- New partners are created using **Partners → Create partner**. No SQL insert is needed after deployment.
- A client lookup bug (`COUNT` incorrectly mapped as a client object) is fixed.
- Changed/deactivated accounts and changed permissions/roles invalidate cookies. Session SP enforces one browser, five-minute idle expiry, and eight-hour absolute expiry.
- Global imports no longer overwrite private records or transfer existing global record ownership to the importing administrator.
- Name normalization preserves Unicode combining marks. Workflow screening no longer depends only on an often-missing prefix. A name score is a potential match, not confirmed identity.
- Compliance approval records its audit in the same transaction, rejects the maker, rechecks authorization and row version, and retires only the same client's previous rule version.
- Unhandled exceptions and handled source-sync failures reach the error SP with a three-second logging timeout and a safe fallback. Raw query strings are not copied into these error records. IPv6 is supported. New error timestamps are UTC; older rows may use the previous local-time convention.

## 1. Deploy database scripts before starting the updated portal

Back up the database and deploy to a UAT copy first. Review each script in `Database/Release`. These scripts were tested in a separate synthetic SQL Server database; they have **not** been applied to your existing AMLScreening database.

Run in this order in SSMS, connected to the intended UAT database:

1. `001_PartnerAccess.sql` — permission presets, Partners menu, shared permission-check function.
2. `spa_UserSession.sql`.
3. `spa_AML_Record_BulkInsert.sql`.
4. `spa_ComplianceRules_Approve.sql`.
5. `spa_SOAPError.sql`.
6. `005_AmlScreeningIndex.sql`.

The four SPs require the existing project tables. The first script is a prerequisite, not optional. No DROP/DELETE migration of existing customer, user, AML, or approval records is performed. The index script refuses duplicate GLOBAL source references; investigate duplicates rather than deleting them blindly. Review any custom indexes depending on NamePrefix before its Unicode column conversion.

Permission presets recognize `Admin`, `AML User`, `AMLUser`, `Viewer`, `APIUser`, and `API User`. Review the role names on your deployment. Unknown roles receive no new access automatically. Platform roles already holding `Platform.CrossClientAccess` receive `Clients.Manage`; a platform permission granted only through a user override needs an explicit reviewed `Clients.Manage` grant as well. Do not grant platform access to all users.

After deployment, log out and sign in again. Old cookies lack the newly required channel permission and will be rejected.

## 2. Create a partner and its accounts

1. Sign in with an existing authorized platform administrator.
2. Open **Partners**, then **Create partner**.
3. Enter code `NABIL` and name `Nabil Bank`. The code is unique. The system generates ClientId; you do not choose it.
4. Save. **Add User** opens with Nabil selected.
5. Enter the user's name, unique username, email and password.
6. Select the AML/portal role for portal access, the API role for API access, or both roles for one account that needs both. Save.
7. On **Manage User Access**, review individual permissions. Role selection gives defaults; explicit permission overrides take precedence.
8. For a second employee, use **Partners → Nabil → Add user**. Both employees share Nabil's ClientId but have different UserIds.
9. For an unrelated client such as Bishal, create another partner. If Bishal later has employees, add them to that existing partner.
10. When an API account later needs the portal, edit that existing account's access: add the portal role and enable `Portal.Access` plus its required feature permissions. Keep its UserId, ClientId and API credentials. Do not create a duplicate identity.

Existing users currently sharing ClientId 1 are **not automatically reassigned**. Confirm each user's membership and the ownership of historical records before migrating them. Merely creating new partners does not separate old data that was already stored under the same ClientId.

New partners receive no copied risk configuration, subscription, API credential, or customer records. Configure/approve their own model, factors, rules and bands through the authorized configuration process before permitting evaluations. This change does not introduce platform impersonation or a global client-switching mechanism for configuration pages.

## 3. Hosting configuration

Use the host's environment settings/secrets rather than committing passwords. Outside Development, startup validates:

- `ConnectionStrings__AMLDatabase`: intended UAT/production SQL connection.
- `AllowedHosts`: actual portal hostname, not `*` or a placeholder.
- `DataProtection__KeysPath`: absolute, persistent directory writable only by the app identity and authorized operators.

Set `ASPNETCORE_ENVIRONMENT=UAT` or `Production` on the host. Configure HTTPS and an appropriate certificate. The existing environment-specific JSON files contain placeholders; host settings must override them. A separate JSON file is not mandatory if the host supplies the values. All portal instances must use compatible protected shared keys and the same session database.

Avoid request polling that extends sessions without user activity. Idle expiry currently means no authenticated server request for five minutes, not a browser keyboard/mouse detector.

## 4. Build and automated UAT

From the solution directory:

```powershell
dotnet restore AmlScreening.slnx
dotnet build AmlScreening.slnx -c Release --no-restore
dotnet run --project tests/AmlScreening.Uat -c Release --no-build -- C:\Projects\AmlScreening
```

The runner requires local SQL Server Windows authentication with permission to create a temporary database and an installed localhost HTTPS development certificate. Set `AML_UAT_SERVER` to another explicitly authorized test SQL Server if required. It creates only a uniquely named `AmlScreening_Uat_<guid>` database, runs synthetic tests, and removes that database. It never reads the app's production connection string. It starts and stops its own hidden localhost portal process. Do not deploy `tests/.../Fixture.sql` to a real database.

See `UAT-RESULTS.md` for the recorded run. The tests exercise actual repositories, SQL procedures, Razor forms and HTTP authentication—not only mocked services.

## 5. Required manual UAT before release

- [ ] Back up/restore a production-shaped UAT database and apply the reviewed scripts there.
- [ ] Confirm existing user-to-partner mapping; approve any historical ownership migration separately.
- [ ] Test two users in Nabil, a user in Bishal, an API-only account, a dual-access account and a platform administrator.
- [ ] Confirm Nabil users see each other's CLIENT records plus GLOBAL records, never Bishal's CLIENT records.
- [ ] Test direct record URLs, edits, deletes, photos, reports and exports—not just hidden menu items.
- [ ] Approve only new/edited/deleted pending items; verify unchanged approved scores never become pending again.
- [ ] Test risk rules/defaults/bands/models with approved expected results for each client.
- [ ] Check compliance maker-checker, stale approvals and concurrent changes with actual business rules.
- [ ] Test real UN/EU source data in UAT and review failures/field-length validation. A source sync does not automatically reverse local inactive/deleted decisions.
- [ ] Validate search results, false positives and false negatives with compliance-approved names, variants and scripts.
- [ ] Confirm session expiry, permission revocation, password handling, login throttling, HTTPS and hosting restart behavior.
- [ ] Force a database logging failure and confirm the safe user message plus fallback application log.
- [ ] Test backup recovery, concurrent users, source-sync load, screening latency, monitoring and alerting.
- [ ] Obtain business/compliance and deployment-owner sign-off.

## Boundaries that must not be mistaken for completed work

- This package stores an API username and one-way secret hash and adds API access permission; it does not implement OAuth2/token issuance, API request authentication middleware, API credential rotation/revocation screens, or the separate Customer KYC API. The API must verify the supplied secret against the hash and resolve the authenticated application to the existing UserId and ClientId server-side.
- Existing legacy customer-profile risk calculation remains outside this requested patch sequence. Do not advertise it as the final approved KYC risk engine; the separate KYC API must use the approved risk services and persist its result atomically.
- The simple workflow candidate scan refuses to evaluate above 10,000 eligible records; it never silently returns clear from a truncated list. Larger-list deployments need a validated candidate-search strategy before enabling this workflow. Portal name lookup shows the first 50 matches with a narrowing notice.
- Existing records without normalized names may have less flexible portal text lookup until reimported/updated. Workflow screening includes eligible records even without a prefix.
- Existing access editing still saves role changes and permission overrides using separate repository transactions. A later failure can leave the earlier role update committed; review access after any error. Full atomic access-editor redesign is not part of this patch.
- Central DB error logging covers unhandled errors and source sync. Other controllers that catch exceptions themselves still use their existing application logger unless explicitly integrated.
- An isolated test pass is not certification of AML compliance, comprehensive business UAT, penetration testing, or production readiness.

## Packaging

The source ZIP contains the updated portal, shared projects, SQL, test runner and release documents. It excludes `.git`, IDE state, build output, temporary files, local appsettings files, private keys/certificates and connection secrets. Safe configuration templates are supplied in the archive; set host values before running. It is a source handoff, not a prebuilt production deployment.
