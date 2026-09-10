# Repository changes and deployment checks

## Approach

Keep the existing small, feature-specific repositories. EF Core handles AML records and compliance rules; Dapper handles the existing SQL queries and import stored procedure. No generic repository, database migrator, or permanent test project was introduced.

The normal flow is: controller checks action permission and validates the form; repository loads the permitted stored row; repository copies editable fields; database saves the change and audit in one transaction.

## Changes made

- AML record detail reads now use the same ownership scope as the list. Unauthenticated AML reads return no rows.
- AML pagination bounds page size to 1–50 and page number to the available pages. An excessive page request cannot overflow the offset. Sorting remains CreatedDate then Id.
- Country totals exclude null and blank country names.
- AML updates authorize the stored record, not client/owner values on the supplied entity. Only the editable AML fields are copied. ClientId, OwnerUserId, IsShared, creation details and deletion details cannot be changed through UpdateAsync.
- Compliance updates load the client-filtered stored rule and copy explicit editable fields instead of attaching the entire supplied entity. Existing approval-related editable fields are preserved as part of the current controller contract; this is not a new approval workflow.
- Both EF repositories require a signed-in client/user for writes, set creation and update actors from the current user, and keep soft deletion and transactional audit writes.
- Update calls require an eight-byte SQL Server row-version token. Stale update failures still return false for the controller to handle.
- Existing actor arguments on UpdateAsync/DeleteAsync remain for controller compatibility, but the authoritative audit actor is now ICurrentUserContext.Username.
- User permission inheritance and permission editing calculations exclude inactive roles. Explicit user overrides remain supported.
- User and subscription paging use a long offset to avoid integer overflow.
- Import transactions are disposed even when an import fails. The existing all-or-nothing import behavior was not changed to silently skip errors.
- Removed the obsolete commented AML GetAllAsync declaration and implementation.

## Not yet certified for live use

These are focused repository improvements, not a full security or live-database certification. No database schema or stored procedures were changed in this pass.

1. Test with two clients and multiple users: private records stay private, shared records follow the existing client scope, and only authorized platform access crosses clients.
2. Verify add/edit/delete action permissions at every controller endpoint. Repository ownership checks do not replace action authorization.
3. AML and compliance delete methods currently do not receive the row version from the displayed form. Add that contract if deletion must reject a row edited since the page was opened.
4. User listing joins can duplicate users when multiple roles are assigned. GetById may also fail in that situation. Decide whether the product supports one role or multiple roles before enforcing the corresponding schema and display behavior.
5. Role assignment and permission replacement need stronger repository-level validation against tenant/platform privilege escalation, plus concurrent-update protection.
6. Compliance GetAllAsync is still unbounded. Introduce matching controller/view pagination before large rule collections are used.
7. Audit summaries are not a complete before/after history. Review full audit requirements and source-sync audit atomicity.
8. Validate actual SQL procedure definitions, indexes, nullability, backup/restore, connection configuration and permissions against the deployed database.
9. Previously identified risk-model configuration, source masters, subscription entitlements and separate API work are outside this repository pass.

## Manual checks

- Open an AML record belonging to a different regular user: deny access unless sharing/client-view permission permits reading it.
- Edit a record and confirm ClientId, OwnerUserId, IsShared and CreatedBy are unchanged; UpdatedBy must be the actual signed-in username.
- Open the same edit form twice, save one, then save the other: the second save must report a conflict.
- Submit page 0, a very large page, and an excessive page size: paging stays bounded.
- Deactivate a role, refresh the user's authentication/permission state, and verify inherited permissions disappear. Explicit overrides must be reviewed separately.
- Force a database/audit failure in an isolated test database: no partial successful business change should remain.

Build verification does not replace these SQL-backed checks. No production data should be used for destructive testing.
