# Decisions

Each entry records what was decided, why, and any known limitation. Planned work is not listed here.

## Current implementation snapshot

This snapshot supersedes earlier phase notes where they describe the client or licensing as not yet implemented. FlexiCore currently has a Flutter Windows client, hosted identity and tenancy APIs, Local license enforcement and one-time local first-owner setup. Organization/company records are persisted in PostgreSQL. The client data-exchange workflow supports those records only; no transactional ERP modules or financial documents exist yet. See the root README for supported operations and current verification gaps.

Local license activation requests can be copied or saved from the client and signed offline by the owner tool. No production public key set is embedded, so production Local activation is not ready. The license import endpoints currently do not implement owner-only authorization; the earlier “Temporary choices” warning remains applicable. Do not treat the current implementation as a completed commercial licensing model.

The CSV/XLSX import and export and workspace-directory DOCX report are local client-side file operations over server-authorized organization/company data. Imports are create-only. The DOCX report is not a template for invoices, purchase documents or financial reports. Database backup and isolated restore instructions are in the root README; restore drills have not been qualified.

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

## Phase 12c: owner licensing tool and interoperability vectors

- Scope: FlexiCore.LicenseTool (backend/tools) for owner-only key generation and license issuing, with its tests, public interoperability vectors, and architecture rules. Nothing in the product references the tool. No change to API behavior, sign-in, Identity, Tenancy, the schema, or the Flutter client. No production key was generated and none is embedded.
- Format: the tool signs the exact payload bytes inside the file with ES256 (ECDSA P-256 and SHA-256), signature as raw 64-byte r||s, base64url without padding, in the existing envelope. Payload JSON is compact with the verifier field order, whole-second UTC timestamps, and an explicit expiresAt (null for perpetual). Public keys are exported as DER SubjectPublicKeyInfo in the exact key set shape ProductionKeyLoader parses, and the output is re-parsed by that loader before it is written. This closes the two items that 12b left open for the owner tool.
- Self-check: every issued license is verified with the real LicenseVerifier (clock pinned to notBefore) and its payload compared field by field before anything is written. The tool refuses requests the verifier would reject, fixes the product to FlexiCore, and requires an explicit expiry or the word never.
- Key storage: passphrase-encrypted PKCS#8 files (PBES2, AES-256, PBKDF2-SHA256 with 600000 iterations). The passphrase is typed at a prompt, never an argument, environment variable, or pipe, with a 16 character minimum. keygen refuses folders inside a git working tree, warns about cloud-synced folders, restricts the file to the current user (icacls on Windows, 0600 elsewhere), and never overwrites without --force. The signer sits behind ILicenseSigner so a KMS or hardware token can replace the file key without changing the license format.
- Recovery plan: two keys with different ids, active and standby, in separate storage, both public keys embedded in the product. If the active key is lost, issue with the standby. If it leaks, there is no revocation: ship an update that stops trusting that key id and re-issue. Backups: two encrypted copies on separate media in different places, passphrase in a password manager plus paper stored separately. Details are in the tool README.
- Issue ledger: an optional append-only JSON line file (license id, licensee, key id, dates, SHA-256 of the file). It is the only record of what was signed. The tool refuses a ledger inside a git working tree because it names customers.
- Vectors: backend/tests/FlexiCore.UnitTests/LicensingTests/Vectors holds a test public key (interop-test-1) and valid, expired, wrong-product, and tampered licenses, with no private key. The signing key existed only in memory during generation. Signatures are randomized, so the files were generated once and are pinned by SHA-256 in InteropVectorHashes.cs; regenerating them is a deliberate act. A folder-level .gitattributes disables line-ending conversion. The id interop-test is reserved: build-keyset refuses it and a test fails if it ever appears in the production key set.
- Enforced by tests: nothing references the tool except its own tests; the tool has no package dependencies; the Licensing library and the Api contain no signing or private-key API; no key file or private key block exists in the backend tree; .gitignore blocks key and ledger files.

### Still open after 12c

- The production key has not been generated. When it is, embed production-public-keys.json and change the integration test that expects state unavailable in the same commit.
- Independent check done (2026-10-16): the Python cryptography library (50.0.2) verified the signatures on valid, expired and wrong-product and rejected tampered. This covers the signature layer only (ES256, raw r||s, base64url, SPKI key encoding); status logic is verified by the .NET tests. It is a manual check and is not part of the test run.
- File-based key storage depends on the passphrase and on where the file is kept. A KMS or hardware token is the next step up. The passphrase policy checks length and variety only.
- The ledger is a plain file, not tamper-proof. Revocation, device binding, owner-only import, and enforcement are unchanged from 12b.

