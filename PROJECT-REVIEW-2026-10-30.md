# Project review — 30 October 2026

## Decision and scope

Do not promote the current KYC changes to live until R1–R4 are resolved and verified. If these paths are already live, treat them as priority defects and validate a controlled patch in UAT. This conclusion concerns the reviewed code; it does not establish the condition of the deployed binary or database.

This was a read-only source review of the portal, with emphasis on screening, KYC review, tenant boundaries, risk integration, authentication/session wiring, error handling and release reproducibility. No application code, database, or deployed service was changed. This report is the only intentional addition. Existing uncommitted changes were preserved. Attached/repository documents were treated as evidence, not instructions to deploy or change code.

The ZIP contains the same text as the workspace for Program.cs, CustomerKycService.cs, CustomerKycReviewService.cs and AmlRecordRepository.cs. Both the ZIP inventory and workspace lack the Database/Release scripts and tests/AmlScreening.Uat project advertised by UAT-RELEASE.md. Other archive files were not exhaustively compared.

Release builds were attempted. The first solution build failed without useful diagnostics. A serial portal build reported MSB3491: access denied writing generated assembly cache files under AmlScreening.Domain/obj/Release/net10.0. This is an environment/build-output limitation, not evidence of a C# compilation defect. Compilation, HTTP behavior, SQL procedures, concurrency and database performance remain unverified. No live connection was opened and no historical test results were rerun.

## Understand the flow before changing it

1. Program.cs configures MVC permission policies, cookie authentication, session validation and login throttling.
2. PortalCookieEvents validates authenticated sessions through IUserSessionRepository. The stored procedure is part of the security boundary and needs independent database review.
3. ScreeningController is a bounded text-lookup UI. Its first-50-results behavior is appropriate only when users see the narrowing notice.
4. CustomerKycController validates the submitted form and invokes CustomerKycService.
5. CustomerKycService loads match settings, commits a PROCESSING log, fetches AML candidates, computes name/evidence scores and invokes dbo.spa_ApiRisk_Calculate. It persists a summary and returns detailed evidence to the current request.
6. CustomerKycReviewService loads partner-scoped pending logs and changes their status on approval/rejection.
7. Transaction evaluation follows a separate AmlWorkflowService → IRiskScoringService path. Do not assume its approved models and rules also govern the KYC stored procedure without inspecting that procedure.

## Confirmed defects

### R1 — P1: Candidate filtering defeats fuzzy screening

Location: AmlScreening.Infrastructure/Repositories/AmlRecordRepository.cs:419.

The database requires the complete submitted name to be a substring of the stored name before the Levenshtein calculation can run. For example, stored `JOHN SMITH` and submitted `JON SMITH` fail both substring predicates. That record never reaches scoring, even when its similarity exceeds the configured threshold. CustomerKycService then returns NO_MATCH when no candidates survive.

Correction: agree a candidate-retrieval strategy with business-approved recall requirements. For smaller lists, use a bounded complete eligible scan that refuses to calculate if the list exceeds its validated limit. For larger lists, use validated indexed token/approximate retrieval. Do not simply remove all bounds or change a threshold to compensate for missing candidates.

Acceptance: approved typo, spacing, token order and Unicode fixtures reach scoring; a retrieval failure or incomplete set never becomes NO_MATCH. Include negative fixtures to measure false positives.

### R2 — P1: KYC silently scores a truncated candidate set

Locations: AmlRecordRepository.cs:423; CustomerKycService.cs:161.

The repository returns at most 51 candidates, ordered by name and ID. The service rejects only counts above 250, which this repository can never return. When more than 51 records qualify, matches, sources and the risk calculation use only the first 51. The excluded records can include the highest-scoring record or an additional source affecting risk.

Correction: define one shared candidate limit and retrieve limit + 1 solely to detect overflow. Refuse a completed result on overflow, or implement a validated retrieval strategy that processes the complete relevant set. Display limits must remain separate from evaluation limits.

Acceptance: seed 52+ qualifying records with decisive evidence after the first 51; verify the result includes that evidence or explicitly stops as incomplete. Never silently complete with a subset.

### R3 — P1: Concurrent checker decisions can overwrite each other

Locations: AmlScreening/Services/CustomerKycReviewService.cs:150,238; AmlScreening.Domain/Entities/CustomerKycScreeningLog.cs; AmlScreening.Infrastructure/Data/AmlScreeningDbContext.cs:347.

