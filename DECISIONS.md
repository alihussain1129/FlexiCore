# Decisions

Each entry records what was decided, why, and any known limitation. Planned work is not listed here.

## D1. .NET 10 and a modular monolith
The installed SDK is .NET 10. One deployable API process and one database; module boundaries are enforced by project structure and architecture tests rather than by separate services.

## D2. PostgreSQL 18, major version pinned
`postgres:18` in `deploy/containers/compose.yaml` and in the test fixture. The exact patch release is whatever the tag resolves to.

## D3. Two database roles
`flexicore_owner` runs migrations and owns objects. `flexicore_app` is used by the API at runtime and is neither a superuser nor an owner, so row-level security added later actually binds it. Verified: the app role cannot create tables; it can use tables the owner creates (default privileges set in migration 0001).

## D4. Forward-only SQL migrations, not EF Core migrations
Row-level security policies, default privileges and composite tenant foreign keys are first-class here, and EF migrations cannot express them natively. EF Core remains the intended tool for queries and writes. Runner guarantees (all covered by integration tests): one runner at a time, each script atomic with its journal row, applied scripts immutable (checksum), database-ahead-of-build refused.

## D5. No Serilog or health-check packages yet
Built-in JSON console logging and a small hand-written PostgreSQL health check. Fewer dependencies in the startup path. Revisit if structured sinks are needed.

## D6. Health endpoints expose only a status word
Failures are logged with the exception and a correlation id; the HTTP body is just `Healthy` or `Unhealthy`.

## D7. Correlation id limitation
`X-Correlation-Id` is accepted only if 1-64 characters of letters, digits, `-` or `_`; otherwise a new id is generated. The id is in the log scope for application logs written inside the pipeline. The host's own "Request starting/finished" lines are written outside that middleware and carry `TraceId` instead.

## D8. No HTTPS redirection inside the app
TLS is expected to terminate at a reverse proxy or ingress. This will be documented in the deployment guides when they are written.

## D9. Secrets
Dev passwords live in the git-ignored `deploy/containers/.env` and in .NET user-secrets. Nothing secret is committed. The test fixture uses throwaway credentials inside disposable containers only.

## D10. Test stack
xUnit v2 (from the template), Testcontainers.PostgreSql for real-database tests, Microsoft.AspNetCore.Mvc.Testing for in-process API tests, NetArchTest.Rules for dependency rules.

## Phase 1b-A: Identity (registration, login, access tokens)

- Identity is its own project (FlexiCore.Identity), referenced by the Api; it must not depend on the Api, the migration runner or EF Core (architecture tests enforce this). Data access is plain Npgsql.
- identity.users, identity.refresh_sessions and audit.events are global (not tenant-scoped). The Phase 1c "every table has RLS" guard needs an explicit allowlist for them. identity.refresh_sessions exists in migration 0002 but nothing uses it until Phase 1b-B.
- Passwords: framework PasswordHasher (PBKDF2, V3 format, HMAC-SHA512), 210,000 iterations by default, minimum 100,000 enforced at startup (Identity:Pbkdf2Iterations). Outdated hashes are re-hashed on successful login.
- Password policy: 12-128 characters (Unicode characters), no composition rules, and the password must not contain the email local part. The local-part check applies only when the local part has 4 or more characters.
- Email uniqueness is enforced on email_normalized (trimmed, invariant-lowercase, application-side; no citext extension).
- Access token: HS256 JWT, 15 minutes, 30 second clock skew, issuer and audience validated, algorithm pinned, claims sub and jti only. The `sid` claim and per-request session checks arrive in Phase 1b-B.
- Jwt:SigningKey: base64, at least 32 bytes, at least 16 distinct byte values; read from user-secrets in Development and from the Jwt__SigningKey environment variable elsewhere. The host does not start without a valid key.
- Registration:Enabled defaults to true in Development and false everywhere else unless set explicitly. When disabled, POST /api/v1/auth/register returns 403.
- Failed logins (unknown email, wrong password, inactive account) return one fixed 401 body. Unknown emails still pay a dummy hash verification.
- Audit: append-only for the app role (INSERT and SELECT only; a test proves UPDATE, DELETE and TRUNCATE are refused). 1b-A records register and login events; metadata never contains secrets.
- Known limitation: with no email verification yet, register answers "email already in use", which reveals registered emails. The fix arrives with email verification.
- Not in 1b-A: refresh tokens, logout, rate limiting, login throttling and lockout (Phase 1b-B).

