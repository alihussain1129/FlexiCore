// ignore_for_file: recursive_getters
import 'package:drift/drift.dart';

import 'inventory_schema.dart';
import 'sales_schema.dart';

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

/// The single local business profile. The CHECK on [id] guarantees at most
/// one row exists, so a second profile can never be inserted by accident.
@DataClassName('BusinessProfileRow')
class BusinessProfiles extends Table {
  TextColumn get id => text().check(id.equals('business-profile'))();
  TextColumn get companyId => text()();
  TextColumn get businessName =>
      text().check(businessName.length.isBetweenValues(1, 160))();
  TextColumn get industry =>
      text().check(industry.length.isBetweenValues(1, 80))();
  TextColumn get currency => text().check(currency.length.equals(3))();
  TextColumn get ownerName =>
      text().check(ownerName.length.isBetweenValues(1, 120))();
  DateTimeColumn get createdAt => dateTime()();
  DateTimeColumn get updatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

/// A place where stock is kept. At most one active warehouse per company is
/// the default (enforced by a partial unique index, see inventory_schema.dart).
@DataClassName('WarehouseRow')
class Warehouses extends Table {
  TextColumn get id => text()();
  TextColumn get companyId => text()();
  TextColumn get code => text().check(code.length.isBetweenValues(1, 20))();
  TextColumn get name => text().check(name.length.isBetweenValues(1, 160))();
  BoolColumn get isDefault =>
      boolean().named('is_default').withDefault(const Constant(false))();
  IntColumn get version => integer().withDefault(const Constant(1))();
  DateTimeColumn get updatedAt => dateTime()();
  TextColumn get originDeviceId => text().nullable()();
  DateTimeColumn get deletedAt => dateTime().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Append-only stock ledger: rows are never updated or deleted (triggers
/// enforce it). A correction is a new reversing row. Quantities are signed
/// integer ten-thousandths: receipts are positive, issues are negative.
@DataClassName('StockMovementRow')
class StockMovements extends Table {
  TextColumn get id => text()();
  TextColumn get companyId => text()();
  TextColumn get productId => text().references(Products, #id)();
  TextColumn get warehouseId => text().references(Warehouses, #id)();
  TextColumn get movementType => text().check(
    movementType.isIn(const [
      'opening',
      'receipt',
      'issue',
      'adjustment',
      'reversal',
    ]),
  )();
  IntColumn get qtyE4 =>
      integer().named('qty_e4').check(qtyE4.equals(0).not())();
  IntColumn get unitCostE4 => integer()
      .named('unit_cost_e4')
      .nullable()
      .check(unitCostE4.isBiggerOrEqualValue(0))();
  TextColumn get reason => text().nullable()();
  TextColumn get sourceType => text().nullable()();
  TextColumn get sourceId => text().nullable()();
  TextColumn get reversesMovementId => text().nullable()();
  TextColumn get clientRequestId =>
      text().check(clientRequestId.length.isBetweenValues(1, 80))();
  DateTimeColumn get occurredAt => dateTime()();
  DateTimeColumn get createdAt => dateTime()();
  TextColumn get createdBy =>
      text().check(createdBy.length.isBetweenValues(1, 120))();
  TextColumn get originDeviceId => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// Current quantity per product and warehouse. A projection of the ledger,
/// updated in the same transaction as every movement. Never negative.
@DataClassName('StockBalanceRow')
class StockBalances extends Table {
  TextColumn get companyId => text()();
  TextColumn get productId => text().references(Products, #id)();
  TextColumn get warehouseId => text().references(Warehouses, #id)();
  IntColumn get qtyE4 =>
      integer().named('qty_e4').check(qtyE4.isBiggerOrEqualValue(0))();
  DateTimeColumn get updatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {companyId, productId, warehouseId};
}

@DataClassName('SaleRow')
class Sales extends Table {
  TextColumn get id => text()();
  TextColumn get companyId => text()();
  TextColumn get receiptNumber => text()();
  TextColumn get clientRequestId => text()();
  TextColumn get warehouseId => text().references(Warehouses, #id)();
  IntColumn get subtotalE4 => integer()();
  IntColumn get discountE4 => integer().withDefault(const Constant(0))();
  IntColumn get taxE4 => integer().withDefault(const Constant(0))();
  IntColumn get totalE4 => integer()();
  IntColumn get amountPaidE4 => integer()();
  TextColumn get status => text().withDefault(const Constant('completed'))();
  IntColumn get changeE4 => integer().withDefault(const Constant(0))();
  IntColumn get taxRateBp => integer().withDefault(const Constant(0))();
  TextColumn get discountReason => text().nullable()();
  TextColumn get paymentMethod => text()();
  DateTimeColumn get createdAt => dateTime()();
  TextColumn get createdBy => text()();
  TextColumn get originDeviceId => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

@DataClassName('SaleLineRow')
class SaleLines extends Table {
  TextColumn get id => text()();
  TextColumn get saleId => text().references(Sales, #id)();
  TextColumn get productId => text().references(Products, #id)();
  TextColumn get skuSnapshot => text()();
  TextColumn get nameSnapshot => text()();
  IntColumn get quantityE4 => integer()();
  IntColumn get unitPriceE4 => integer()();
  IntColumn get discountE4 => integer().withDefault(const Constant(0))();
  IntColumn get lineTotalE4 => integer()();
  IntColumn get taxE4 => integer().withDefault(const Constant(0))();

  @override
  Set<Column> get primaryKey => {id};
}

@DataClassName('SalePaymentRow')
class SalePayments extends Table {
  TextColumn get id => text()();
  TextColumn get saleId => text().references(Sales, #id)();
  IntColumn get amountE4 => integer()();
  TextColumn get method => text()();
  DateTimeColumn get receivedAt => dateTime()();
  TextColumn get reference => text().nullable()();

  @override
  Set<Column> get primaryKey => {id};
}

/// POS settings. One row per local database (same pattern as the business
/// profile). Rates are basis points: 1500 is 15%.
@DataClassName('PosSettingsRow')
class PosSettings extends Table {
  TextColumn get id => text().check(id.equals('pos-settings'))();
  TextColumn get companyId => text()();
  IntColumn get taxRateBp => integer()
      .withDefault(const Constant(0))
      .check(taxRateBp.isBetweenValues(0, 10000))();
  IntColumn get maxDiscountBp => integer()
      .withDefault(const Constant(10000))
      .check(maxDiscountBp.isBetweenValues(0, 10000))();
  TextColumn get receiptFooter => text().nullable()();
  DateTimeColumn get updatedAt => dateTime()();

  @override
  Set<Column> get primaryKey => {id};
}

@DriftDatabase(
  tables: [
    Units,
    Products,
    BusinessProfiles,
    Warehouses,
    StockMovements,
    StockBalances,
    Sales,
    SaleLines,
    SalePayments,
    PosSettings,
  ],
)
class AppDatabase extends _$AppDatabase {
  AppDatabase(super.executor);

  static const int currentSchemaVersion = 5;

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
      for (final statement in kInventorySchemaStatements) {
        await customStatement(statement);
      }
      for (final statement in kSalesSchemaStatements) {
        await customStatement(statement);
      }
      for (final statement in kSalesIntegrityStatements) {
        await customStatement(statement);
      }
    },
    // Forward-only. Future schema versions add a step here per version.
    // Never drop or recreate user tables to "fix" a migration.
    onUpgrade: (m, from, to) async {
      // v1 -> v2: adds the business profile. Existing tables are untouched.
      if (from < 2) {
        await m.createTable(businessProfiles);
      }

      // v2 -> v3: inventory. Existing tables are untouched.
      if (from < 3) {
        await m.createTable(warehouses);
        await m.createTable(stockMovements);
        await m.createTable(stockBalances);
        for (final statement in kInventorySchemaStatements) {
          await customStatement(statement);
        }
      }

      // v3 -> v4: add sales without dropping or rewriting existing data.
      if (from < 4) {
        await m.createTable(sales);
        await m.createTable(saleLines);
        await m.createTable(salePayments);
        for (final statement in kSalesSchemaStatements) {
          await customStatement(statement);
        }
      }

      // v4 -> v5: tax, discount detail, change, sale status, POS settings and
      // integrity triggers. Existing sales keep their numbers; new columns get
      // safe defaults.
      if (from < 5) {
        // A database older than v4 just had sales created from the current
        // definition (it already has these columns), so only v4 needs them added.
        if (from >= 4) {
          await m.addColumn(sales, sales.status);
          await m.addColumn(sales, sales.changeE4);
          await m.addColumn(sales, sales.taxRateBp);
          await m.addColumn(sales, sales.discountReason);
          await m.addColumn(saleLines, saleLines.taxE4);
        }
        await m.createTable(posSettings);
        for (final statement in kSalesIntegrityStatements) {
          await customStatement(statement);
        }
      }
    },
    beforeOpen: (details) async {
      // Must run outside a transaction, which is why it lives here.
      await customStatement('PRAGMA foreign_keys = ON');
    },
  );
}