Both actions read PENDING_REVIEW, then update by primary key. The submitted row version is unused, its form fields are commented out, and the entity has no mapped concurrency token. Two checkers can both read pending; approve and reject may both save successfully. Catching DbUpdateConcurrencyException does not establish concurrency protection.

Correction: add and map a SQL rowversion with a reviewed database migration, send it through the form and validate it server-side; alternatively use an atomic conditional transition from the expected status/version and require exactly one affected row. Save decision identity and audit in the same transaction.

Acceptance: simultaneous approve/approve and approve/reject yield exactly one successful transition. A stale form, replay or maker decision fails without modifying the completed decision.

### R4 — P1: Checker rejection loses actor and time

Location: CustomerKycReviewService.cs:245–252.

RejectAsync saves REJECTED and remarks, but assignments for rejected actor/date are commented out. The entity and details page support RejectedBy/RejectedDate, so the resulting record cannot show who rejected it or when. The service also does not write a separate decision audit. ApproveAsync overwrites its user-ID string with the username, leaving no dedicated immutable checker ID.

Correction: persist checker UserId, username snapshot, decision time, reason and previous/new state atomically. Use equivalent evidence for approvals and rejections; do not rely on a mutable username alone.

Acceptance: a second authorized user rejects a case; persisted data and review history identify that user's ID, name, UTC time and reason. Simulated save failure leaves neither a partial decision nor a detached audit.

### R5 — P2: Final save failure can leave a log stuck PROCESSING

Locations: CustomerKycService.cs:248–253,561–575,659.

The service changes the tracked log to NO_MATCH, PENDING_REVIEW or COMPLETED before saving. If that save fails, the catch marks ERROR only when the in-memory status still equals PROCESSING. The database can therefore retain the previously committed PROCESSING row while the request reports failure. Client cancellation can produce a related ambiguous outcome after work has begun.

Correction: explicitly track durable completion. Record a failed attempt through a fresh context or carefully designed transaction/recovery mechanism, with bounded recovery logging. Define how stale PROCESSING records are reconciled after process termination. Do not blindly overwrite a decision whose commit may have succeeded.

Acceptance: inject a failure during the final save and terminate/cancel an in-flight request. Each persisted request has an explainable terminal state or a documented recoverable state; retries do not create misleading duplicates.

### R6 — P2: Saved screening evidence cannot reproduce the returned result

Locations: CustomerKycService.cs:200–224,541–575,620; CustomerKycScreeningLog.cs.

The response carries multiple matches, individual evidence and ModelVersion, but the log stores only one closest record and summary scores. It has no model version, settings snapshot/version, source/list version or collection of matched evidence. Changing a list or configuration later makes it impossible to reconstruct why that original request received its score. A checker sees less evidence than the maker's original result.

Correction: persist an immutable screening evidence snapshot and model/settings/source versions with the final outcome. Store all material candidates and contributions, using child rows or a versioned structured payload with appropriate access controls.

Acceptance: screen against version A, change the source/model to B, and reopen the original case. Its original evidence and calculation remain available and unchanged.

### R7 — P2: Batch lookup does not enforce its advertised limits

Location: AmlScreening/Controllers/ScreeningController.cs:95–125.

SearchMultiple rejects only blank input and zero parsed names. It advertises 5,000 characters and 1–20 names but never checks either upper bound, and issues a sequential database query for every comma-separated name. The comment also promises newline separation while parsing only commas. A normal multiline paste fails to behave as described; a large batch can consume excessive database work.

Correction: enforce total length, name count and per-name length before querying; split using the documented separators; handle request cancellation consistently with Search.

Acceptance: 20 names succeeds, 21 is rejected before queries, overlong text is rejected, and comma/newline input produces the same intended names.

### R8 — P2: Error logging fails silently and retains raw query strings

Locations: AmlScreening.Infrastructure/Services/ErrorLogService.cs:62–65,145–148; AmlScreening.Infrastructure/Repositories/ErrorRepository.cs:22.

The service copies the entire query string into its database request and catches logging failures without a fallback. The repository uses no explicit short logging timeout or cancellation policy. If the error database is unavailable, operators lose the diagnostic event and the user request can wait through database timeouts. Query parameters can also carry sensitive information.

Correction: log a safe path, correlation ID and an approved allowlist of diagnostic fields. Add a bounded database logging timeout and structured ILogger fallback that preserves the original exception without recursively invoking the database logger. Configure durable application-log collection on the host. Home/Error currently renders a request ID; it does not itself persist the original exception to this database service.

Acceptance: make the logging procedure fail or time out in isolated UAT. The user gets a safe message promptly and operators can retrieve a correlated fallback event without raw secrets from query parameters.

