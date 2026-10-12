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