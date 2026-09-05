# Risk scoring: local create/edit/delete guide

**Superseded UI:** see [RiskScores-Simple-Flow.md](RiskScores-Simple-Flow.md).
The current portal has two controllers, fixed ten-row pagination and one Edit ranges
form. The older individual Add/Edit/Delete Band screens below are no longer exposed.

## What you can do now

| Menu | Actions | When the change takes effect |
| --- | --- | --- |
| AML Records | Existing Add, Edit, Delete | On successful save; deletion is soft delete |
| Compliance → Risk Scores | Add value/range, Edit, Delete, Activate changes | After activating that factor's complete draft |
| Compliance → Risk Score Setup | Add band, Edit, Delete, Activate changes | After activating the complete draft band set |

The two risk pages remain separate. Countries, occupations, purposes and similar entries are **scoring rules** under existing factors, not a replacement for every master/dropdown table in the portal.

## Start locally

1. Stop and rebuild your Visual Studio application, then start it again. An already-running process may still have the old screens.
2. Confirm that the local database is the intended test database. Do not test with real production records.
3. Run `Database/RiskScoring.CrudReadiness.sql` in SSMS. This script only checks schema, permissions, menus and versions; it changes no data.
4. Sign in as an administrator whose effective permissions include `RiskScoring.View` and `RiskScoring.Manage`. Sign out/in after changing role permissions so claims refresh.
5. Open the two menus under Compliance. Direct routes are `/RiskScoring/Index` and `/RiskScoring/Setup`.

No database migrator or permanent test project was added. This CRUD upgrade reuses existing risk tables and does not add a stored procedure. It requires the rowversion, deletion, approval and version columns from the earlier alignment scripts.

If preflight reports missing columns, have your DBA review `RiskScoring.AlignExisting.sql`. The existing initial setup sequence is alignment → `RiskScoring.sql` → `RiskScoring.SplitMenus.sql`. The seed script contains UAT examples; do not use them as an approved live risk policy or blindly rerun seed scripts against established production configuration.

## Example 1: add an occupation score

1. Open Risk Scores and select Occupation.
2. Click **Add risk score**.
3. For a local-only test, enter value code `TEST_OCCUPATION`, display name `Test occupation`, score `40`, and an explanation.
4. Click **Save draft**. The page shows a draft banner and the next version. Existing approved calculations are unchanged.
5. Review every entry in this factor, including other pages of the list. Keep a `DEFAULT` rule for values that are not listed.
6. Click **Activate changes** and confirm. New evaluations now use this factor version.
7. To remove the test entry, click its **Delete** button, confirm, then activate that factor again.

The future API must send the corresponding value code in its scoring input. Names are normalized to uppercase codes with underscores, for example `test occupation` becomes `TEST_OCCUPATION`. Adding a code here does not automatically change a partner's dropdown.

## Example 2: amount ranges

1. Select Amount, then Add risk score or Edit.
2. Enter a non-negative inclusive minimum, an inclusive maximum, score and explanation. Currency is NPR only.
3. Leave maximum empty for an open-ended range. A `DEFAULT` rule handles otherwise unmatched values; its numeric limits are ignored.
4. Save each change to the draft, then activate the complete factor.

For example, ranges `0–1,000.00` and `1,000.01–no maximum` do not overlap. Two ranges that both include 1,000 will be rejected on activation. Receiver-count ranges use whole numbers instead of decimals. Scores must be integers from 0 to 100. Business staff must choose and approve actual scores; these examples are only for testing the form.

## Example 3: add or remove a risk band

1. Open Risk Score Setup.
2. Use Add risk band or Edit to enter risk level, minimum, maximum, decision and explanation.
3. Save to draft. Adjust any neighboring bands in that same draft.
4. Before activation, the complete set must cover 0–100 exactly once. No gaps or overlaps are allowed. A blank maximum means 100.
5. Click Activate changes. An invalid draft stays editable and does not replace the approved bands.

To delete a band, delete it from the draft and expand/replace neighboring bands to cover its range. You cannot activate an empty band set. ALLOW, REVIEW and BLOCK are supported decisions. Existing assessments are not recalculated when configuration changes.

## Permissions, ownership and audit

