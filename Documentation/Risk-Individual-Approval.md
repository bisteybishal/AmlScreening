# Risk Scores: individual approval

This replaces category-wide score approval. Risk Score Setup still approves adjoining
ranges together so a boundary change cannot create gaps or overlaps.

## Manual deployment

1. Back up the database and stop portal writes.
2. Run Database/RiskScoring.IndividualApproval.sql in the portal database. It adds
   nullable ReplacesRuleId. MakerChecker.sql must already have been applied.
3. Review the duplicate-copy preview for the selected ClientId. To archive only
   identical old pending copies, change ArchiveCopies to 1 and rerun. Keep the output
   with your deployment records. Approved rows are not changed or deleted.
4. Changed legacy pending copies need manual review; do not guess their original
   record or maker. They are not automatically approved. Recreate verified intended
   edits through the portal after archiving the legacy request under change control.
5. Deploy the portal and restart. Test with two administrators of the same client.

## Simple flow

- Add: insert one PENDING row, ReplacesRuleId is null.
- Edit: insert one PENDING proposed replacement pointing to the approved row.
- Delete: insert one pending removal pointing to the approved row. Approved data
  continues to work until a checker approves it.
- Each pending row has its own Review link. Approval changes only that row and,
  for edit/delete, retires only its referenced original.
- Same maker cannot approve. Admin role, permission, client scope, row concurrency,
  review snapshot and transactional audit remain enforced by the server.
- Existing approved scores retain their IDs and revisions when unrelated scores
  are added. Calculation reads all active APPROVED scores, not the maximum category
  revision. Unapproved and archived rows never affect calculation.
- A category's DEFAULT rule must be approved first when setting it up initially.
  Approval validates the resulting active category, not other pending requests.
- Pending removals remain visible in the list so another admin can review them.

No database changes are executed automatically. External API code/stored procedures
must also read ClientId-scoped active APPROVED rows, without selecting only the
maximum RuleVersion for a whole factor. That separate project is not modified here.

## Verification

Portal Release build: zero errors and warnings. A temporary SQL Server tempdb test
schema passed 24 checks, including one added row with unchanged approved rowversions,
one-at-a-time approval, maker/checker/client/permission guards, stale review rejection,
edit/delete isolation, approved-score calculation across revisions and audit entries.
The test schema was removed afterward; the application database was not changed.
The temporary test build could not retrieve NuGet vulnerability data (NU1900);
this does not replace a deployment dependency-security review. Browser UI was not
visually exercised in this run.
