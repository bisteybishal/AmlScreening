# Risk scoring flow and live-release gaps

For the simplified screens/controller split, see [RiskScores-Simple-Flow.md](RiskScores-Simple-Flow.md).

The portal configures and displays risk. The future partner API authenticates the client, submits KYC/transactions and calls the shared workflow service.

## Calculation

1. Resolve ClientId from trusted authentication.
2. Load the customer's transaction within that client.
3. Screen parties against AML records and evaluate amount/velocity rules.
4. Load effective approved model and factor rules for the client.
5. Match PEP, occupation, funds source, amount, country, purpose, receiver count and relationship.
6. Calculate category scores from factor scores and weights. Each category totals 100%.
7. Transaction risk is the maximum of weighted factor risk and compliance-rule risk.
8. Overall score uses the model's customer/transaction/screening weights.
9. Map score to a band; apply stricter screening/compliance actions.
10. Save assessment, factor evidence, decision, alerts/cases and audit in the existing transaction.
11. Portal users inspect the saved evidence.

Example: customer 50, transaction 30, screening 0 with 40/40/20 weights gives 32 overall and REVIEW under the sample bands.

## Database inspection

Client 1 has 8 factors, 38 sample rules, 4 bands and 1 model. Category weights total 100%. Bands cover 0–29, 30–59, 60–79 and 80–100. No transactions existed during inspection. The model is still labelled UAT Seed.

## Fixes made in this review

NPR-only scoring; positive amounts with at most two decimals; validation of model/factor weights; rejection of missing categories; full band coverage validation; numeric DEFAULT matching correction. The calculator rejects invalid configuration rather than returning an ALLOW result.

## Remaining work before live

1. Name the business approver and approve actual factor values, especially country risk. Existing sample values are not approved live policy.
2. Introduce separate maker/approver permissions if required by the production policy. Factor-level drafts and atomic activation are implemented; the same RiskScoring.Manage user can currently edit and activate. This is not independent approval or a single release spanning all factors, model weights and bands.
3. Add approved model-weight editing if required. Rule/value/range and band create/edit/soft-delete are implemented; factor weights and model weights remain DBA-managed configuration.
4. Replace legacy hardcoded customer create/update scoring with the same approved model.
5. Review automatic CONFIRMED_MATCH classification: name similarity alone does not establish confirmation.
6. Implement the separate API, trusted tenant identity, idempotency and validated inputs.
7. Verify concurrent evaluation, client isolation, rollback and boundary values against SQL Server.
8. Persist complete model/band versions and compliance-rule contributions in immutable evidence.
9. Verify watchlist freshness/completeness, hosting, secrets, backups and monitoring.

The project is not yet verified for production. Compilation alone does not prove the authenticated database workflow.

## Create, edit and delete upgrade — 5 September 2026

See [RiskScoring-CRUD-Guide.md](RiskScoring-CRUD-Guide.md) for the two-menu workflow, table usage, manual SQL checks, changed files and local verification.

Approved rule values and band values are no longer edited in place. The first change copies a complete factor or complete band set into the next draft version. Subsequent edits affect that draft. Deleting marks the draft row with DeleteFg=Y, DeleteDate and DeleteBy. Approval metadata and inactive historic versions are retained. Activation validates the complete set and retires the previous active version in the same SQL transaction as its audit entry.
