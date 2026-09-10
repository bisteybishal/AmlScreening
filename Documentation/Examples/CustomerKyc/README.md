# Customer KYC API — step-by-step reference

## Read this first

This is a SEPARATE, copyable .NET 10 reference project. It does not replace or modify
C:/Projects/ScreeningApi or the portal. No application SQL has been executed.

The OAuth server and protected API share ONE host in this example:
- POST /connect/token — partner credentials -> opaque/reference access token
- POST /api/customer-screening — bearer access token + your customer JSON

The portal remains a separate host. OpenIddict handles credential validation, hashed
client-secret storage, token issuance and token validation. Do not Base64-encode
secrets in the database; Basic encoding is only the token-request transport format.
The partner never supplies your internal Clients.Id in the request body.

This is a working-flow reference, not a production AML certification. Exact-name
matching is not complete KYC/identity verification, alias matching or list coverage.
New partners are REVIEW-only by default.

## Step 1 — inspect the source in this order

1. Program.cs: OAuth, validation, certificates and registration/export commands.
2. Controllers/TokenController.cs: exchange validated client credentials for a token.
3. Controllers/CustomerScreeningController.cs: scope check, trusted partner and errors.
4. Infrastructure/KycRepository.cs: ONE SQL transaction around processing and save.
5. Application/CustomerRiskCalculator.cs: customer factors only; no transaction inputs.
6. Database/01-customer-kyc.sql: new tables, no changes to portal risk tables.
7. Database/02-procedures.sql: existing Customers and risk configuration integration.

The folders are intentionally simple; the reference uses one csproj. To integrate
with your existing clean architecture, move Application models/calculator into
ScreeningApi.Application, repository into ScreeningApi.Infrastructure and
controllers/host setup into ScreeningApi. Replace the old name-only service call
with ProcessAsync rather than retaining both execution paths. Rename namespaces
and update registrations together. Keep AuthDbContext in Infrastructure if desired.
Do not paste the complete reference project over your existing solution.

## Step 2 — prerequisites and configuration

- .NET 10 SDK and SQL Server supporting OPENJSON (database compatibility >=130).
- Existing AML database tables: Clients, Customers, AmlRecords, RiskFactors,
  RiskFactorRules, RiskBands. Review their actual columns against 02-procedures.sql.
- SourceOfFunds and Occupation are limited to 150 chars to match your Customers table.
- Each client needs its own active customer factors: OCCUPATION, CUSTOMER_PEP,
  SOURCE_OF_FUNDS, with weights totaling 100 and approved rules.
- Risk bands must be approved and cover 0–100 without overlaps.
- Shared AML records must be present UNDER THAT CLIENT: ClientId + IsShared=1.
  Private user rows and records belonging to another client are deliberately excluded.
  A global sanctions dataset requires a separate explicit entitlement design.

Work from this reference directory or copy it to a separate review directory.

Configure ConnectionStrings:AMLDatabase using .NET user-secrets locally or your
deployment secret store. Do not commit credentials. Example local Windows-auth
configuration value (change server/database to your real local setup):

Server=localhost;Database=AMLScreening;Integrated Security=True;TrustServerCertificate=True

TrustServerCertificate is LOCAL ONLY; use a trusted SQL certificate in UAT/production.
Set ASPNETCORE_ENVIRONMENT=Development for local testing and OAuth:Issuer to
https://localhost:7443/. Trust the local HTTPS development certificate using
dotnet dev-certs https --trust.

Then:
    dotnet restore
    dotnet build -c Release

## Step 3 — manually create OAuth storage

The runtime never calls EnsureCreated, Migrate or automatic database updates.

Export the OpenIddict schema:
    dotnet run --no-launch-profile -- --export-auth-schema

Save the printed SQL to an SQL file, review it, and run it manually in SSMS against
the intended AML database after taking a backup. It creates OpenIddict tables in
the oauth schema. Export does not execute SQL. Do not rerun create statements over
existing OAuth tables; future upgrades require reviewed incremental scripts.

Apply Database/01-customer-kyc.sql and Database/02-procedures.sql manually.

New storage:
- oauth.OpenIddictApplications: OAuth applications and hashed client secrets.
- oauth.OpenIddictTokens: reference-token state/payload; server certificates protect it.
- Other OpenIddict tables are library-managed protocol infrastructure.
- kyc.PartnerAccess: OAuth application -> internal ClientId and enabled/review-only flags.
- dbo.Customers: one current profile per ClientId + CustomerReference.
- kyc.Assessments: one immutable result per screening, with encrypted input snapshot.
- kyc.AssessmentFactors: rule/weight/input evidence.
- kyc.AssessmentMatches: AML record/source/reference and matching method.

