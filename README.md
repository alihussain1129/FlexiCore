# FlexiCore

Configurable, multi-tenant business management platform (Flutter for Android and Windows, ASP.NET Core, PostgreSQL).
Working name; branding is not final.

## Status

Backend phases 1a to 2b-C are implemented; there is no client application yet. See `DECISIONS.md` for the reasoning behind each phase.

**Implemented and tested (backend):**
- API host with `/health/live` and `/health/ready` (readiness checks PostgreSQL), per-request correlation id, JSON console logging.
- PostgreSQL 18 in Docker with two roles: `flexicore_owner` (migrations) and `flexicore_app` (runtime; not a superuser, owns nothing), and a forward-only SQL migration runner.
- Identity: registration, login, access tokens, rotating refresh tokens with reuse detection, logout, account lockout, per-IP rate limiting, session validation on every authenticated request, log redaction.
- Append-only audit trail (`audit.events`).
- Tenancy: organizations, companies, branches and memberships with PostgreSQL row-level security, a transaction-local tenant context, role-aware policies, and organization and company endpoints (create, list, get) with a cap of 10 owned organizations per user, audited successes and refusals, and a per-user creation rate limit.
- Unit, architecture and integration tests (integration tests run against a real PostgreSQL via Testcontainers).

**Not implemented:** membership management endpoints, branch endpoints, any business module, licensing and activation, and the Flutter app.
## Prerequisites (Windows)

.NET SDK 10.0.401 (see `global.json`), Docker Desktop (WSL2 backend), Git. Flutter is not needed until slice 1d.

## Run it locally (PowerShell, from the repo root)

1. Create `deploy/containers/.env` from `.env.example` with real dev-only passwords (the file is git-ignored; never commit it).
2. Start PostgreSQL:
   `docker compose -f deploy/containers/compose.yaml up -d`
3. Apply migrations as the owner role:
   `$env:FLEXICORE_MIGRATIONS_CONNECTION = "Host=127.0.0.1;Port=5432;Database=flexicore;Username=flexicore_owner;Password=<owner password>"`
   `dotnet run --project backend/src/FlexiCore.Migrations`
4. Give the API the app-role connection string (stored outside the repo):
   `dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5432;Database=flexicore;Username=flexicore_app;Password=<app password>" --project backend/src/FlexiCore.Api`
5. Run the API:
   `$env:ASPNETCORE_ENVIRONMENT = "Development"; dotnet run --project backend/src/FlexiCore.Api --urls http://127.0.0.1:5080`

## Tests

`dotnet test backend/FlexiCore.slnx` (Docker Desktop must be running; integration tests fail rather than skip if it is not).

See `DECISIONS.md` for the reasoning behind the choices made so far.