## Phase 1b-B: Refresh tokens, logout, lockout, session claim

- No new migration: identity.refresh_sessions and the users.failed_login_count / locked_until columns from 0002 are used as they are.
- Refresh token: 32 random bytes, base64url (43 characters). Only its SHA-256 is stored (token_hash, 32 bytes). Plaintext tokens are never stored, logged or audited. The stored hash is also re-checked with a constant-time comparison after lookup.
- Lifetimes: access token 15 minutes; refresh token 30 days (Identity:RefreshTokenDays, 1-90); absolute family lifetime 90 days, fixed. A rotated token never expires later than family_expires_at, so rotation cannot extend the 90-day cap.
- Rotation is strict and has no grace window. The first use of a token rotates it; any later use of a rotated token is treated as theft and revokes the whole family (revoked_reason = reuse_detected). A client that submits the same refresh token twice, or two concurrent refreshes of one token, therefore loses its session.
- Concurrency: every operation that changes a token family first takes a Postgres advisory transaction lock on the family id. Rotation, reuse handling and logout are serialized per family, so exactly one of several concurrent refreshes succeeds and a logout cannot miss a token issued by a concurrent refresh.
- Every refresh failure (malformed, unknown, expired, revoked, reused, inactive user) returns one fixed 401 body; the reason appears only in the audit trail.
- Logout: POST /api/v1/auth/logout with { refreshToken } revokes the whole family. It returns 204 for any well-formed request body, whether or not the token exists. There is no logout-all yet.
- sid claim: the access token carries sid = the refresh family id. Every authenticated request checks that the family is not revoked and has not passed its absolute expiry (one indexed query). Tokens without a sid are rejected. Logout and reuse detection therefore end access tokens immediately, not after 15 minutes.
- Lockout: 5 consecutive failed passwords lock the account for 15 minutes (Identity:Lockout:MaxFailedAttempts, Identity:Lockout:Minutes). The counter lives on the user row, found through email_normalized, so email casing or whitespace cannot bypass it. While locked, the password is not checked and the response is the same fixed 401 as any other failed login. A successful login resets the counter; an expired lock restarts the count at 1. Known limitation: the counter has no time decay (there is no last-failure column), and an attacker can deliberately lock a known account.
- Rate limiting: built-in ASP.NET Core limiter, fixed window per client IP, shared by register, login, refresh and logout (Identity:RateLimit:PermitPerMinute, default 20), 429 with Retry-After. Known limitations: unknown emails have no row to lock and are limited only by this per-IP limit; the limiter is in-memory per instance; behind a reverse proxy every client shares the proxy's IP until forwarded headers are configured.
- Audit actions added: auth.refresh (success/failure with reason), auth.logout, auth.session.revoked (reuse_detected, user_inactive, logout), auth.lockout (denied), auth.login denied/account_locked. State changes and their audit rows commit in one transaction. Metadata holds ids and reasons only.
- Logging: nothing in FlexiCore.Identity logs, HttpLogging stays off, and IdentityModel ShowPII is forced off. An architecture test fails if Identity ever depends on Microsoft.Extensions.Logging, so adding logging there needs a redaction review first. An integration test captures all log levels and asserts that no password, access token, refresh token or Authorization value appears.
- Not done: purge of expired or revoked refresh_sessions rows (the app role cannot delete), logout-all, device labels, email verification, tenant isolation (Phase 1c).
## Phase 2b-C1: first tenant-aware API endpoints