The existing transaction-bound RiskAssessments/RiskAssessmentFactors are NOT written.
RiskModels is NOT used: it combines transaction/customer/screening categories, whereas
this endpoint calculates customer-only risk.

## Step 4 — register partner A

Ensure the intended dbo.Clients row already exists and is active.
Supply these settings SECURELY for a one-time operator command:
    Registration:OAuthClientId = client-a
    Registration:ClientId = <existing internal integer client ID>
    Registration:Secret = <cryptographically random secret, at least 32 characters>

Use environment variable names Registration__OAuthClientId, Registration__ClientId,
Registration__Secret, or a secrets store. Do not place secrets in command arguments
or paste them into chat. Generate them with a password manager/cryptographic RNG.

Run:
    dotnet run --no-launch-profile -- --register-client

This explicitly writes the OAuth application and mapping together. It refuses to
overwrite an existing application. Remove Registration settings after registration.
Deliver the secret securely to the partner. There is no public client-registration
endpoint and no partner secret in your KYC JSON.

Repeat for client-b with a DIFFERENT existing Clients.Id and separate random secret.
The code checks the active mapping on both token issuance and each KYC request.
Revoking PartnerAccess.IsActive blocks existing tokens from executing KYC too.

## Step 5 — start the local server

    dotnet run --no-launch-profile --urls https://localhost:7443

If you use another hostname/port, update OAuth:Issuer as well.
Keep HTTPS enabled. Never disable OpenIddict's HTTPS requirement.

## Step 6 — obtain a token in Postman

POST https://localhost:7443/connect/token

Authorization tab:
    Type: Basic Auth
    Username: client-a
    Password: the partner's unencoded secret

Body -> x-www-form-urlencoded:
    grant_type = client_credentials
    scope = customer.screening

Let Postman create the Basic header. It encodes the client identifier/secret pair.
Do not encode the password separately and do not double-encode it.

Response contains access_token, token_type=Bearer and expires_in.
The access token given to the partner is opaque/reference, not a JWT to decode.
This host validates it locally using OpenIddict's shared store and keys.
There is no separate remote introspection call because both endpoints share a host.

## Step 7 — call Customer KYC

POST https://localhost:7443/api/customer-screening

Headers:
    Authorization: Bearer <access_token>
    Content-Type: application/json
    Idempotency-Key: <new unique value for this screening operation>

Body:
{
  "clientReference": "PARTNER-CUST-10001",
  "customer": {
    "fullName": "Ram Bahadur",
    "dateOfBirth": "1990-01-10",
    "nationality": "NPL",
    "country": "NPL",
    "occupation": "ManuFacture",
    "sourceOfFunds": "Business Income",
    "identificationType": "Citizenship",
    "identificationNumber": "12345678"
  }
}

Interpretation:
- clientReference is the stable partner customer reference, not a request ID.
- Existing client/customer reference updates that client's current profile.
- Each NEW retry key creates a new assessment and retains the full input snapshot.
- Retry with SAME key + same normalized DTO serialization: return previous response
  and same correlation ID, without another customer update or assessment.
- Same key + changed input: 409 Conflict.
- Same reference/key under another internal client is independent.
- Key values are case-sensitive. Requests for the same internal client share retry
  keys even when that client has multiple OAuth applications.
- Inactive customers are not silently reactivated.
- Request cancellation after commit can still mean it was saved: retry the same key.

DOB/country/ID details are stored in the encrypted input snapshot; they are NOT yet
used for document verification or fuzzy identity matching. Identification number
is not copied to an unprotected ordinary Customers column.
Confirmed Customers.IsPep is not overwritten by a potential name match.

## Step 8 — understand the response

The shape matches your existing API response. RiskScore=20 is NOT hardcoded.
A potential name match has HighestMatchScore=100 (exact-name similarity, NOT 100%
identity certainty). isPep means potential PEP-list name match.

Only active APPROVED rules of the internal client are used. An exact factor rule
takes precedence over DEFAULT. Missing/ambiguous rules return nullable score/level,
REVIEW and a saved explanation. All bands are validated before assigning a level.
Missing bands may leave a computed score with null level and REVIEW.

