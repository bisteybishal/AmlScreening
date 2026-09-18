# Automated UAT result

Run date: 17 September 2026 (Asia/Katmandu)

Result: **54 checks passed, 0 failed**.

The run created a uniquely named synthetic SQL Server database, applied the release SQL, used the actual repositories and stored procedures, started the actual portal assembly for HTTP checks, and removed the synthetic database at the end. The existing `AMLScreening` database was not modified.

Covered checks include:

- partner create/list and duplicate protection;
- API username and one-way secret hash stored on the same user identity;
- user creation under the selected partner, role union for portal/API access, and protected platform roles;
- client/private versus GLOBAL AML visibility, direct-ID access, update/delete isolation, and fail-closed unauthenticated queries;
- missing-prefix workflow candidates and Unicode name normalization;
- import upsert, ownership preservation, local private-record protection, and import authorization;
- one-session-per-user, wrong-client session rejection, idle/absolute expiry, API-only portal denial, inactive partner denial, and concurrent browser arbitration;
- compliance maker-checker, cross-client rejection, stale/replay rejection, and same-transaction audit;
- IPv6 and bounded error logging;
- actual HTTP login, antiforgery, partner form, user form, client denial, API-only denial, and permission-change cookie invalidation.

This is an automated UAT gate, not a compliance certification, penetration test, load test, visual review, or business sign-off. Run the documented manual UAT against your UAT database before production.