- Five endpoints: POST and GET /api/v1/organizations, POST /api/v1/organizations/{id}/companies, GET /api/v1/companies, GET /api/v1/companies/{id}. All require a valid session.
- The caller is taken only from the validated access token's sub claim. No user or tenant identifier is accepted from a request body, header or query string. app.company_id is not used yet; company-scoped endpoints will select the company through the route and validate membership.
- Organization and company creation call the SECURITY DEFINER functions from migration 0005. The tables are not forced under row-level security because those functions rely on owner bypass; flexicore_app is not an owner and has no insert rights on them.
- Error mapping: SQLSTATE 42501 becomes a generic 403 (identical for "not allowed" and "organization does not exist"), 23505 becomes 409, 23514 becomes 400, 28000 becomes 401. A company the caller cannot see is a 404, identical to a company that does not exist.
- Input rules mirror the database CHECK constraints (name 1-160 characters without control characters, slug 3-80, industry key 2-64) so callers get 400 validation problems.
- Lists are capped at 100 entries, ordered by name then id. Real pagination comes when lists can grow large.
- Known gaps, planned for 2b-C2: no audit events for organization or company creation; no per-user cap on organization creation (needs a migration); no rate limit on these endpoints; a duplicate organization slug reveals that the slug exists, the same limitation as duplicate email at registration.
## Phase 2b-C2: audit and organization cap

- Migration 0006 replaces tenancy.create_organization and tenancy.create_company in place (CREATE OR REPLACE keeps owner and grants; 0005 is not edited).
- Successful creations are audited inside the same database transaction by the functions (actions tenancy.organization.create and tenancy.company.create, outcome success), so an audit row cannot be skipped and cannot exist without the object it describes.
- Refusals roll the transaction back, so the store audits them afterwards in a separate short transaction through TenancyAuditWriter. The audit table only accepts the outcomes success, failure and denied: forbidden, quota and no-active-user are denied; duplicate slug and invalid input are failure. The specific cause is in metadata.reason (not_permitted, limit_reached, no_active_user, duplicate_slug, invalid_input). Plain validation errors (400 before the store is reached) are not audited.
- Company creation rows always target the parent organization (target_type organization), for successes and refusals; a success carries the new company id in metadata.company_id.
- audit.events.tenant_id is left null; what it means will be decided with the first company-scoped audit reader.
- Cap: a user may own at most 10 organizations (active owner memberships), a constant in the function. Changing it means a new migration. A per-user advisory lock serializes creations so simultaneous requests cannot both pass the check. The cap raises SQLSTATE 53400, mapped to 403 with type urn:flexicore:problem:quota-exceeded. There is no per-organization cap on companies yet.
- TenantContext carries an optional CorrelationId, set into the transaction as app.correlation_id so functions can stamp audit rows. FlexiCore.Tenancy now references SharedKernel to read the request correlation id.
- TenancyAuditWriter deliberately duplicates the single insert from Identity's AuditWriter because Tenancy must not depend on Identity. Move the writer to a shared project when a third module needs it.
- Still open for 2b-C3: rate limiting on the creation endpoints.
## Phase 2b-C3: rate limit on creation endpoints

- Policy `tenancy-create`: built-in ASP.NET Core fixed window of 1 minute, no queue, applied only to POST /api/v1/organizations and POST /api/v1/organizations/{id}/companies. Both share one window per user. Reads are not limited.
- Partition: `user:<sub>` for authenticated callers, `ip:<address>` otherwise, so the limit is fair behind a shared proxy IP. Setting: Tenancy:RateLimit:CreatePermitPerMinute, default 10, validated to 1..1000000 (TenancySettings; an invalid value fails at startup).
- The limiter now runs after UseAuthentication (it must, to read the user). Identity's per-IP policy therefore also runs after authentication: a request that carries a bearer token has the token and session validated before it can be answered with 429. Register and login carry no token and are unaffected.
- Tenancy registers only a policy. The 429 status code and Retry-After header come from the global rejection handler registered by Identity; Tenancy must not replace it. There is no custom problem type for 429.
- 429 rejections are not audited: they happen before the store, change nothing, and a flood would fill the audit table.
- Known limitations: in-memory and per instance; a fixed window allows a burst across a window boundary; requests that fail validation (400) still count toward the limit.
## Phases 2a, 2b-A and 2b-B (recorded retrospectively in 2b-D)

These sections were not written when the phases were committed. This summary comes from the commit messages and the migrations; the detailed policy design is in migrations 0004 and 0005, which remain the source of truth.

