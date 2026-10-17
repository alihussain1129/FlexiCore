# FlexiCore

FlexiCore is an in-progress, multi-tenant business application built with Flutter, ASP.NET Core and PostgreSQL. It is **not yet a complete ERP or production-ready commercial product**.

## Current product status

### Implemented

- ASP.NET Core API with PostgreSQL-backed identity, refresh-token rotation, audit events and tenancy.
- Hosted mode uses the existing registration/sign-in and access/refresh token flows. The Docker Compose deployment runs Hosted mode.
- Local mode has server-side license enforcement, an offline installation request code, license-file import, and a one-time, loopback-only first-owner setup followed by the normal persistent sign-in session.
- The owner-only `FlexiCore.LicenseTool` can issue installation-bound licenses and verify them. It is not shipped with the application.
- Organizations and companies can be created, selected, listed, and persisted through authenticated, tenant-scoped APIs.
- The workspace has searchable, sortable and paginated organization/company tables, row selection, TSV clipboard copy for selected/filtered rows, and a copyable workspace summary.
- Data exchange currently covers only organizations and companies:
  - `.xlsx` and CSV exports contain authorized records; the filtered export applies the workspace search, and the separate “all” action explicitly exports all authorized records.
  - `.xlsx`, CSV and import templates are available for organization/company creation. Imports show column mapping, sample valid rows, validation errors and duplicate slugs before confirmation. Imports are create-only; company rows are created under the selected organization.
  - Excel exports have named worksheets, styled headings, column widths, filters and a frozen heading row. CSV/TSV output protects formula-leading values. Formula cells are rejected on import.
  - A styled `.docx` workspace directory report can be exported for filtered or all authorized records. It contains organization/company directory data, not invoices or financial reports.
- XLSX import enforces compressed-file size, entry count, expanded-size, compression-ratio, row and column limits before importing.

### Not implemented or not yet verified

- No product catalog, inventory, purchasing, sales, invoicing, accounting, payroll or other transactional ERP module is implemented. The workspace intentionally does not present these as working modules.
- No financial records exist, so currency/date formatting and financial totals are not applicable yet. Data exchange does not currently provide individual-cell clipboard selection, arbitrary business-entity imports, or DOCX invoices, purchase documents, product catalogs or financial reports.
- There are no saved document-branding/layout settings or customization settings that alter business workflows.
- No production public license keys are embedded. The Local license flow and owner tool are exercised with test keys in automated tests, but a customer-issued license cannot be accepted by a production build until an approved public key set is generated and embedded. Never use test keys for a deployment.
- Backup and recovery steps are documented below, but restore drills, fresh-machine installation and end-to-end offline customer activation have not been qualified as production procedures.
- Local setup and data exchange are covered by focused tests; manually exercising the complete desktop import/export experience against a deployed Windows application remains outstanding.

See [DECISIONS.md](./DECISIONS.md) for architectural choices, security boundaries and known limitations. The licensing tool has separate owner-only guidance in [backend/tools/FlexiCore.LicenseTool/README.md](./backend/tools/FlexiCore.LicenseTool/README.md).

## Requirements

- .NET SDK 10.0.401 (see `global.json`)
- Flutter and Dart versions compatible with `client/pubspec.yaml`
- Docker Desktop with the PostgreSQL test containers available
- Windows 10/11 for the Windows desktop target

## Start the Hosted development stack (PowerShell)

From the repository root:

1. Create the ignored environment file and replace the development placeholders with unique local-only values:

   ```powershell
   Copy-Item .\deploy\containers\.env.example .\deploy\containers\.env
   ```

2. Start PostgreSQL, apply migrations and start the API:

   ```powershell
   docker compose -f .\deploy\containers\compose.yaml up --build -d
   ```

   The API is published at `http://localhost:5265` on loopback. Compose is configured for **Hosted** mode; do not expose it to an untrusted network without configuring TLS termination and deployment security.

3. Run the Windows client in another terminal:

   ```powershell
   Set-Location .\client
   flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5265
   ```

The first container initialization creates the database roles. The migration service applies the forward-only migrations before the API starts. Do not commit `.env` or use its local passwords outside development.

## Local licensing and first-owner setup

Local mode requires `Deployment__Mode=Local`, an absolute `License__Directory`, and an installed valid license. The API must be bound to loopback for the anonymous activation and first-owner setup endpoints; the setup endpoint also checks the connection address, Host and forwarding headers. After the first owner is created, Local sign-in uses the same authenticated session and refresh-token behavior as the normal identity system.

The activation screen displays a machine request code that can be copied or saved as text. The owner uses the protected licensing tool to issue an installation-bound `.lic` file; the file can then be imported locally without internet access. License enforcement is performed by the API, not only by the Flutter client.

**Production Local activation is currently blocked:** no production public key set is embedded. Test-vector keys are for automated tests only. The tool README describes safe key generation and issuance; do not generate a production key inside this repository.

## Supported data exchange

Current import/export actions are integrated in the organization/company workspace. The server remains authoritative for authorization and company scope; an imported parent organization or record ID cannot change that scope. Imports only create records and report per-row failures; they do not update or delete existing data.

CSV/XLSX/TSV and DOCX exports are generated on the client from records returned by the API. `.xlsx` and `.docx` generation is verified by reopening the produced packages in automated tests and checking their contents/metadata. CSV is parsed back in tests. This verification does not certify interoperability with every third-party spreadsheet or word-processing application.

## Database backup and recovery

Back up to encrypted, access-controlled storage. A PostgreSQL custom-format dump is consistent while the database is running. In Windows PowerShell, use `cmd.exe` for binary redirection so PowerShell does not transcode the dump:

```powershell
New-Item -ItemType Directory -Force D:\Backups | Out-Null
& $env:ComSpec /d /c 'docker compose -f .\deploy\containers\compose.yaml exec -T postgres pg_dump -U postgres -Fc flexicore > "D:\Backups\flexicore.dump"'
if ($LASTEXITCODE -ne 0) { throw "Database backup failed." }
```

To inspect and rehearse a restore without overwriting the running database, copy the dump into the database container and restore it to a separate empty database:

```powershell
$container = (docker compose -f .\deploy\containers\compose.yaml ps -q postgres).Trim()
if (-not $container) { throw "The PostgreSQL container is not running." }
docker cp D:\Backups\flexicore.dump "${container}:/tmp/flexicore.dump"
docker compose -f .\deploy\containers\compose.yaml exec -T postgres pg_restore --list /tmp/flexicore.dump
if ($LASTEXITCODE -ne 0) { throw "The backup archive could not be read." }
docker compose -f .\deploy\containers\compose.yaml exec -T postgres createdb -U postgres -O flexicore_owner flexicore_recovery
if ($LASTEXITCODE -ne 0) { throw "Could not create the isolated recovery database." }
docker compose -f .\deploy\containers\compose.yaml exec -T postgres pg_restore --no-owner --role=flexicore_owner --dbname=flexicore_recovery /tmp/flexicore.dump
if ($LASTEXITCODE -ne 0) { throw "Database restore failed." }
```

Validate the restored database and application using a separately configured API connection before switching any deployment to it. Keep the original database and backup unchanged until recovery is confirmed. Schedule backups and test restores regularly; this repository does not automate backup retention or perform a restore drill.

## Build and test

From the repository root:

```powershell
dotnet build .\backend\FlexiCore.slnx --configuration Release
dotnet test .\backend\FlexiCore.slnx --configuration Release
Set-Location .\client
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
flutter build windows
```

Backend integration tests use real PostgreSQL containers; Docker must be running.
