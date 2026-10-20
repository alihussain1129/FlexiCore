import 'dart:io';

import 'package:drift/drift.dart' show Value, driftRuntimeOptions;
import 'package:drift/native.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/catalog/data/local_catalog_repository.dart';
import 'package:flexicore_client/features/catalog/domain/catalog_domain.dart';
import 'package:flutter_test/flutter_test.dart';

Matcher _catalogError(CatalogError error) =>
    isA<CatalogException>().having((e) => e.error, 'error', error);

void main() {
  // Tests keep an in-memory database open while file-backed ones are opened
  // on different executors; drift's same-executor warning does not apply.
  driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;

  late AppDatabase db;
  late LocalCatalogRepository repo;

  setUp(() {
    db = AppDatabase(NativeDatabase.memory());
    repo = LocalCatalogRepository(db, companyId: 'company-1');
  });

  tearDown(() async => db.close());

  group('database', () {
    test('creates schema at version 1 with foreign keys enabled', () async {
      final version = await db.customSelect('PRAGMA user_version').getSingle();
      expect(
        version.read<int>('user_version'),
        AppDatabase.currentSchemaVersion,
      );

      final fk = await db.customSelect('PRAGMA foreign_keys').getSingle();
      expect(fk.read<int>('foreign_keys'), 1);

      final tables = await db
          .customSelect("SELECT name FROM sqlite_master WHERE type = 'table'")
          .get();
      final names = tables.map((r) => r.read<String>('name')).toSet();
      expect(names, containsAll(<String>['units', 'products']));
    });

    test('foreign key rejects a product pointing at a missing unit', () async {
      await expectLater(
        db
            .into(db.products)
            .insert(
              ProductsCompanion.insert(
                id: 'p1',
                companyId: 'company-1',
                sku: 'SKU1',
                name: 'Widget',
                updatedAt: DateTime.now(),
                unitId: const Value('no-such-unit'),
              ),
            ),
        throwsA(anything),
      );
    });
  });

  group('units', () {
    test('create, list, update, soft delete', () async {
      final kg = await repo.createUnit(code: ' KG ', name: ' Kilogram ');
      expect(kg.code, 'KG');
      expect(kg.name, 'Kilogram');
      expect(kg.version, 1);

      final updated = await repo.updateUnit(
        kg.id,
        code: 'KG',
        name: 'Kilogramme',
        expectedVersion: 1,
      );
      expect(updated.version, 2);
      expect((await repo.listUnits()).single.name, 'Kilogramme');

      await repo.deleteUnit(kg.id, expectedVersion: 2);
      expect(await repo.listUnits(), isEmpty);
    });

    test(
      'rejects invalid codes and duplicates; code reusable after delete',
      () async {
        await expectLater(
          repo.createUnit(code: '-bad', name: 'X'),
          throwsA(_catalogError(CatalogError.validation)),
        );
        final a = await repo.createUnit(code: 'PCS', name: 'Pieces');
        await expectLater(
          repo.createUnit(code: 'PCS', name: 'Again'),
          throwsA(_catalogError(CatalogError.duplicate)),
        );
        await repo.deleteUnit(a.id, expectedVersion: 1);
        final b = await repo.createUnit(code: 'PCS', name: 'Pieces v2');
        expect(b.id, isNot(a.id));
      },
    );

    test('cannot delete a unit used by an active product', () async {
      final unit = await repo.createUnit(code: 'BOX', name: 'Box');
      final product = await repo.createProduct(
        sku: 'A-1',
        name: 'Gadget',
        unitId: unit.id,
      );

      await expectLater(
        repo.deleteUnit(unit.id, expectedVersion: 1),
        throwsA(_catalogError(CatalogError.inUse)),
      );

      await repo.deleteProduct(product.id, expectedVersion: 1);
      await repo.deleteUnit(unit.id, expectedVersion: 1);
      expect(await repo.listUnits(), isEmpty);
    });
  });

  group('products', () {
    test('create, search, update, soft delete', () async {
      final unit = await repo.createUnit(code: 'PCS', name: 'Pieces');
      final p = await repo.createProduct(
        sku: 'W-100',
        name: 'Widget',
        barcode: '123456789',
        unitId: unit.id,
        salePriceE4: 15000,
        costPriceE4: 9000,
      );
      expect(p.salePriceE4, 15000);
      expect(p.unitId, unit.id);

      expect((await repo.listProducts(query: 'widg')).length, 1);
      expect((await repo.listProducts(query: 'zzz')), isEmpty);

      final u = await repo.updateProduct(
        p.id,
        sku: 'W-100',
        name: 'Widget Pro',
        barcode: null,
        unitId: unit.id,
        salePriceE4: 20000,
        costPriceE4: 9000,
        expectedVersion: 1,
      );
      expect(u.version, 2);
      expect(u.barcode, isNull);

      await repo.deleteProduct(p.id, expectedVersion: 2);
      expect(await repo.listProducts(), isEmpty);
    });

    test(
      'rejects duplicate SKU, bad barcode, negative price, missing unit',
      () async {
        await repo.createProduct(sku: 'S1', name: 'One');
        await expectLater(
          repo.createProduct(sku: 'S1', name: 'Two'),
          throwsA(_catalogError(CatalogError.duplicate)),
        );
        await expectLater(
          repo.createProduct(sku: 'S2', name: 'Two', barcode: 'ab'),
          throwsA(_catalogError(CatalogError.validation)),
        );
        await expectLater(
          repo.createProduct(sku: 'S2', name: 'Two', salePriceE4: -1),
          throwsA(_catalogError(CatalogError.validation)),
        );
        await expectLater(
          repo.createProduct(sku: 'S2', name: 'Two', unitId: 'missing'),
          throwsA(_catalogError(CatalogError.notFound)),
        );
        expect((await repo.listProducts()).length, 1);
      },
    );

    test('stale expectedVersion is a conflict and changes nothing', () async {
      final p = await repo.createProduct(sku: 'S1', name: 'One');
      await repo.updateProduct(
        p.id,
        sku: 'S1',
        name: 'One b',
        salePriceE4: 0,
        costPriceE4: 0,
        expectedVersion: 1,
      );
      await expectLater(
        repo.updateProduct(
          p.id,
          sku: 'S1',
          name: 'Stale write',
          salePriceE4: 0,
          costPriceE4: 0,
          expectedVersion: 1,
        ),
        throwsA(_catalogError(CatalogError.conflict)),
      );
      expect((await repo.listProducts()).single.name, 'One b');
    });
  });

  group('transactions', () {
    test('an error inside a transaction rolls back every write', () async {
      await expectLater(
        db.transaction(() async {
          await repo.createUnit(code: 'KG', name: 'Kilogram');
          await repo.createProduct(sku: 'S1', name: 'One');
          throw StateError('boom');
        }),
        throwsStateError,
      );
      expect(await repo.listUnits(), isEmpty);
      expect(await repo.listProducts(), isEmpty);
    });
  });

  group('persistence', () {
    late Directory dir;

    setUp(() async {
      dir = await Directory.systemTemp.createTemp('flexicore_db_test_');
    });

    tearDown(() async {
      if (await dir.exists()) await dir.delete(recursive: true);
    });

    test('data survives closing and reopening the database file', () async {
      final first = await openAppDatabase(directory: dir);
      final r1 = LocalCatalogRepository(first, companyId: 'company-1');
      final unit = await r1.createUnit(code: 'PCS', name: 'Pieces');
      await r1.createProduct(sku: 'P-1', name: 'Persisted', unitId: unit.id);
      await first.close();

      final second = await openAppDatabase(directory: dir);
      addTearDown(second.close);
      final r2 = LocalCatalogRepository(second, companyId: 'company-1');
      final products = await r2.listProducts();
      expect(products.single.name, 'Persisted');
      expect(products.single.unitId, unit.id);
      expect((await r2.listUnits()).single.code, 'PCS');
    });

    test('another company id cannot see this data', () async {
      final database = await openAppDatabase(directory: dir);
      addTearDown(database.close);
      final a = LocalCatalogRepository(database, companyId: 'company-a');
      final b = LocalCatalogRepository(database, companyId: 'company-b');
      await a.createProduct(sku: 'S1', name: 'Only A');
      expect(await b.listProducts(), isEmpty);
    });
  });
}