- 2a: tenancy schema (organizations, companies, branches, memberships) with PostgreSQL row-level security.
- 2b-A: FlexiCore.Tenancy with TenantContext, TenantScope and TenantConnectionFactory. The context is set with transaction-local set_config, so it cannot leak to the next user of a pooled connection; tests cover pooled-connection leaks.
- 2b-B: role-aware branch write policies (migration 0005 replaced the branches_company_isolation policy) and the SECURITY DEFINER functions tenancy.create_organization and tenancy.create_company.

## Phase 2b-D: transaction isolation and session-race tests

- Both locking designs (refresh/logout re-reading the session after the family advisory lock, and the organization cap counting after the per-user advisory lock) need READ COMMITTED semantics. A probe showed that Npgsql starts a transaction with no isolation level at READ COMMITTED even when the database default is repeatable read, so the explicit pins do not change behaviour today. They state the requirement in code. Pinned: login, rotate and logout in RefreshSessionStore, TenantConnectionFactory, and both UserStore transactions (registration and the failed-login counter).
- Tests flip the database default to repeatable read and check that session ordering, the organization cap and failed-login counting still hold. They pass with or without the pins (confirmed for the UserStore pin; the probe explains why), so they show robustness to the default, not that each pin is necessary.
- Deterministic tests hold the family advisory lock from a test connection, queue refresh and logout in a chosen order (observed in pg_locks), then release it. Both orders must leave no usable token.
- audit.events has no foreign keys on purpose (migration 0003). A test guards this, and a refusal row can be written for an actor that does not exist.
- The intermittent failure of A_logout_racing_a_refresh_never_leaves_a_usable_token_behind (once, in a full run) remains unexplained. A sibling test now asserts the logout status over 25 iterations and dumps session and audit state on failure.
## Phase 2c-1: list and add organization members

- Migration 0007 adds tenancy.list_organization_members and tenancy.add_organization_member, the same pattern as 0006: SECURITY DEFINER, pinned search_path, held by the owner role, executable only by flexicore_app, success audited inside the function.
- Listing is for an active owner or admin of an active organization (otherwise 42501). It returns at most 500 members ordered by creation time then user id.
- Adding: owners may add any role, admins may add only the member role. The target is found by normalized email among active users. An unknown or inactive address raises the same 42501 as a permission failure, so the response does not reveal whether an address is registered. A duplicate raises 23505, an invalid role 23514. Success is audited as tenancy.member.add with member_user_id and role in metadata.
- Tenancy applies Trim().ToLowerInvariant() to the email inline, because Tenancy must not reference Identity. This duplicates Identity EmailRules.Normalize; see the pin test noted in 2c-3.

## Phase 2c-2: change role and remove member

- Migration 0008 adds tenancy.change_organization_member_role (owner only) and tenancy.remove_organization_member (owners remove anyone, admins remove plain members, anyone may remove themselves). A target that is not a member of the organization raises the same 42501 as a permission failure.
- Last-owner guard: demoting or removing an owner raises 55000 when the organization has one active owner or fewer, including an owner removing themselves. Both functions take a per-organization advisory lock (key tenancy.members:<organization id>) before checking, so two simultaneous changes cannot both pass the count. The count runs after the lock in a later statement, so it relies on READ COMMITTED, as recorded in 2b-D.
- Removing a member deletes their company memberships first (company_memberships references the organization membership); the number removed is recorded in metadata.company_memberships_removed.
- Successes are audited as tenancy.member.role_change (metadata includes from_role and role) and tenancy.member.remove.
- Routes: GET /api/v1/organizations/{id}/members, POST /api/v1/organizations/{id}/members, PUT /api/v1/organizations/{id}/members/{userId}/role, DELETE /api/v1/organizations/{id}/members/{userId}. The member list is not rate limited. Add, change role and remove use the tenancy-create policy from 2b-C3, so they share one window per user with organization and company creation.
- Known limitation: the guard fires whenever the target's role is owner, but it counts only active owners. Removing or demoting a non-active owner membership is therefore also refused while only one active owner exists.
## Phase 2c-3: pin test for email normalization

