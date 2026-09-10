# Risk Scores and Risk Score Setup: maker–checker

> Risk Scores now use individual approval. See [Risk-Individual-Approval.md](Risk-Individual-Approval.md)
> for the current workflow and required database script. The category-copy behavior
> described below is historical; the grouped Risk Score Setup range workflow remains.

## Verification on 2026-09-08 (original grouped implementation)

- Release solution build: 0 errors, 0 warnings.
- 26 SQL Server checks passed in a uniquely named schema in tempdb. Coverage includes pending creation, initial range creation/replay, administrator self-approval rejection, permission and role checks, cross-client denial, maker-only editing, stale review/replay denial, active-value preservation, pending deletions, range validation, missing legacy ownership and approval audit writes.
- Generated test tables and schema were removed after the run. The AML application database was not changed. No permanent test project was added to the solution.
- Razor views compile, but the updated pages have not been visually verified in a live browser. Test the final layout and two-account flow after applying the manual schema upgrade.

## What changed

Both modules keep the reference screenshots' compact blue tables, category tabs for scores, and a separate range table for setup. Scores show status and maker. Pages display ten score records directly, without a page-size/search submission requirement.

Saving an addition, edit or removal creates a Pending version. The previously Approved version remains available to the calculator. Another administrator reviews the complete active and pending sets side by side, including removals, then approves. There is no self-approval exception for administrators.

To keep this implementation small, one maker owns a pending category/range set. Other users cannot edit it. It is possible for the maker to revise a pending set; any such change invalidates a review already opened by a checker. This release does not add a separate Submit or Reject workflow.

Scores in one category are approved together. All risk ranges are approved together so two adjoining boundaries cannot be activated separately. Existing 0–100 validation, DEFAULT coverage, NPR calculation and category weights are not replaced with the screenshot's illustrative 10/15/20 scale.

## Deployment order — required

1. Back up the intended database.
2. Review and manually run `Database/RiskScoring.MakerChecker.sql` before deploying this application build. It adds nullable maker/checker IDs to score and range rows and grants the new approval permission to the existing active Admin role. It does not approve scores or ranges.
3. Review existing pending/legacy DRAFT versions reported by the script. If maker information is missing, both edit and approval intentionally fail closed. Do not guess ownership from the latest editor or automatically assign it to the logged-in administrator. Establish contributors from audit/history; where provenance cannot be established, archive and recreate the pending proposal through a controlled, audited recovery process. Approved history does not require backfilling.
4. Sign users out and in to refresh their role/permission claims.
5. Use two distinct Admin accounts in the same client. The maker requires RiskScoring.View and RiskScoring.Manage; the checker requires RiskScoring.View and RiskScoring.Approve. A user with both permissions still cannot approve their own proposal.
6. Validate against the deployed schema and role data before releasing to users. The script has not been applied to the application database by this task.

The Admin role name is matched exactly as `Admin`, consistent with the portal's existing role name. Permissions alone do not bypass the administrator requirement. No client ID or maker ID is accepted from an editable form.

## Portal use

- Risk Scores: choose category → Add score/Edit/Delete → save Pending → different administrator chooses Review changes → compare both complete sets → Approve changes.
- Risk Score Setup: Create ranges (empty state) or Edit ranges → save Pending → different administrator chooses Review changes → review every range → Approve changes. Initial 0–29/30–59/60–79/80–100 ranges are proposed form values only; they are not saved or active until users explicitly save and approve them.
- Pending sets with unknown maker IDs remain visible for inspection but cannot be approved.
- You can keep an incomplete set pending while adding missing DEFAULT/YES/NO entries. Approval validates completeness.

## Code map

- `RiskScoringService.Configuration.cs`: validate and save pending changes; ownership guard; transaction/client lock.
- `RiskScoringService.Approval.cs`: load a consistent review, check maker versus checker, detect stale reviews and activate atomically with audit.
- `RiskScoringService.cs`: existing calculation and read queries; approved-only calculation remains unchanged.
- `RiskScoresController` / `RiskScoreSetupController`: small page actions with Admin, permission and anti-forgery checks.
- `Views/Shared/RiskApprovalReview.cshtml`: one shared review screen, displaying active and pending sets, including removed entries.
- `ICurrentUserContext.IsAdministrator`: portal derives this from authenticated Admin role claims. Other hosts default to false and must explicitly provide trusted identity if they invoke configuration writes.

## Security and maintenance notes

- Maker/checker use stable user IDs; names are displayed and recorded for audit.
- All rows in a pending set, including soft-deleted rows, participate in the review snapshot and maker check.
- A review value is a fingerprint of the row IDs and SQL rowversions. It is not an authentication token. Server-side identity and permissions are checked separately.
- The existing transaction-owned client lock serializes edits and approvals. Audit and activation commit together.
- An old approval POST with no review value is rejected. The old unprotected activation service overloads were removed.
- Direct SQL writes and elevated database users remain outside application enforcement; protect database access and use controlled scripts.
- The separate Screening API must consume only approved applicable rules; IsActive alone is not sufficient because pending rows also exist.
- This is not a certification of the whole portal for production. Risk-model governance, partner API authentication, broader repository issues and operational deployment checks remain separate work.
