// ignore_for_file: recursive_getters
import 'package:drift/drift.dart';

part 'app_database.g.dart';

/// Local mirror of `catalog.units` (server migration 0012).
///
/// Ids are client-generated UUID strings so records keep a stable identity if
/// sync is added later. `version`, `updatedAt`, `originDeviceId` and
/// `deletedAt` mirror the server columns; deletes are soft deletes.
@DataClassName('UnitRow')
class Units extends Table {
  TextColumn get id => text()();
  TextColumn get companyId => text()();
  TextColumn get code => text()();
  TextColumn get name => text().check(name.length.isBetweenValues(1, 160))();
  IntColumn get version => integer().withDefault(const Constant(1))();
  DateTimeColumn get updatedAt => dateTime()();
  TextColumn get originDeviceId => text().nullable()();
  DateTimeColumn get deletedAt => dateTime().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Local mirror of `catalog.products`.
///
/// Money is stored as integer ten-thousandths ("E4") so it matches the
/// server's numeric(18,4) exactly and never touches floating point.
@DataClassName('ProductRow')
class Products extends Table {
  TextColumn get id => text()();
  TextColumn get companyId => text()();
  TextColumn get sku => text()();
  TextColumn get barcode => text().nullable()();
  TextColumn get name => text().check(name.length.isBetweenValues(1, 160))();
  TextColumn get unitId => text().nullable().references(Units, #id)();
  IntColumn get salePriceE4 => integer()
      .withDefault(const Constant(0))
      .check(salePriceE4.isBiggerOrEqualValue(0))();
  IntColumn get costPriceE4 => integer()
      .withDefault(const Constant(0))
      .check(costPriceE4.isBiggerOrEqualValue(0))();
  IntColumn get version => integer().withDefault(const Constant(1))();
  DateTimeColumn get updatedAt => dateTime()();
  TextColumn get originDeviceId => text().nullable()();
  DateTimeColumn get deletedAt => dateTime().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

@DriftDatabase(tables: [Units, Products])
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  static const int currentSchemaVersion = 1;

  @override
  int get schemaVersion => currentSchemaVersion;

  static const List<String> _indexStatements = [
    // Same partial unique indexes as the server: a code or SKU can be reused
    // once the record holding it has been soft-deleted.
    'CREATE UNIQUE INDEX IF NOT EXISTS uq_units_company_code '
        'ON units (company_id, code) WHERE deleted_at IS NULL',
    'CREATE UNIQUE INDEX IF NOT EXISTS uq_products_company_sku '
        'ON products (company_id, sku) WHERE deleted_at IS NULL',
    'CREATE INDEX IF NOT EXISTS ix_products_company_barcode '
        'ON products (company_id, barcode) WHERE barcode IS NOT NULL',
    'CREATE INDEX IF NOT EXISTS ix_products_company_name '
        'ON products (company_id, name)',
    'CREATE INDEX IF NOT EXISTS ix_products_unit ON products (unit_id)',
  ];

  @override
  MigrationStrategy get migration => MigrationStrategy(
    onCreate: (m) async {
      await m.createAll();
      for (final statement in _indexStatements) {
        await customStatement(statement);
      }
    },
    // Forward-only. Future schema versions add a step here per version.
    // Never drop or recreate user tables to "fix" a migration.
    onUpgrade: (m, from, to) async {},
    beforeOpen: (details) async {
      // Must run outside a transaction, which is why it lives here.
      await customStatement('PRAGMA foreign_keys = ON');
    },
  );
}
