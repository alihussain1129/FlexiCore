import 'dart:io';

import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/profile/data/local_profile_repository.dart';
import 'package:flexicore_client/features/profile/domain/business_profile.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqlite3/sqlite3.dart';

/// Builds a genuine schema-version-1 file: the exact tables and indexes the
/// v1 AppDatabase created, user_version = 1, with representative data.
void _buildV1(String path) {
  final raw = sqlite3.open(path);
  raw.execute('''
    CREATE TABLE units (
      id TEXT NOT NULL,
      company_id TEXT NOT NULL,
      code TEXT NOT NULL,
      name TEXT NOT NULL CHECK (LENGTH(name) BETWEEN 1 AND 160),
      version INTEGER NOT NULL DEFAULT 1,
      updated_at INTEGER NOT NULL,
      origin_device_id TEXT NULL,
      deleted_at INTEGER NULL,
      PRIMARY KEY (id)
    )''');
  raw.execute('''
    CREATE TABLE products (
      id TEXT NOT NULL,
      company_id TEXT NOT NULL,
      sku TEXT NOT NULL,
      barcode TEXT NULL,
      name TEXT NOT NULL CHECK (LENGTH(name) BETWEEN 1 AND 160),
      unit_id TEXT NULL REFERENCES units (id),
      sale_price_e4 INTEGER NOT NULL DEFAULT 0 CHECK (sale_price_e4 >= 0),
      cost_price_e4 INTEGER NOT NULL DEFAULT 0 CHECK (cost_price_e4 >= 0),
      version INTEGER NOT NULL DEFAULT 1,
      updated_at INTEGER NOT NULL,
      origin_device_id TEXT NULL,
      deleted_at INTEGER NULL,
      PRIMARY KEY (id)
    )''');
  raw.execute(
    'CREATE UNIQUE INDEX uq_units_company_code ON units (company_id, code) '
    'WHERE deleted_at IS NULL',
  );
  raw.execute(
    'CREATE UNIQUE INDEX uq_products_company_sku ON products '
    '(company_id, sku) WHERE deleted_at IS NULL',
  );
  raw.execute(
    'CREATE INDEX ix_products_company_barcode ON products '
    '(company_id, barcode) WHERE barcode IS NOT NULL',
  );
  raw.execute(
    'CREATE INDEX ix_products_company_name ON products (company_id, name)',
  );
  raw.execute('CREATE INDEX ix_products_unit ON products (unit_id)');

  raw.execute(
    "INSERT INTO units VALUES ('u-pcs','local-company','pcs','Pieces',3,"
    "1700000000,'dev-1',NULL)",
  );
  raw.execute(
    "INSERT INTO units VALUES ('u-old','local-company','box','Box',2,"
    "1700000100,NULL,1700000500)",
  );
  raw.execute(
    "INSERT INTO products VALUES ('p-1','local-company','SKU-1','890123',"
    "'Panadol','u-pcs',125000,90000,4,1700000200,'dev-1',NULL)",
  );
  raw.execute(
    "INSERT INTO products VALUES ('p-2','local-company','SKU-2',NULL,"
    "'Old item','u-old',5000,3000,2,1700000300,NULL,1700000600)",
  );
  raw.execute(
    "INSERT INTO products VALUES ('p-3','other-company','SKU-1',NULL,"
    "'Other co','u-pcs',100,50,1,1700000400,NULL,NULL)",
  );
  raw.execute('PRAGMA user_version = 1');
  raw.close();
}

void main() {
  late Directory dir;
  late File file;

  setUp(() {
    dir = Directory.systemTemp.createTempSync('fc_mig_');
    file = File('${dir.path}/flexicore_local.sqlite');
    _buildV1(file.path);
  });
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  DateTime ts(int s) => DateTime.fromMillisecondsSinceEpoch(s * 1000);

  test('v1 -> v2 keeps every unit and product exactly as it was', () async {
    final db = await openAppDatabase(directory: dir);

    final version = await db.customSelect('PRAGMA user_version').getSingle();
    expect(version.read<int>('user_version'), 2);

    final units = await db.select(db.units).get();
    expect(units.map((u) => u.id).toSet(), {'u-pcs', 'u-old'});
    final pcs = units.firstWhere((u) => u.id == 'u-pcs');
    expect(pcs.companyId, 'local-company');
    expect(pcs.version, 3);
    expect(pcs.deletedAt, isNull);
    expect(units.firstWhere((u) => u.id == 'u-old').deletedAt, ts(1700000500));

    final products = await db.select(db.products).get();
    expect(products, hasLength(3));
    final p1 = products.firstWhere((p) => p.id == 'p-1');
    expect(p1.companyId, 'local-company');
    expect(p1.unitId, 'u-pcs');
    expect(p1.salePriceE4, 125000);
    expect(p1.costPriceE4, 90000);
    expect(p1.barcode, '890123');
    expect(p1.deletedAt, isNull);
    final p2 = products.firstWhere((p) => p.id == 'p-2');
    expect(p2.unitId, 'u-old');
    expect(p2.deletedAt, ts(1700000600));
    expect(products.firstWhere((p) => p.id == 'p-3').companyId, 'other-company');

    await db.close();
  });

  test('indexes survive and the new table exists and is empty', () async {
    final db = await openAppDatabase(directory: dir);
    final names = (await db.customSelect('SELECT name FROM sqlite_master').get())
        .map((r) => r.read<String>('name'))
        .toSet();
    expect(
      names,
      containsAll([
        'business_profiles',
        'uq_units_company_code',
        'uq_products_company_sku',
        'ix_products_company_barcode',
        'ix_products_company_name',
        'ix_products_unit',
      ]),
    );
    expect(await db.select(db.businessProfiles).get(), isEmpty);
    await db.close();
  });

  test('profile can be created on a migrated database', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    await repo.create(
      const ProfileInput(
        businessName: 'Noor Pharmacy',
        industry: 'Pharmacy',
        currency: 'PKR',
        ownerName: 'Ali Hussain',
      ),
    );
    expect(await db.select(db.products).get(), hasLength(3));
    await db.close();

    final reopened = await openAppDatabase(directory: dir);
    expect(
      (await LocalProfileRepository(reopened, companyId: 'local-company').load())
          ?.businessName,
      'Noor Pharmacy',
    );
    await reopened.close();
  });

  test('currentSchemaVersion is 2', () {
    expect(AppDatabase.currentSchemaVersion, 2);
  });
}