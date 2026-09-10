# Customer KYC — NO AUTHENTICATION, local Development only

Verification: Release build succeeded. 13 calculator/SQL checks passed in a unique
temporary tempdb schema; all test objects were removed. The initial NuGet audit
feed was unreachable (NU1900); dependency vulnerability checking remains to be
completed. HTTP/Postman end-to-end testing against your local database is still
required. No scripts were applied to your AML database.

This is a separate copyable reference. It does not overwrite your existing
C:/Projects/ScreeningApi project. No application database scripts were executed.

No OAuth, JWT, Basic authentication, Authorization header, partner registration or
client-secret configuration is needed. DO NOT deploy this version to UAT/production.
The process refuses non-Development environments and rejects non-loopback callers.
Never expose it through a public reverse proxy or tunnel.

## 1. Open the project

Open CustomerKycLocal.csproj in Visual Studio. This example is one small API project
with Application, Infrastructure and Controllers folders. Read:
- Application/Models.cs: your nested customer request and response shape.
- Application/CustomerRiskCalculator.cs: pure customer-only risk calculation.
- Infrastructure/KycRepository.cs: stored procedure calls inside one SQL transaction.
- Controllers/CustomerScreeningController.cs: input validation and HTTP response.
- Program.cs: LOCAL safety checks and dependency registration.

To integrate into the existing solution manually, put models/calculator in
ScreeningApi.Application, the repository in ScreeningApi.Infrastructure, and the
controller/host setup in ScreeningApi. Rename namespaces together and replace the
old name-only service call. Do not retain both processing paths or remove your
existing connection settings accidentally. This reference's repository owns both
calculation orchestration and persistence to keep the example small.

## 2. Configure a LOCAL database and test client

Use a local development copy of the AML database, not UAT/production data.

In appsettings.json:
    LocalTesting:ClientId = your existing internal Clients.Id
    LocalTesting:AllowClearDecisions = false

Set ConnectionStrings:AMLDatabase with Visual Studio Manage User Secrets or an
environment variable. A local Windows-auth example value is:
    Server=localhost;Database=AMLScreening;Integrated Security=True;TrustServerCertificate=True

Choose the actual LOCAL server/database. TrustServerCertificate is local only.
No customer JSON/header ClientId is accepted. To test Client 2, change this setting
and restart. This is a local testing convenience, NOT authentication or a mechanism
for serving multiple external clients safely.

## 3. Apply the SQL manually

Back up the local database and inspect your schema first.
Required existing tables:
    dbo.Clients
    dbo.Customers
    dbo.AmlRecords
    dbo.RiskFactors
    dbo.RiskFactorRules
    dbo.RiskBands

Run these two files in SSMS against that LOCAL database:
1. Database/01-tables.sql
2. Database/02-procedures.sql

No migrator runs from application startup.

The new objects use kyc_local, deliberately separate from the authenticated sample's
kyc schema. These scripts must not replace the authenticated kyc.LockClient/SaveResult
procedures. Do not apply both variants without deciding which storage to retain.

Existing dbo.Customers is reused:
- ClientId + CustomerReference identifies the customer, never the name alone.
- A repeated reference updates only that client's profile.
- Each new screening operation saves a separate assessment.
- Inactive customers are not automatically reactivated.

New tables:
    kyc_local.Assessments
    kyc_local.AssessmentFactors
    kyc_local.AssessmentMatches

A result's encrypted InputSnapshot retains the submitted KYC data, including
identification fields absent from your Customers schema. Do not log raw PII.
Protect/back up local Data Protection keys if you need to retain/decrypt that history.

## 4. Check risk configuration

For the configured client:
- Active customer factors must be OCCUPATION, CUSTOMER_PEP and SOURCE_OF_FUNDS.
- Weights must total 100.
- Rule must be active, APPROVED, not deleted and currently effective.
- Individual rule revisions are supported; no category-wide max RuleVersion filter.
- Approved risk bands must cover 0–100 without overlaps/gaps.
- Exact rule first; approved DEFAULT second; otherwise null score and REVIEW.
- MANUFACTURE alone is only one input score, not necessarily the customer's total.

AML matching is exact-name only. It reads this client's IsShared=1, active,
non-deleted UN/EU/PEP records. Other clients and private user lists are excluded.
Your DOB, ID number and documents are NOT verified by this example.
Potential PEP name matches are not written as confirmed Customers.IsPep.

New/unknown risk:
Your legacy Customers.RiskScore is NOT NULL. The compatibility write stores
RiskLevel=NOT_CALCULATED with zero as a storage placeholder when no score exists.
The nullable assessment is authoritative. The API does NOT return that placeholder.
The portal must display NOT_CALCULATED as a dash, not 0/LOW.
For production migrate Customers.RiskScore to nullable and update portal C# and views
together, rather than silently altering its database type alone.

## 5. Build and run

From this directory, in PowerShell:
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    dotnet restore
    dotnet build -c Release
    dotnet dev-certs https --trust
    dotnet run --no-launch-profile --urls https://localhost:7453

Configure your connection string securely before running.
There are no token/authentication endpoints in this version.

## 6. Send the request in Postman

POST https://localhost:7453/api/customer-screening

Authorization: select NO AUTH.

Headers:
    Content-Type: application/json
    Idempotency-Key: local-test-001

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

Use a new Idempotency-Key for each NEW screening. For a retry of the same operation,
reuse the same key and input: the original saved response/correlationId is returned.
Same key with different input returns HTTP 409. ClientReference identifies the
customer; it is not a retry key.

## 7. Understand decisions

AllowClearDecisions=false deliberately forces REVIEW unless the configured decision
is BLOCK. This avoids interpreting an incomplete exact-name prototype as production
clearance. For LOCAL response-format testing only you can set it true. Then approved
bands can return ALLOW when no match exists. This is not validation of list coverage.

Decision precedence:
- Configured BLOCK -> BLOCK.
- Potential name match -> at least REVIEW.
- Missing/invalid configuration -> REVIEW with nullable score/level.
- Review-only mode -> REVIEW.
- Otherwise approved band decision.

No-match status CLEAR means only no exact-name match in the configured shared records.
It does not guarantee sanction clearance or completed KYC.

## 8. Verify persistence

Replace the values with those used/returned by the API:
    DECLARE @ClientId int = 1;
    DECLARE @CorrelationId varchar(32) = 'paste-returned-id';
    SELECT a.* FROM kyc_local.Assessments a
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT c.* FROM dbo.Customers c
    JOIN kyc_local.Assessments a ON a.CustomerId=c.Id AND a.ClientId=c.ClientId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT f.* FROM kyc_local.AssessmentFactors f
    JOIN kyc_local.Assessments a ON a.Id=f.AssessmentId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;
    SELECT m.* FROM kyc_local.AssessmentMatches m
    JOIN kyc_local.Assessments a ON a.Id=m.AssessmentId
    WHERE a.ClientId=@ClientId AND a.CorrelationId=@CorrelationId;

The API returns success only after commit. Customer profile, response, factor evidence
and matches are saved together. A failure rolls the transaction back.
A disconnected caller may miss the success response after commit; retry with the SAME key.

Existing transaction RiskAssessments/RiskAssessmentFactors are not written because
they require transactions. RiskModels is not used for this customer-only score.
The old portal transaction-history page does not automatically display these records.
