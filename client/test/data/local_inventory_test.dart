import 'dart:io';

import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/domain/inventory_domain.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:sqlite3/sqlite3.dart' as sql;

const _company = 'local-company';

class _Env {
  _Env(this.db, this.repo, this.warehouse);

  final AppDatabase db;
  final LocalInventoryRepository repo;
  final Warehouse warehouse;
  String get productId => 'p1';
}

Future<void> _addProduct(
  AppDatabase db,
  String id, {
  int costE4 = 0,
  bool deleted = false,
}) async {
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: id,
          companyId: _company,
          sku: 'SKU-$id',
          name: 'Product $id',
          updatedAt: DateTime(2026, 10, 21),
          costPriceE4: Value(costE4),
          deletedAt: Value(deleted ? DateTime(2026, 10, 21) : null),
        ),
      );
}

Future<_Env> _env({int costE4 = 0}) async {
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  await _addProduct(db, 'p1', costE4: costE4);
  final repo = LocalInventoryRepository(db, companyId: _company);
  final wh = await repo.ensureDefaultWarehouse();
  return _Env(db, repo, wh);
}

MovementRequest _req(
  _Env e,
  StockMovementType type,
  int quantityE4, {
  String? id,
  int? cost,
  String? reason,
  String? productId,
}) => MovementRequest(
  productId: productId ?? e.productId,
  warehouseId: e.warehouse.id,
  type: type,
  quantityE4: quantityE4,
  requestId: id ?? generateUuidV4(),
  actor: 'Tester',
  unitCostE4: cost,
  reason: reason,
);

Future<int> _onHand(_Env e, {String? productId}) async {
  final lines = await e.repo.stockLines(warehouseId: e.warehouse.id);
  return lines
      .firstWhere((l) => l.productId == (productId ?? e.productId))
      .onHandE4;
}

Future<int> _movementCount(AppDatabase db) async {
  final rows = await db
      .customSelect('SELECT COUNT(*) AS c FROM stock_movements')
      .get();
  return rows.single.read<int>('c');
}

