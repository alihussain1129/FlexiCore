import 'dart:io';

import 'package:drift/drift.dart' show Value;
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_backup.dart';
import 'package:flexicore_client/data/local/database_integrity.dart';
import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/profile/data/local_profile_repository.dart';
import 'package:flexicore_client/features/profile/domain/business_profile.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqlite3/sqlite3.dart' as sql;

Future<void> _seed(AppDatabase db) async {
  await db
      .into(db.units)
      .insert(
        UnitsCompanion.insert(
          id: 'u1',
          companyId: 'local-company',
          code: 'pcs',
          name: 'Pieces',
          updatedAt: DateTime(2026, 10, 21),
        ),
      );
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: 'p1',
          companyId: 'local-company',
          sku: 'SKU-1',
          name: 'Panadol',
          updatedAt: DateTime(2026, 10, 21),
          unitId: const Value('u1'),
          salePriceE4: const Value(125000),
        ),
      );
}

Future<void> _addSecondProduct(AppDatabase db) => db
    .into(db.products)
    .insert(
      ProductsCompanion.insert(
        id: 'p2',
        companyId: 'local-company',
        sku: 'SKU-2',
        name: 'Added after backup',
        updatedAt: DateTime(2026, 10, 22),
      ),
    );

int _count(String path, String table) {
  final raw = sql.sqlite3.open(path, mode: sql.OpenMode.readOnly);
  try {
    return raw.select('SELECT COUNT(*) c FROM $table').first['c'] as int;
  } finally {
    raw.close();
  }
}

const _profile = ProfileInput(
  businessName: 'Noor Pharmacy',
  industry: 'Pharmacy',
  currency: 'PKR',
  ownerName: 'Ali Hussain',
);

void main() {
  late Directory dir;

  setUp(() => dir = Directory.systemTemp.createTempSync('fc_backup_'));
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  test('integrity check passes on a healthy database', () async {
    final db = await openAppDatabase(directory: dir);
    await _seed(db);
    final report = await checkDatabaseIntegrity(db);
    expect(report.isOk, isTrue, reason: report.problems.join('; '));
    await db.close();
  });

  test('backup is valid and does not change the live database', () async {
    final db = await openAppDatabase(directory: dir);
    await _seed(db);
    final backup = await createBackup(
      db,
      File('${dir.path}/backups/b1.sqlite'),
    );
    expect(backup.existsSync(), isTrue);
    expect(validateBackupFile(backup).schemaVersion, 2);
    expect(File('${backup.path}.partial').existsSync(), isFalse);
    expect(await db.select(db.products).get(), hasLength(1));
    await db.close();
  });

  test('backup then restore returns to the backed-up state', () async {
    var db = await openAppDatabase(directory: dir);
    await _seed(db);
    await LocalProfileRepository(db, companyId: 'local-company').create(_profile);
    final backup = await createBackup(
      db,
      File('${dir.path}/backups/b1.sqlite'),
    );
    await _addSecondProduct(db);
    await db.close();

    final result = await restoreBackup(
      backup: backup,
      directory: dir,
      clock: () => DateTime(2026, 10, 22, 12),
    );
    expect(result.schemaVersion, 2);
    expect(result.safetyCopy, isNotNull);
    expect(result.safetyCopy!.existsSync(), isTrue);
    expect(_count(result.safetyCopy!.path, 'products'), 2);

    db = await openAppDatabase(directory: dir);
    final products = await db.select(db.products).get();
    expect(products.map((p) => p.id).toList(), ['p1']);
    expect(products.single.salePriceE4, 125000);
    expect(products.single.unitId, 'u1');
    final loaded = await LocalProfileRepository(
      db,
      companyId: 'local-company',
    ).load();
    expect(loaded?.businessName, 'Noor Pharmacy');
    expect((await checkDatabaseIntegrity(db)).isOk, isTrue);
    await db.close();
  });

  test('backup never overwrites an existing file', () async {
    final db = await openAppDatabase(directory: dir);
    final target = File('${dir.path}/existing.sqlite')
      ..writeAsStringSync('keep me');
    await expectLater(createBackup(db, target), throwsA(isA<BackupException>()));
    expect(target.readAsStringSync(), 'keep me');
    await db.close();
  });

  test('corrupt backup is rejected and the live database is untouched', () async {
    final db = await openAppDatabase(directory: dir);
    await _seed(db);
    await db.close();
    final live = File('${dir.path}/flexicore_local.sqlite');
    final before = live.readAsBytesSync();

    final bad = File('${dir.path}/bad.sqlite')
      ..writeAsBytesSync(List<int>.generate(4096, (i) => i % 251));
    await expectLater(
      restoreBackup(backup: bad, directory: dir),
      throwsA(isA<BackupException>()),
    );

    expect(live.readAsBytesSync(), before);
    expect(File('${live.path}.restoring').existsSync(), isFalse);
    final names = dir.listSync().map((e) => e.path).join('\n');
    expect(names.contains('pre-restore'), isFalse);
  });

  test('a backup from a newer schema is rejected', () {
    final f = File('${dir.path}/future.sqlite');
    final raw = sql.sqlite3.open(f.path);
    raw.execute('CREATE TABLE units (id TEXT)');
    raw.execute('CREATE TABLE products (id TEXT)');
    raw.execute('PRAGMA user_version = 99');
    raw.close();
    expect(() => validateBackupFile(f), throwsA(isA<BackupException>()));
  });

  test('restoring an older v1 backup upgrades it to the current schema', () async {
    final db0 = await openAppDatabase(directory: dir);
    await _seed(db0);
    await db0.close();

    final v1 = File('${dir.path}/old-v1.sqlite');
    final raw = sql.sqlite3.open(v1.path);
    raw.execute('''
      CREATE TABLE units (
        id TEXT NOT NULL, company_id TEXT NOT NULL, code TEXT NOT NULL,
        name TEXT NOT NULL CHECK (LENGTH(name) BETWEEN 1 AND 160),
        version INTEGER NOT NULL DEFAULT 1, updated_at INTEGER NOT NULL,
        origin_device_id TEXT NULL, deleted_at INTEGER NULL, PRIMARY KEY (id))''');
    raw.execute('''
      CREATE TABLE products (
        id TEXT NOT NULL, company_id TEXT NOT NULL, sku TEXT NOT NULL,
        barcode TEXT NULL,
        name TEXT NOT NULL CHECK (LENGTH(name) BETWEEN 1 AND 160),
        unit_id TEXT NULL REFERENCES units (id),
        sale_price_e4 INTEGER NOT NULL DEFAULT 0,
        cost_price_e4 INTEGER NOT NULL DEFAULT 0,
        version INTEGER NOT NULL DEFAULT 1, updated_at INTEGER NOT NULL,
        origin_device_id TEXT NULL, deleted_at INTEGER NULL, PRIMARY KEY (id))''');
    raw.execute(
      "INSERT INTO units VALUES ('u-old','local-company','box','Box',1,"
      '1700000000,NULL,NULL)',
    );
    raw.execute('PRAGMA user_version = 1');
    raw.close();

    final result = await restoreBackup(backup: v1, directory: dir);
    expect(result.schemaVersion, 2);

    final db = await openAppDatabase(directory: dir);
    final units = await db.select(db.units).get();
    expect(units.map((u) => u.id).toList(), ['u-old']);
    expect(await db.select(db.businessProfiles).get(), isEmpty);
    await db.close();
  });
}