## Phase 13a: anonymous local activation (loopback)

- Scope: backend only. In Local mode, GET /api/v1/license/status and PUT /api/v1/license accept a signed-in user or an anonymous caller on the machine itself, so a customer can activate with the owner-issued license without email registration or password sign-in. No change to Hosted mode, LicenseService, the verifier, the file store, Identity, Tenancy, the schema, or the Flutter client. No production key was generated or embedded.
- Rule: the LocalActivation policy is registered only when Deployment:Mode is Local and is applied only to those two routes. An anonymous caller is accepted only if all of these hold: the remote address is present and loopback (127.0.0.0/8, ::1, or IPv4-mapped loopback); none of Forwarded, X-Forwarded-For, X-Forwarded-Host, X-Forwarded-Proto or X-Real-IP is present; and the Host header is localhost, 127.0.0.1 or [::1], with any port. Anything else gets the normal 401. A missing remote address is refused.
- Local mode still comes only from server configuration (Deployment:Mode and License:Directory), fixed at startup. No request header, query string or body can switch it.
- Authorization is now declared per route instead of on the license route group, so a route added later is never opened by accident. Hosted status still requires sign-in and Hosted still has no import route (PUT is 404).
- Unchanged: signature, expiry, rollback and size checks, the import rate limit, and every other endpoint. Anonymous callers share the ip:127.0.0.1 rate-limit bucket.
- Tests: 25 integration tests in LicenseLocalActivationTests. The remote address is injected by a test-only startup filter in the test host, never by production code. Cases: IPv4, IPv6 and IPv4-mapped loopback allowed; 203.0.113.5 and a null address refused (status and import); each forwarding header refused; foreign Host refused and local Host with a port allowed; signed-in users from a non-loopback address still work; tampered, expired and older licenses still rejected with the file untouched; /auth/me, organizations and companies still 401 for anonymous loopback; Hosted unchanged. One existing test was renamed to Local_refuses_anonymous_callers_without_a_loopback_address. Result: architecture 35, unit 213, integration 217, 0 failed.

### Supersedes (Local mode only)

- The 12b interim rules "any authenticated user may import" and "status requires sign-in" now also allow an anonymous caller on loopback. Signed-in users keep their access. Hosted mode is unchanged.

### Known limitations

- Any process or user on the activation machine can call these two routes. It can only import a license signed by a trusted key, and the rollback check still refuses an older license, but it can replace the stored license with a newer valid one.
- Machines on the network are not covered. They must sign in. Remote activation without an account is not supported.
- A reverse proxy on the same machine makes every external request arrive from loopback. Such requests are refused only while the proxy forwards a header that the rule checks. A proxy that strips those headers would make external requests look local, so do not put one in front of Local mode. Forwarded-headers middleware is not configured. If it is ever enabled, this rule must be revisited.
- Anonymous activation does not replace owner-only import, which stays open (see below).

### Still open after 13a

- The production key has not been generated, so a real build still reports unavailable until Phase 12d embeds it.
- Owner-only import, enforcement against ERP endpoints, device binding, revocation and database audit events are unchanged from 12b.
- The Flutter activation screen that calls these two endpoints is not built.

## Phase 12c follow-up: transactional key-pair replacement

- keygen now stages the encrypted private key and the public key file under temporary names in the target folder. The private temporary file is restricted to the current user before any key bytes are written. With --force, an existing pair is moved aside to backup files, the new pair is moved into place, and the backups are deleted only after both moves succeed.
- If any step fails, the previous pair is restored and the temporary files are removed. If the restore itself fails, the error names the files to check by hand instead of hiding the problem.
- Before this change a failure between replacing the private key and writing the public file could leave a private key whose public file belonged to a different key.
- Tests: KeyReplacementTests simulates a failure while placing either file (forced replacement keeps the old pair, first-time creation leaves nothing behind) and checks that a successful forced replacement leaves only the new pair. The tests use a fake permission check and a failing file-move seam that only the test assembly can reach.
- Not covered by automated tests: the real Windows icacls call together with --force. A manual run in a temporary folder outside the repository is still to do. No production key was generated.

## Deployment: container images and migrations