Do not approve a sanctions hit automatically because the customer score is low.
A configured BLOCK remains BLOCK; otherwise any match or review-only flag forces REVIEW.
No matches produces screeningStatus=CLEAR for this exact-name check only. It is not
an assertion that every relevant sanctions/PEP source is available or up to date.

New partners have AllowClearDecisions=0. Keep it zero until screening quality,
list completeness/freshness and decision policy have been reviewed. Merely toggling
it does not make this exact-name prototype production-ready.

## Step 9 — inspect the saved result

Run in SSMS, replacing the client and correlation ID:
    DECLARE @ClientId int = 1;
    DECLARE @CorrelationId varchar(32) = 'returned-correlation-id';
    SELECT a.* FROM kyc.Assessments a
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT c.* FROM dbo.Customers c
    JOIN kyc.Assessments a ON a.CustomerId=c.Id AND a.ClientId=c.ClientId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT f.* FROM kyc.AssessmentFactors f JOIN kyc.Assessments a ON a.Id=f.AssessmentId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT m.* FROM kyc.AssessmentMatches m JOIN kyc.Assessments a ON a.Id=m.AssessmentId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;

The authoritative calculated values are nullable fields in kyc.Assessments.
Your legacy Customers.RiskScore is NOT NULL: this compatibility reference uses
RiskLevel=NOT_CALCULATED and numeric zero as a STORAGE PLACEHOLDER when unavailable.
The portal must display NOT_CALCULATED as no score (a dash), not 0/LOW.
Before production, migrate Customers.RiskScore to nullable and update the portal
entity/view/query code together if you want to eliminate this compatibility field.
The response NEVER substitutes that placeholder for a missing calculated score.

## Step 10 — UAT/production requirements (not optional)

- Use separate credentials/databases/OAuth applications per environment.
- Configure real HTTPS issuer and AllowedHosts. Do not expose Development settings.
- Supply separate valid encryption/signing PFX certificates with private keys via:
  Certificates:Encryption:Path/Password and Certificates:Signing:Path/Password.
  Keep secrets outside source. Plan certificate rotation while retaining old keys
  until existing tokens and encrypted snapshots can be processed.
- Set DataProtection:Directory to persistent, access-restricted storage, backed up
  with its certificate. Losing the keys loses encrypted KYC history.
- Deployment account can apply SQL; runtime account should have EXECUTE only on kyc
  procedures plus the required OpenIddict store permissions. No DDL permission.
- Token rate limiter is global to this process; API limiter is per OAuth application.
  Distributed deployments need coordinated throttling and brute-force monitoring.
- Implement actual partner subscriptions/expiry/allowed-source entitlements.
  PartnerAccess.IsActive is only an enabled flag, not a subscription system.
- Implement list version/freshness checks, aliases, identity disambiguation and
  approved PEP treatment. The supplied match evidence is exact-name only.
- Restrict/report audit access, establish KYC retention/deletion and backup policy.
  Snapshots are protected but factor inputs and response data still need DB access controls.
- Forwarded headers are intentionally not trusted. If behind IIS/reverse proxy,
  configure only known proxies/networks and verify the original HTTPS scheme.
- Per-client SQL locking trades throughput for simple correctness. Load-test it.
- Add a portal customer-assessment history screen scoped by ClientId. Existing
  transaction screens do not automatically display these new assessment tables.

## Tests to perform

Verification performed for this reference:
- Release build: 0 errors, 0 warnings.
- OAuth schema export: succeeded without a database connection.
- 13 calculator/SQL checks passed, using only a random temporary tempdb schema.
  Test objects were removed afterward; the AML database was untouched.
- Token issuance and protected KYC HTTP calls have NOT been exercised end-to-end.
  Register test clients and complete the Postman checks before adopting this code.
- Existing portal/API projects were not modified or deployed.

Wrong secret; missing/expired token; wrong scope; disabled mapping; cross-client
customer reference; separate risk configurations; approved vs pending rule; default
fallback; incomplete rules/bands; simultaneous retries; changed-input retry conflict;
new assessment for rescreen; rollback on save error; historical input preservation.

## Official references

- https://documentation.openiddict.com/guides/getting-started/creating-your-own-server-instance
- https://documentation.openiddict.com/guides/getting-started/implementing-token-validation-in-your-apis
- https://documentation.openiddict.com/configuration/token-storage