- Tenancy normalizes member emails in FlexiCore.Tenancy.MemberEmail.Normalize, which repeats Identity EmailRules.Normalize (trim, then invariant lowercase) because Tenancy must not reference Identity.
- EmailNormalizationPinTests in the unit test project references both modules and asserts that the two functions agree on mixed case, surrounding whitespace, accented, dotted capital I and titlecase inputs. Removing the lowercase step from MemberEmail makes it fail (checked by hand).
- If the normalization rule changes, change both functions and the pin test together.
## Phase 12b: license status and import (infrastructure only)

- Scope: GET /api/v1/license/status and PUT /api/v1/license, a file-based license store, and Hosted versus Local mode. Nothing is enforced: no existing ERP endpoint checks the license, sign-in is unchanged, no local sessions exist, the Flutter client is unchanged, and there are no schema or migration changes.
- Mode: Deployment:Mode is Hosted or Local. When absent or empty it is Hosted in every environment, so existing hosts and tests need no change. Any other explicit value stops startup. Local requires License:Directory as an absolute path. In Hosted mode no file is read, GET status returns state notApplicable, and PUT is not mapped (404).
- Keys: in Local mode the verifier is built only from the production public keys embedded in the Licensing assembly. No configuration or environment setting can supply keys. No production public key exists yet, so a real Local deployment reports state unavailable (reason no_trusted_keys) and refuses every import with 503, and a warning is logged at startup. Missing keys never lead to accepting a license, and unavailable is kept apart from invalid.
- Storage: one file, license.lic, holding the exact imported bytes. Writes go to a temporary file in the same directory and are moved over the old file, so a failed import leaves the previous license byte-for-byte unchanged. A license is fully verified before the store is touched. Status is re-evaluated against the clock on every read; no derived state is stored.
- Import rules: only Valid and ValidInGrace licenses are accepted. A license whose issuedAt is older than the installed one is refused (409), identical bytes return 200 with replaced false, and an unreadable or unauthentic current file never blocks a valid import. Import has its own per-user rate limit (License:ImportPermitPerMinute, default 10) and the body is capped at 16 KB without buffering more than the cap.
- Errors: RFC 9457 problem details with type urn:flexicore:problem:license-* and a reason extension; bodies never echo any part of the file. 400 malformed or unsupported version, 413 too large, 415 wrong content type, 422 rejected (unknown key, bad signature, wrong product, expired, not yet valid), 409 older than current, 503 no trusted keys or storage failure.
- Details for expired and not-yet-valid licenses: the verifier now returns the payload for authentic Expired and NotYetValid files so status can show them. IsAccepted is unchanged; callers must never base access on the payload alone.

### Temporary choices (must change before commercial release)

- Any authenticated user may import a license. This is an interim development rule and is NOT owner-only licensing. A commercial build needs a proper role or local-owner model so that only the machine owner or an administrator can import.
- Status is readable by any authenticated user.
- Import audit is structured log lines only (license id, outcome, correlation id). There are no database audit events.
- Import locking is in-process. Two API instances sharing one license directory could race, so Local mode assumes a single instance.

### Known limitations

- The license file is signed, not secret and not tamper-proof against the machine owner: anyone who can write to the directory can delete it or replace it with another valid license. There is no device binding.
- A user can patch the client or the API binary, including the embedded keys. Nothing in this slice defends against that.
- There is no revocation: a signed license stays valid until it expires.
- maxInstallations is informational only. Nothing counts or limits installations.
- Signature encoding is defined by the verifier (ES256, raw 64-byte r||s, base64url). It has been exercised only with .NET-generated test keys; an independent signer must be checked against a fixed test vector before release.
- Two things stay open for the owner tool: it must sign the exact payload bytes, and it must export the public key as DER SubjectPublicKeyInfo.

### Before commercial release

- Owner-only (or administrator-only) import, with sign-in rules for Local mode decided in the local-session slice.
- Embed the production public key from the owner tool, and add an interoperability test vector (public key plus signed file, no private key).
- Device binding, a revocation or replacement strategy, and a decision on tamper-resistant storage.
- Decide how and where licensing is enforced against ERP endpoints, including the effect on tenancy.
- Database audit events for import, and a plan for multiple instances if Local mode ever runs more than one.