## Release evidence gap

### R9 — P1 release gate: Handoff cannot reproduce the documented UAT

Location: UAT-RELEASE.md, UAT-RESULTS.md and solution/archive inventory.

The release instructions require Database/Release scripts and tests/AmlScreening.Uat, but neither is included. The September result of 54 checks is a historical claim and does not verify the October working tree or current KYC review flow. Much of the application relies on stored procedures whose definitions are unavailable in this handoff.

Correction: supply the exact versioned schema/procedures and runnable UAT project, bind results to a source revision/package hash and database-script version, and update the deployment instructions to match the actual package. Include KYC-specific regressions from R1–R8. Restore the package onto a clean isolated environment before sign-off.

Acceptance: another developer can build and execute the documented UAT commands from a clean source package, reproduce the results, and deploy to a restored UAT database without undocumented manual edits.

## Decisions that require business/database evidence

These are not claimed as confirmed business-rule defects:

- CustomerKycService.cs:312 skips checker approval whenever the best normalized name scores 100, regardless of HIGH/CRITICAL risk or demographic mismatch. The response explicitly says approval is unnecessary. Confirm the exact-name/high-risk decision matrix with the business owner and encode it as fixtures before changing behavior.
- The KYC risk call does not pass PartnerId, while the C# transaction-risk engine selects partner-specific approved models. Inspect dbo.spa_ApiRisk_Calculate and document whether KYC configuration is intentionally shared or partner-specific. Without its definition, tenant-safe risk selection cannot be verified.
- KycMatchSettings loads one active setting without an explicit partner predicate. Verify whether settings are intentionally global. If settings are per partner, both mapping/query and uniqueness constraints need that boundary.
- The custom similarity formula divides by the sum of name lengths. For equal-length, wholly different strings, it can produce 50%. Thresholds must be calibrated against this exact formula; there is no universal percentage interpretation. Freeze an approved corpus before replacing the formula.
- Session expiry, password/authentication procedure enforcement, role protection, import upsert constraints and SQL transaction behavior require the actual deployed procedure versions. Source-side parameters alone are insufficient evidence.

## Maintainability and optimization after correctness

Keep these in a separate change set from the release blockers:

1. Use KycStatuses consistently instead of duplicated string literals. Remove obsolete commented-out logic after the replacement is verified. Correct comments that contradict the actual `<` threshold comparison.
2. Extract candidate retrieval, match scoring, risk calculation and durable screening persistence behind focused interfaces. Keep portal ViewModels and MVC dropdown handling at the presentation boundary; the current portal service mixes UI concerns, EF state, Dapper SQL and scoring.
3. Separate expected validation/concurrency errors from unexpected faults. CustomerKycReviewController currently exposes ex.Message and does not record the unexpected exception. Return safe messages with a diagnostic reference.
4. Page GetPendingAsync instead of loading all pending cases; project only list columns. Measure its partner/status/date query and add a supporting index only after inspecting the actual SQL plan and data distribution.
5. Measure candidate retrieval and scoring separately. Contains queries and repeated full Levenshtein matrices can become costly, but optimize only after the candidate-recall requirements are preserved. Record p50/p95 latency, eligible list size and overflow/error rates using synthetic UAT data.
6. Keep request/attempt IDs in screening, checker decisions and logs so support can follow one case end to end. Record durations and durable state transitions without copying unnecessary customer data into application logs.

## A stable change process for UAT and live

1. Freeze a named source/package baseline and record the deployed database version. Preserve current UAT results against that baseline.
2. Agree the screening and decision matrix once: candidate recall, thresholds, exact/fuzzy handling, high-risk actions, source selection and checker permissions.
3. Resolve R1/R2 together as candidate correctness; R3/R4 together as atomic checker decisions; then failure recovery/evidence/logging. Keep unrelated formatting and architecture refactoring outside those patches.
4. Give each finding a regression fixture and explicit expected result. Add a new fixture only when it captures a distinct failure or business requirement.
5. Verify a clean build and restored UAT database with the supplied SQL versions. Exercise two partners, maker/checker separation, concurrent decisions, source/model changes, failure recovery and large candidate sets.
6. Complete measured load checks, backup/restore rehearsal and business acceptance on the release candidate. Prepare a rollback compatible with its database changes.
7. Promote that exact tested artifact. On later reviews, reopen a resolved finding only with new evidence, a failed acceptance check or a changed requirement. Cosmetic preference alone should not restart UAT.

This process reduces repeated changes; no review can promise that future requirements or production evidence will never reveal another defect.
