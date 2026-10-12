# FlexiCore

Configurable, multi-tenant business management platform (Flutter for Android and Windows, ASP.NET Core, PostgreSQL).
Working name; branding is not final.

## Status

Phase 1, slice 1a (backend foundation) only. Nothing else is implemented.

**Implemented and tested:**
- API host with `/health/live` and `/health/ready` (readiness checks PostgreSQL), per-request correlation id, JSON console logging.
- PostgreSQL 18 in Docker with two roles: `flexicore_owner` (migrations) and `flexicore_app` (runtime; not a superuser, owns nothing).
- Forward-only SQL migration runner (advisory lock, atomic scripts, immutable once applied, refuses a database ahead of the build).
- Unit, architecture and integration tests (integration tests run against a real PostgreSQL via Testcontainers).

**Not implemented:** authentication, tenants, branches, roles, row-level security, any business module, and the Flutter app.

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