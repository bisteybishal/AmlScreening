# Verification record — 12 September 2026

## Verified in this update

- Reviewed current portal/API source and performed read-only SQL metadata checks. The local database has no owner field on Customers or transactions, no CustomerKycResults or ApiApplications table. It currently contains only the admin account; older screenshots are not current database fixtures.
- Preserved current photo fields and lookup, fixed the view route to GetPhoto, and retained owner-aware photo queries. Raster response checks accept JPEG/PNG only and send nosniff.
- Added complete account-access controller/repository/view, portal enablement flag, create-user checkbox, authenticated API application mapping and logout-session release. Roles do not decide the channel; module permissions still apply.
- Built the isolated portal and API projects, including Razor views: zero compilation errors.
- Parsed all nine SQL scripts with SQL Server ScriptDom: no syntax errors. Checked screening.js with node --check: no syntax errors.
- Ran 32 focused checks: original 19 ownership/query/scoring checks plus photo mapping, portal enablement on same UserId/ClientId, password/inactive-account denial, API mapping/ambiguous claims rejection and service commit/disposal ordering.
- API actor resolver is linked into the unit project to avoid loading the two web hosts' different authentication dependencies into one test process. Both web hosts are separately built normally.

## What these checks do not establish

No migration was applied to the user's database. SQL parsing checks syntax, not execution. Generated EF SQL and test doubles do not prove real database ownership, rollback or concurrent sessions. No real OAuth-token requests, browser visual tests, external provider freshness, PEP/CIB integration, migration replay or load tests were performed. NuGet vulnerability checks were unavailable; run a dependency audit with network access before deployment.

Complete the restored-test-database and HTTP checks in START-HERE.md and database/04-verify-readonly.sql before switching traffic. No active project files, database records, settings, users or running services were changed; all supplied code and verification builds stay under Documentation/OwnershipRelease.
