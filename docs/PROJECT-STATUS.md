# FlexiCore Project Status

Last verified: 2026-10-20
Repository: D:\Projects\flexicore
Branch at checkpoint: phase-16a-local-db
Last previously committed revision: 946c787

## Product direction

FlexiCore is an offline-first ERP for Windows Desktop and Android.
Each application must support standalone local business workflows and
persistent local data without requiring internet, cloud login, or the
other device to operate. Device pairing and synchronization are optional.

Preserve existing architecture, user changes, licensing enforcement,
public-key security, backend capabilities, and local data. Inspect before
editing; prefer incremental, tested changes over rewrites.

## Verified Flutter results

- Full Flutter test suite: 118 tests passed.
- Targeted local catalog and money tests: 20 tests passed.
- Flutter analyze: no issues found.
- Flutter version: 3.47.6.
- Dart version: 3.13.5.

The 20 targeted tests are included in the full suite; do not add these
counts together.

## Implemented foundations

- .NET 10 ASP.NET Core backend and PostgreSQL-based persistence.
- Existing identity/authentication, tenancy, catalog, licensing, and
  device-pairing foundations, based on previous project records.
- Drift/SQLite local database foundation in the Flutter client.
- Local Units and Products tables and database-opening infrastructure.
- Catalog domain models, validation, repository, controller, and UI files.
- Money utility using integer ten-thousandths for prices.
- A local catalog entry point in client/lib/main_local.dart.

Backend implementation history is not a fresh verification of the
current branch. Re-run the backend build and tests before claiming the
current backend is fully verified.

## Not yet verified as complete

- Complete standalone offline ERP workflows on Windows and Android.
- Actual persistence through normal app close/reopen on both platforms.
- Full local setup, ownership, and license activation integration.
- Complete inventory ledger, stock movements, sales/POS, purchases,
  customers/suppliers, accounting, and reports.
- Barcode scanning integrated with local product lookup and creation.
- Optional device linking and synchronization end-to-end.
- Premium responsive GUI across desktop and mobile.
- Complete Word, Excel, PDF, and CSV import/export workflows.
- Current-branch backend build, tests, and platform release verification.

## Known Git changes at checkpoint

Previously reported modified/untracked files:

- scripts/run-app.ps1
- client/flutter_01.png
- client/flutter_02.png
- client/flutter_03.png
- client/lib/core/money.dart
- client/lib/features/catalog/presentation/catalog_controller.dart
- client/lib/features/catalog/presentation/catalog_screen.dart
- client/lib/main_local.dart
- client/test/core/money_test.dart
- client/test/features/catalog_controller_test.dart
- deploy/containers/compose.yaml.before-api-network-fix

Preserve these changes. Do not use destructive Git commands or discard
user files.

## Next steps

1. Commit the reviewed local database/catalog changes and this status file.
2. Run the current backend build and tests; identify Docker-dependent failures.
3. Inspect the real app entry points and local database persistence.
4. Verify standalone offline workflows on Windows and Android.
5. Build missing ERP workflows incrementally with tests.
6. Then improve the responsive GUI and implement robust import/export.

## Working rules

Inspect first. Preserve uncommitted work. Do not rewrite from scratch.
Do not claim functionality is complete without evidence. Run tests after
changes and record verified results in this document.
