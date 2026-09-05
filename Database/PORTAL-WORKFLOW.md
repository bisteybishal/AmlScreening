# AML Portal Workflow Prototype

The shared application service covers this synchronous prototype flow. The
portal configures and reviews it; the separately hosted API will submit
customers and transactions:

1. Receive a customer/KYC profile from the future partner API.
2. Receive an AML transaction from the future partner API.
3. Normalize sender and receiver names.
4. Compare both parties with active AML records for the current client.
5. Apply the versioned factor scores from `RiskFactorRules` and the weights in
   `RiskModels` to calculate customer, transaction, screening and overall risk.
6. Evaluate active AmountThreshold and Velocity compliance rules.
7. Persist ALLOW, REVIEW or BLOCK with reasons and version information.
8. Automatically create an alert and case for REVIEW or BLOCK.
9. Let portal investigators review factor evidence, update alerts and
   disposition cases.
10. Write audit events for every mutation and evaluation.

## Database setup

Back up the AMLScreening database, then run the scripts in this order:

1. `ClientIsolation.sql`
2. `AuditLogs.sql`
3. `RolesPermissions.sql`
4. `MenuItems.sql`
5. `PortalAmlWorkflow.sql`
6. `RiskScoring.sql`

`PortalAmlWorkflow.sql` is non-destructive. It adds missing columns and creates
the customer, transaction, screening, risk, decision, alert and case tables.

After applying it, sign out and sign in again so the authentication cookie
contains the newly assigned Admin permissions.

## Practical end-to-end test

1. Add an AML record named `Example PEP Person`.
2. Open Risk Scoring and verify the model, factor rules and risk bands.
3. Submit a customer named `Example PEP Person` through the API/test harness and select PEP.
4. Add an active AmountThreshold rule with threshold `500000`, score `40`, and action `Review`.
5. Submit and evaluate a transaction for that customer with amount `700000 NPR`.
6. Open the transaction details in the portal and verify the four scores,
   factor evidence, matched record, decision reasons, rule versions and correlation ID.
7. Open Alerts and update the generated alert.
8. Open Cases, review the transaction evidence, add a disposition and close the case.

## Intentionally deferred to the API project

- OAuth2 client credentials or mutual TLS
- API credentials and subscription enforcement
- `/api/v1/customer/screen`, `/transaction/screen`, and `/aml/evaluate`
- Idempotency-key storage and API request logs
- External UN/EU/PEP list ingestion and list-version approval
- Redis velocity counters
- OpenSearch/Elasticsearch candidate retrieval
- RabbitMQ/Kafka and outbox workers
- Behaviour baselines, structuring and network analysis
- Maker-checker rule approval
- STR/SAR external filing integration
- OpenTelemetry, health checks, HA/DR and load testing

The prototype deliberately remains a modular monolith. Share the Domain,
Application and Infrastructure projects with the future API rather than copying
the evaluation logic into API controllers.
