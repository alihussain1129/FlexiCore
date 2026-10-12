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