void main() {
  test('quantity parsing and formatting', () {
    expect(parseQtyE4('12'), 120000);
    expect(parseQtyE4('12.5'), 125000);
    expect(parseQtyE4('0.0001'), 1);
    expect(parseQtyE4('1,5'), isNull);
    expect(parseQtyE4('1.23456'), isNull);
    expect(parseQtyE4('-1'), isNull);
    expect(parseQtyE4(''), isNull);
    expect(formatQtyE4(125000), '12.5');
    expect(formatQtyE4(120000), '12');
    expect(formatQtyE4(1234500000), '123,450');
    expect(formatQtyE4(125000, minDecimals: 2), '12.50');
    expect(formatQtyE4(-5000), '-0.5');
  });

  test('schema is v5 with inventory and sales tables', () async {
    final e = await _env();
    Future<List<String>> cols(String table) async {
      final rows = await e.db.customSelect('PRAGMA table_info($table)').get();
      return [for (final r in rows) r.read<String>('name')];
    }

    expect(AppDatabase.currentSchemaVersion, 5);
    expect(
      await cols('stock_movements'),
      containsAll([
        'qty_e4',
        'unit_cost_e4',
        'client_request_id',
        'reverses_movement_id',
        'source_type',
        'origin_device_id',
      ]),
    );
    expect(
      await cols('stock_balances'),
      containsAll(['qty_e4', 'warehouse_id']),
    );
    expect(await cols('warehouses'), containsAll(['is_default', 'deleted_at']));
    final v = await e.db.customSelect('PRAGMA user_version').getSingle();
    expect(v.read<int>('user_version'), 5);
  });

  test('the default warehouse is created once', () async {
    final e = await _env();
    final again = await e.repo.ensureDefaultWarehouse();
    expect(again.id, e.warehouse.id);
    expect((await e.repo.warehouses()).length, 1);
  });

  test('receive and issue change the balance and write the ledger', () async {
    final e = await _env();
    await e.repo.record(
      _req(e, StockMovementType.receipt, 100000, cost: 20000),
    );
    expect(await _onHand(e), 100000);
    await e.repo.record(_req(e, StockMovementType.issue, 30000));
    expect(await _onHand(e), 70000);
    expect(await _movementCount(e.db), 2);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('balance equals the ledger sum after a mixed sequence', () async {
    final e = await _env();
    await _addProduct(e.db, 'p2');
    await e.repo.record(_req(e, StockMovementType.opening, 50000));
    await e.repo.record(_req(e, StockMovementType.receipt, 25000, cost: 10000));
    await e.repo.record(_req(e, StockMovementType.issue, 15000));
    await e.repo.record(
      _req(e, StockMovementType.adjustment, -5000, reason: 'Damaged'),
    );
    await e.repo.record(
      _req(e, StockMovementType.receipt, 70000, productId: 'p2'),
    );
    expect(await _onHand(e), 55000);
    expect(await _onHand(e, productId: 'p2'), 70000);
    final sums = await e.db
        .customSelect(
          'SELECT product_id, SUM(qty_e4) AS s FROM stock_movements '
          'GROUP BY product_id',
        )
        .get();
    final byProduct = {
      for (final r in sums) r.read<String>('product_id'): r.read<int>('s'),
    };
    expect(byProduct['p1'], 55000);
    expect(byProduct['p2'], 70000);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('an issue larger than on hand is refused and changes nothing', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 50000));
    await expectLater(
      e.repo.record(_req(e, StockMovementType.issue, 60000)),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e), 50000);
    expect(await _movementCount(e.db), 1);
  });

  test(
    'a failure after the ledger insert rolls the whole write back',
    () async {
      final e = await _env();
      await e.db.customStatement(
        'CREATE TRIGGER t_fail BEFORE INSERT ON stock_balances '
        "BEGIN SELECT RAISE(ABORT, 'boom'); END",
      );
      await expectLater(
        e.repo.record(_req(e, StockMovementType.receipt, 10000)),
        throwsA(anything),
      );
      expect(await _movementCount(e.db), 0);
      expect(await _onHand(e), 0);
    },
  );

  test('the same request id is applied once', () async {
    final e = await _env();
    final r = _req(e, StockMovementType.receipt, 10000, id: 'same-request');
    await e.repo.record(r);
    await e.repo.record(r);
    expect(await _onHand(e), 10000);
    expect(await _movementCount(e.db), 1);
  });

  test('a request id reused for a different change is refused', () async {
    final e = await _env();
    await e.repo.record(
      _req(e, StockMovementType.receipt, 10000, id: 'same-request'),
    );
    await expectLater(
      e.repo.record(
        _req(e, StockMovementType.receipt, 20000, id: 'same-request'),
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e), 10000);
  });

  test(
    'rules: zero, negative, too large and adjustment without reason',
    () async {
      final e = await _env();
      for (final bad in [
        _req(e, StockMovementType.receipt, 0),
        _req(e, StockMovementType.receipt, -10000),
        _req(e, StockMovementType.receipt, kMaxQuantityE4 + 1),
        _req(e, StockMovementType.adjustment, 10000),
        _req(e, StockMovementType.issue, 10000, cost: 5000),
      ]) {
        await expectLater(
          e.repo.record(bad),
          throwsA(isA<InventoryException>()),
        );
      }
      expect(await _movementCount(e.db), 0);
    },
  );

  test('an adjustment cannot take stock below zero', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 20000));
    await e.repo.record(
      _req(e, StockMovementType.adjustment, -5000, reason: 'Damaged'),
    );
    expect(await _onHand(e), 15000);
    await expectLater(
      e.repo.record(
        _req(e, StockMovementType.adjustment, -99000, reason: 'Too much'),
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e), 15000);
  });

  test('a movement can be reversed once and a reversal cannot', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 40000));
    final first = (await e.repo.movementsForProduct(e.productId)).single;
    expect(first.canReverse, isTrue);
    await e.repo.reverse(
      movementId: first.id,
      requestId: 'rev-1',
      actor: 'Tester',
    );
    expect(await _onHand(e), 0);
    await expectLater(
      e.repo.reverse(movementId: first.id, requestId: 'rev-2', actor: 'Tester'),
      throwsA(isA<InventoryException>()),
    );
    final all = await e.repo.movementsForProduct(e.productId);
    final reversal = all.firstWhere(
      (m) => m.type == StockMovementType.reversal,
    );
    await expectLater(
      e.repo.reverse(
        movementId: reversal.id,
        requestId: 'rev-3',
        actor: 'Tester',
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(all.firstWhere((m) => m.id == first.id).isReversed, isTrue);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('reversing a receipt that was already issued is refused', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 10000));
    await e.repo.record(_req(e, StockMovementType.issue, 10000));
    final receipt = (await e.repo.movementsForProduct(e.productId))
        .firstWhere((m) => m.type == StockMovementType.receipt);
    await expectLater(
      e.repo.reverse(
        movementId: receipt.id,
        requestId: 'rev-x',
        actor: 'Tester',
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e), 0);
  });

  test('the ledger is append-only and balances cannot go negative', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 10000));
    await expectLater(
      e.db.customStatement("UPDATE stock_movements SET reason = 'x'"),
      throwsA(anything),
    );
    await expectLater(
      e.db.customStatement('DELETE FROM stock_movements'),
      throwsA(anything),
    );
    await expectLater(
      e.db.customStatement('UPDATE stock_balances SET qty_e4 = -1'),
      throwsA(anything),
    );
    expect(await _movementCount(e.db), 1);
  });

  test('reconcile reports a tampered balance', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 10000));
    await e.db.customStatement(
      'UPDATE stock_balances SET qty_e4 = qty_e4 + 10000',
    );
    expect(await e.repo.reconcile(), isNotEmpty);
  });

  test('deleted products and unknown warehouses are rejected', () async {
    final e = await _env();
    await _addProduct(e.db, 'gone', deleted: true);
    await expectLater(
      e.repo.record(
        _req(e, StockMovementType.receipt, 10000, productId: 'gone'),
      ),
      throwsA(isA<InventoryException>()),
    );
    await expectLater(
      e.repo.record(
        MovementRequest(
          productId: e.productId,
          warehouseId: 'nope',
          type: StockMovementType.receipt,
          quantityE4: 10000,
          requestId: generateUuidV4(),
          actor: 'Tester',
        ),
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _movementCount(e.db), 0);
  });

  test('stock value is quantity times product cost price', () async {
    final e = await _env(costE4: 25000);
    await e.repo.record(_req(e, StockMovementType.receipt, 100000));
    final s = await e.repo.summary();
    expect(s.stockedCount, 1);
    expect(s.outOfStockCount, 0);
    expect(formatE4Big(s.valueE4, minDecimals: 2), '25.00');
    expect(s.movementCount, 1);
  });

  test(
    'search matches name and sku, and wildcards are not wildcards',
    () async {
      final e = await _env();
      Future<int> count(String q) async => (await e.repo.stockLines(
        warehouseId: e.warehouse.id,
        search: q,
      )).length;
      expect(await count('product p'), 1);
      expect(await count('sku-p1'), 1);
      expect(await count('zzz'), 0);
      expect(await count('%'), 0);
      expect(await count('_'), 0);
    },
  );

  test(
    'upgrading a v2 database keeps its data and makes a safety copy first',
    () async {
      final dir = Directory.systemTemp.createTempSync('fc_upgrade_');
      addTearDown(() {
        try {
          dir.deleteSync(recursive: true);
        } catch (_) {}
      });

      var db = await openAppDatabase(directory: dir);
      await _addProduct(db, 'old1', costE4: 12000);
      await db.close();

      // Make the file look like a v2 database.
      final live = File(
        '${dir.path}${Platform.pathSeparator}flexicore_local.sqlite',
      );
      final raw = sql.sqlite3.open(live.path);
      raw.execute('DROP TABLE stock_balances');
      raw.execute('DROP TABLE stock_movements');
      raw.execute('DROP TABLE warehouses');
      raw.execute('PRAGMA user_version = 2');
      raw.close();

      db = await openAppDatabase(directory: dir);
      addTearDown(db.close);

      final copies = dir
          .listSync()
          .whereType<File>()
          .where((f) => f.path.contains('.pre-v5-'))
          .where((f) => !f.path.endsWith('-wal') && !f.path.endsWith('-shm'))
          .toList();
      expect(copies.length, 1);
      final copy = sql.sqlite3.open(
        copies.single.path,
        mode: sql.OpenMode.readOnly,
      );
      expect(copy.select('PRAGMA user_version').first.values.first, 2);
      expect(
        copy
            .select("SELECT COUNT(*) FROM products WHERE id = 'old1'")
            .first
            .values
            .first,
        1,
      );
      copy.close();

      final v = await db.customSelect('PRAGMA user_version').getSingle();
      expect(v.read<int>('user_version'), 5);
      final old = await db
          .customSelect("SELECT name FROM products WHERE id = 'old1'")
          .get();
      expect(old.length, 1);

      final repo = LocalInventoryRepository(db, companyId: _company);
      final wh = await repo.ensureDefaultWarehouse();
      await repo.record(
        MovementRequest(
          productId: 'old1',
          warehouseId: wh.id,
          type: StockMovementType.receipt,
          quantityE4: 50000,
          requestId: generateUuidV4(),
          actor: 'Tester',
        ),
      );
      expect(
        (await repo.stockLines(warehouseId: wh.id)).single.onHandE4,
        50000,
      );
      await expectLater(
        db.customStatement("UPDATE stock_movements SET reason = 'x'"),
        throwsA(anything),
      );
    },
  );

  test(
    'pre-existing safety copy is preserved during an upgrade backup',
    () async {
      final dir = Directory.systemTemp.createTempSync('fc_existing_copy_');
      addTearDown(() {
        try {
          dir.deleteSync(recursive: true);
        } catch (_) {}
      });

      var db = await openAppDatabase(directory: dir);
      await _addProduct(db, 'old1');
      await db.close();

      final live = File(
        '${dir.path}${Platform.pathSeparator}flexicore_local.sqlite',
      );

      final raw = sql.sqlite3.open(live.path);
      raw.execute('DROP TABLE stock_balances');
      raw.execute('DROP TABLE stock_movements');
      raw.execute('DROP TABLE warehouses');
      raw.execute('PRAGMA user_version = 2');
      raw.close();

      final existing = File('${live.path}.pre-v3-20261021-120000');
      existing.writeAsStringSync('preserve this existing file');

      final backup = await copyBeforeUpgrade(
        live,
        clock: () => DateTime(2026, 10, 21, 12, 0, 0),
      );

      expect(existing.readAsStringSync(), 'preserve this existing file');
      expect(backup, isNotNull);
      expect(backup!.path, isNot(existing.path));

      final copiedDb = sql.sqlite3.open(
        backup.path,
        mode: sql.OpenMode.readOnly,
      );
      try {
        expect(copiedDb.select('PRAGMA user_version').first.values.first, 2);
        expect(
          copiedDb.select('PRAGMA integrity_check').first.values.first,
          'ok',
        );
        expect(
          copiedDb
              .select("SELECT COUNT(*) FROM products WHERE id = 'old1'")
              .first
              .values
              .first,
          1,
        );
      } finally {
        copiedDb.close();
      }
    },
  );
  test('opening a current database makes no safety copy', () async {
    final dir = Directory.systemTemp.createTempSync('fc_nocopy_');
    addTearDown(() {
      try {
        dir.deleteSync(recursive: true);
      } catch (_) {}
    });
    final db = await openAppDatabase(directory: dir);
    await db.close();
    final db2 = await openAppDatabase(directory: dir);
    addTearDown(db2.close);
    final copies = dir.listSync().whereType<File>().where(
      (f) => f.path.contains('.pre-v'),
    );
    expect(copies, isEmpty);
  });
}
