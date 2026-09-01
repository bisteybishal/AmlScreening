# AML Portal Workflow Prototype

This portal implementation covers the synchronous prototype flow:

1. Create a customer/KYC profile.
2. Create an AML transaction.
3. Normalize sender and receiver names.
4. Compare both parties with active AML records for the current client.
5. Calculate customer, transaction, screening and overall risk scores.
6. Evaluate active AmountThreshold and Velocity compliance rules.
7. Persist ALLOW, REVIEW or BLOCK with reasons and version information.
8. Automatically create an alert and case for REVIEW or BLOCK.
9. Let investigators update alerts and disposition cases in the portal.
10. Write audit events for every mutation and evaluation.

## Database setup

Back up the AMLScreening database, then run the scripts in this order:

1. `ClientIsolation.sql`
2. `AuditLogs.sql`
3. `RolesPermissions.sql`
4. `MenuItems.sql`
5. `PortalAmlWorkflow.sql`

`PortalAmlWorkflow.sql` is non-destructive. It adds missing columns and creates
the customer, transaction, screening, risk, decision, alert and case tables.

After applying it, sign out and sign in again so the authentication cookie
contains the newly assigned Admin permissions.

## Practical portal test

1. Add an AML record named `Example PEP Person`.
2. Add a customer named `Example PEP Person` and select PEP.
3. Add an active AmountThreshold rule with threshold `500000`, score `40`, and action `Review`.
4. Create a transaction for that customer with amount `700000 NPR`.
5. Keep `Evaluate immediately` selected.
6. Open the transaction details and verify the four scores, matched record,
   decision reasons, rule version and correlation ID.
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