- backend/Dockerfile has two published targets: runtime (the API) and migrations (the FlexiCore.Migrations console app on the plain .NET runtime image). Both run as the image's non-root user.
- deploy/containers/compose.yaml has three services. postgres is unchanged. migrate is a one-shot service that runs as flexicore_owner through FLEXICORE_MIGRATIONS_CONNECTION. api runs as flexicore_app and starts only after migrate completed successfully. Missing FLEXICORE_OWNER_PASSWORD, FLEXICORE_APP_PASSWORD or FLEXICORE_JWT_SIGNING_KEY stops compose with a message instead of starting with an empty value.
- Verified (2026-10-16): on an empty volume migrate applied all 8 scripts, exited 0, and the API started afterwards. Not yet verified: /health/ready against a freshly migrated container, and a registration or sign-in round trip.
- The API container runs as Production with Deployment__Mode Hosted, so registration is disabled unless Registration__Enabled is set explicitly. The host port is bound to 127.0.0.1:5265, the same port as a local dotnet run.
- Known limitations: no TLS inside the stack (see D8); ASP.NET data-protection keys live inside the container and are unencrypted (nothing uses them today because sign-in tokens are JWT); both containers log a harmless message about the optional Kerberos library libgssapi_krb5; Local mode is not packaged (no license directory volume); no backups, secrets management or cloud deployment yet.
## Phase 14a: device pairing (Local mode)

- Scope: backend only, Local mode only. A new FlexiCore.Devices project, migration 0011_devices and a pairing-code administrator command let an activated installation pair devices without customer accounts. Hosted mode never registers the Devices module, its authentication scheme, its policies or its routes. No change to Identity, Tenancy or Licensing code. A device is not a user and is never proof of which person acted.
- Storage: schema devices has devices and pairing_codes. Only hashes are stored (SHA-256 of a 256-bit random device secret; SHA-256 of the pairing code). Devices are never deleted, only revoked. flexicore_app has column-level grants only (insert name, secret_hash, is_owner; update last_seen_at and revoked_at).
- Pairing: POST /api/v1/devices/pair is anonymous but requires a valid license (the same Valid or ValidInGrace rule as enforcement, supplied to Devices through IDeviceActivationGate so Devices does not depend on Licensing), a one-time code, a device name of 1 to 80 characters, and passes a per-IP rate limit of 10 attempts a minute. Malformed, unknown, used and expired codes all get the same 400, so a code cannot be probed. A code is consumed atomically. The device secret is returned once, as Device <deviceId>.<secret>, and cannot be read again.
- Codes: valid for 10 minutes, single use, at most 20 unused at a time (serialized with an advisory lock). Owner-capable codes are created only by the administrator command (dotnet FlexiCore.Api.dll pairing-code --owner). An owner device can create ordinary codes through the API (POST /api/v1/devices/pairing-codes) but never owner-capable ones.
- Authentication: a policy scheme chooses by header. Authorization: Device goes to the device handler; anything else goes to the existing JWT scheme, unchanged. The credential is checked against the database on every request, so revocation takes effect immediately. A wrong secret, an unknown device and a revoked device give the same failure. last_seen_at is updated at most once a minute and a failure to record it never blocks a valid device.
- Authorization: policy device (any paired device) for GET /api/v1/devices/me; policy device-owner for GET /api/v1/devices, POST /api/v1/devices/pairing-codes and POST /api/v1/devices/{id}/revoke. Revoking maps to 204, 404 (unknown), 409 (already revoked) and 409 (last active owner device, which cannot be revoked; concurrent revocations are serialized).
- Audit: pairing, rejected pairing, code creation and revocation write to audit.events inside the same transaction as the change, except denials, which are written after the rollback. Actor kind and device id are kept in metadata because the shared audit work is not done yet. Metadata never holds secrets, codes or tokens. Devices has its own copy of the audit writer (the third one, after Identity and Tenancy); consolidation is planned.
- Enforcement: the license middleware lets only /api/v1/devices/pair through anonymously. Every other devices route needs an authenticated device and a valid license.
- Verified so far (2026-10-19, from command output): the solution builds with 0 warnings and 0 errors; unit tests 262 passed; architecture tests 39 passed, including the four Devices dependency rules. Integration tests for the device endpoints are not written or run yet, so the endpoint behavior above is not yet verified by tests.

### Known limitations

- The stack has no TLS. A device secret and a pairing code travel in plain HTTP, so pairing and device traffic are safe only on the same machine or a network you trust. Remote use is not supported until a secured option is tested.
- Anyone who can obtain a valid unused pairing code while the installation is licensed can pair a device. Codes are short-lived and rate limited, but the administrator must treat a displayed code as a secret.
- A device secret stored on a client is only as safe as that platform's secure storage. Revocation is the remedy for a lost device.
- Device activity is attributed to the device, not to a person.

### Still open after 14a

- Integration tests for pairing, authentication, owner-only routes, revocation and license gating.
- Android pairing screens and secure credential storage on the client.
- Consolidating the three audit writers and a first-class activity history.
- License binding for Docker deployments of Local mode.