- `RiskScoring.View`: see the risk pages.
- `RiskScoring.Manage`: create/edit/delete and activate. Button visibility and server-side authorization both enforce this.
- ClientId always comes from the signed-in user context; there is no editable ClientId field.
- Risk rules/bands are client configuration, not private records belonging to one user.
- AML Records retain separate Create/Edit/Delete permissions and ownership authorization. Seeing another user's/shared record does not by itself permit editing or deleting it.
- AML creation assigns ownership on the server and derives sharing from platform-admin access. Duplicate reference checks now include soft-deleted records to match the database's unique index.
- Risk deletion stores `DeleteFg='Y'`, deletion time (UTC) and the logged-in username. Old approved versions remain available in database history.
- SQL transaction locks serialize configuration writers per client. Rowversion rejects stale edit/delete forms. Data changes and their audit entries commit together or roll back together.
- Review all draft rows before activation. This simple workflow is not a separate maker/checker process and does not provide approval of a frozen cross-factor release.

## Tables used

| Table | Purpose |
| --- | --- |
| RiskFactors | Existing factor definitions, types, weights and order |
| RiskFactorRules | Per-factor value/range scores, draft/approved versions and deletion history |
| RiskBands | Versioned final-score ranges and decisions |
| RiskModels | Existing approved category weights and model version; read-only in this UI |
| AuditLogs | Who created, changed, deleted or activated configuration and when |
| Permissions / RolePermissions | Existing View and Manage access |
| MenuItems | The two existing risk menu links |
| RiskAssessments / RiskAssessmentFactors | Previously saved evaluation and factor evidence; configuration edits do not overwrite them |

Draft rows intentionally coexist with approved rows. Do not select all rows without filtering version/status when building the separate API; call the shared scoring/workflow service.

## Files changed for this upgrade

- `AmlScreening.Application/Models/RiskConfigurationInput.cs`: editable rule/band inputs and expected configuration errors.
- `AmlScreening.Application/Interface/IRiskScoringService.cs`: create/save/delete/activate operations.
- `AmlScreening.Application/Models/RiskScoringModels.cs`: draft-state information for the pages.
- `AmlScreening.Infrastructure/Services/RiskScoringService.Configuration.cs`: draft copying, CRUD, validation, activation, locking and auditing.
- `AmlScreening.Infrastructure/Services/RiskScoringService.cs`: latest-version display while the calculator keeps using approved configuration.
- `AmlScreening/Controllers/RiskScoringController.cs`: GET forms and authorized antiforgery-protected POST actions; safe error messages.
- `AmlScreening/ViewModels/RiskScoringViewModels.cs`: editable fields and page draft flags.
- `AmlScreening/Views/RiskScoring/Index.cshtml`, `Setup.cshtml`, `EditRule.cshtml`, `EditBand.cshtml`: separate lists, shared Add/Edit forms, confirmation and activation buttons.
- `AmlScreening/wwwroot/css/risk-scoring.css`: compact action styles.
- `AmlScreening/Views/Shared/_Layout.cshtml`: Add Band remains under the Setup tab/menu.
- `AmlScreening.Infrastructure/Repositories/AmlRecordRepository.cs`: defensive ownership checks, server-derived sharing and soft-deleted duplicate reference checks.
- `Database/RiskScoring.CrudReadiness.sql`: read-only deployment preflight.

## Local verification and limits

Release compilation and temporary SQL Server service checks are recorded in the task handoff. Tests use an isolated generated client, then delete only that client's temporary risk/audit rows. No permanent integration-test project is added and business-client configuration is not modified by those checks.

Verified on 5 September 2026: the solution Release build passed with zero warnings/errors; the read-only database preflight found the required columns and both active menu links; 19 SQL Server checks passed. These covered rule creation/edit/deletion, preserved active scoring during drafts, activation and fallback, stale edits, incomplete bands, band replacement, cross-client read/delete denial, missing Manage permission, audit/soft-delete history, overlapping/negative amount ranges, fractional receiver counts and non-NPR rejection. Temporary test rows were cleaned up. The temporary harness's package vulnerability lookup was unavailable because NuGet could not be reached; this was not a completed dependency security scan.

Before release, perform an authenticated browser walkthrough of Add/Edit/Delete/Activate with both administrator and read-only users. Also verify actual API evaluations, business-approved scores and weights, watchlist freshness, ownership behavior, backup/restore, HTTPS, host secrets, monitoring and operational sign-off. The separate partner API is not implemented by this CRUD upgrade. Production readiness is **not** established merely by these buttons working.
