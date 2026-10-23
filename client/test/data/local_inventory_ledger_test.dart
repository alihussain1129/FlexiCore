import 'dart:io';

import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/domain/inventory_domain.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'local-company';

class _Env {
  _Env(this.db, this.repo, this.wh);
  final AppDatabase db;
  final LocalInventoryRepository repo;
  final Warehouse wh;
}

Future<void> _addProduct(AppDatabase db, String id) async {
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: id,
          companyId: _company,
          sku: 'SKU-$id',
          name: 'Product $id',
          updatedAt: DateTime(2026, 10, 21),
        ),
      );
}

Future<_Env> _env() async {
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  await _addProduct(db, 'p1');
  await _addProduct(db, 'p2');
  final repo = LocalInventoryRepository(db, companyId: _company);
  final wh = await repo.ensureDefaultWarehouse();
  return _Env(db, repo, wh);
}

MovementRequest _req(
  _Env e,
  StockMovementType type,
  int qty, {
  String? id,
  String? reason,
  String product = 'p1',
  String? warehouseId,
  DateTime? at,
  int? cost,
}) => MovementRequest(
  productId: product,
  warehouseId: warehouseId ?? e.wh.id,
  type: type,
  quantityE4: qty,
  requestId: id ?? generateUuidV4(),
  actor: 'Tester',
  reason: reason,
  occurredAt: at,
  unitCostE4: cost,
);

Future<int> _onHand(_Env e, {String product = 'p1', String? warehouseId}) async {
  final lines = await e.repo.stockLines(warehouseId: warehouseId ?? e.wh.id);
  return lines.firstWhere((l) => l.productId == product).onHandE4;
}

Future<int> _count(AppDatabase db) async {
  final rows = await db
      .customSelect('SELECT COUNT(*) AS c FROM stock_movements')
      .get();
  return rows.single.read<int>('c');
}

Future<bool> _succeeds(Future<void> f) async {
  try {
    await f;
    return true;
  } on InventoryException {
    return false;
  }
}

void main() {
  test('a reused request id for a different reversal is refused', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 10000));
    await e.repo.record(_req(e, StockMovementType.receipt, 20000));
    await e.repo.record(
      _req(e, StockMovementType.receipt, 5000, id: 'rcpt-1'),
    );
    final moves = await e.repo.movementsForProduct('p1');
    final a = moves.firstWhere((m) => m.qtyE4 == 10000);
    final b = moves.firstWhere((m) => m.qtyE4 == 20000);

    await e.repo.reverse(movementId: a.id, requestId: 'rev-1', actor: 'Tester');
    // The same reversal sent again is a harmless retry.
    await e.repo.reverse(movementId: a.id, requestId: 'rev-1', actor: 'Tester');
    // The same id for another movement is a conflict.
    await expectLater(
      e.repo.reverse(movementId: b.id, requestId: 'rev-1', actor: 'Tester'),
      throwsA(isA<InventoryException>()),
    );
    // An id that belongs to a receipt cannot be used for a reversal.
    await expectLater(
      e.repo.reverse(movementId: b.id, requestId: 'rcpt-1', actor: 'Tester'),
      throwsA(isA<InventoryException>()),
    );
    // An id that belongs to a reversal cannot be used for a receipt.
    await expectLater(
      e.repo.record(_req(e, StockMovementType.receipt, 10000, id: 'rev-1')),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e), 25000);
    expect(await _count(e.db), 4);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('dates: future and before 2000 are refused, a past date is kept', () async {
    final e = await _env();
    await expectLater(
      e.repo.record(
        _req(
          e,
          StockMovementType.receipt,
          10000,
          at: DateTime.now().add(const Duration(days: 5)),
        ),
      ),
      throwsA(isA<InventoryException>()),
    );
    await expectLater(
      e.repo.record(
        _req(e, StockMovementType.receipt, 10000, at: DateTime(1999, 12, 31)),
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _count(e.db), 0);

    final past = DateTime(2026, 10, 1, 12);
    await e.repo.record(_req(e, StockMovementType.receipt, 10000, at: past));
    final m = (await e.repo.ledger()).single;
    expect(m.occurredAt.isAtSameMomentAs(past), isTrue);
  });

  test('creating a warehouse cleans the input and refuses bad or duplicate codes', () async {
    final e = await _env();
    final w = await e.repo.createWarehouse(code: ' shop2 ', name: ' Shop two ');
    expect(w.code, 'SHOP2');
    expect(w.name, 'Shop two');
    expect(w.isDefault, isFalse);

    final all = await e.repo.warehouses();
    expect(all.length, 2);
    expect(all.first.id, e.wh.id);

    for (final bad in [
      ('shop2', 'Duplicate'),
      ('', 'No code'),
      ('bad code', 'Space in code'),
      ('A' * 21, 'Long code'),
      ('OK1', ''),
      ('OK2', '   '),
    ]) {
      await expectLater(
        e.repo.createWarehouse(code: bad.$1, name: bad.$2),
        throwsA(isA<InventoryException>()),
        reason: 'code "${bad.$1}" name "${bad.$2}"',
      );
    }
    expect((await e.repo.warehouses()).length, 2);
  });

  test('each warehouse keeps its own balance', () async {
    final e = await _env();
    final w2 = await e.repo.createWarehouse(code: 'W2', name: 'Second');
    await e.repo.record(_req(e, StockMovementType.receipt, 50000));
    await e.repo.record(
      _req(e, StockMovementType.receipt, 30000, warehouseId: w2.id),
    );
    expect(await _onHand(e), 50000);
    expect(await _onHand(e, warehouseId: w2.id), 30000);
    await expectLater(
      e.repo.record(
        _req(e, StockMovementType.issue, 40000, warehouseId: w2.id),
      ),
      throwsA(isA<InventoryException>()),
    );
    expect(await _onHand(e, warehouseId: w2.id), 30000);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('the breakdown adds up to the stored balance', () async {
    final e = await _env();
    final w2 = await e.repo.createWarehouse(code: 'W2', name: 'Second');
    await e.repo.record(_req(e, StockMovementType.opening, 50000));
    await e.repo.record(_req(e, StockMovementType.receipt, 25000));
    await e.repo.record(_req(e, StockMovementType.issue, 15000));
    await e.repo.record(
      _req(e, StockMovementType.adjustment, -5000, reason: 'Count'),
    );
    await e.repo.record(
      _req(e, StockMovementType.receipt, 99000, warehouseId: w2.id),
    );
    final receipt = (await e.repo.movementsForProduct('p1')).firstWhere(
      (m) => m.type == StockMovementType.receipt && m.warehouseName != 'Second',
    );
    await e.repo.reverse(
      movementId: receipt.id,
      requestId: 'rev-b',
      actor: 'Tester',
    );

    final lines = await e.repo.breakdown(warehouseId: e.wh.id);
    expect(lines.length, 1); // p2 has no stock and no movements
    final b = lines.single;
    expect(b.productId, 'p1');
    expect(b.openingE4, 50000);
    expect(b.receivedE4, 25000);
    expect(b.issuedE4, -15000);
    expect(b.otherE4, -30000); // adjustment -5000 and the reversal -25000
    expect(b.onHandE4, 30000);
    expect(b.matchesLedger, isTrue);
    expect((await e.repo.breakdown(warehouseId: w2.id)).single.onHandE4, 99000);
  });

  test('ledger filters by type, date range and warehouse', () async {
    final e = await _env();
    final w2 = await e.repo.createWarehouse(code: 'W2', name: 'Second');
    await e.repo.record(
      _req(e, StockMovementType.receipt, 10000, at: DateTime(2026, 9, 1, 12)),
    );
    await e.repo.record(
      _req(e, StockMovementType.receipt, 20000, at: DateTime(2026, 10, 1, 12)),
    );
    await e.repo.record(
      _req(e, StockMovementType.issue, 5000, at: DateTime(2026, 10, 2, 12)),
    );

    expect((await e.repo.ledger()).length, 3);
    final issues = await e.repo.ledger(type: StockMovementType.issue);
    expect(issues.single.qtyE4, -5000);
    final window = await e.repo.ledger(
      from: DateTime(2026, 9, 15),
      to: DateTime(2026, 10, 2),
    );
    expect(window.single.qtyE4, 20000);
    expect((await e.repo.ledger(warehouseId: w2.id)), isEmpty);
    expect((await e.repo.ledger(warehouseId: e.wh.id)).length, 3);
  });

  test('two issues at the same moment cannot both take the last stock', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.receipt, 100000));
    final results = await Future.wait([
      _succeeds(e.repo.record(_req(e, StockMovementType.issue, 60000))),
      _succeeds(e.repo.record(_req(e, StockMovementType.issue, 60000))),
    ]);
    expect(results.where((r) => r).length, 1);
    expect(await _onHand(e), 40000);
    expect(await _count(e.db), 2);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('the same request sent twice at the same moment is stored once', () async {
    final e = await _env();
    final r = _req(e, StockMovementType.receipt, 10000, id: 'double-tap');
    await Future.wait([e.repo.record(r), e.repo.record(r)]);
    expect(await _onHand(e), 10000);
    expect(await _count(e.db), 1);
  });

  test('after a rolled-back failure the same request id can be retried', () async {
    final e = await _env();
    await e.db.customStatement(
      'CREATE TRIGGER t_fail BEFORE INSERT ON stock_balances '
      "BEGIN SELECT RAISE(ABORT, 'boom'); END",
    );
    final r = _req(e, StockMovementType.receipt, 10000, id: 'retry-me');
    await expectLater(e.repo.record(r), throwsA(anything));
    expect(await _count(e.db), 0);
    await e.db.customStatement('DROP TRIGGER t_fail');
    await e.repo.record(r);
    expect(await _onHand(e), 10000);
    expect(await _count(e.db), 1);
  });

  test('stock survives closing and reopening the database file', () async {
    final dir = Directory.systemTemp.createTempSync('fc_stock_reopen_');
    addTearDown(() {
      try {
        dir.deleteSync(recursive: true);
      } catch (_) {}
    });

    var db = await openAppDatabase(directory: dir);
    await _addProduct(db, 'p1');
    var repo = LocalInventoryRepository(db, companyId: _company);
    final wh = await repo.ensureDefaultWarehouse();
    final opening = MovementRequest(
      productId: 'p1',
      warehouseId: wh.id,
      type: StockMovementType.opening,
      quantityE4: 80000,
      requestId: 'open-1',
      actor: 'Tester',
    );
    await repo.record(opening);
    await repo.record(
      MovementRequest(
        productId: 'p1',
        warehouseId: wh.id,
        type: StockMovementType.issue,
        quantityE4: 30000,
        requestId: 'issue-1',
        actor: 'Tester',
        reason: 'Damaged',
      ),
    );
    await db.close();

    db = await openAppDatabase(directory: dir);
    addTearDown(db.close);
    repo = LocalInventoryRepository(db, companyId: _company);
    final again = await repo.ensureDefaultWarehouse();
    expect(again.id, wh.id);
    expect((await repo.stockLines(warehouseId: wh.id)).single.onHandE4, 50000);
    expect((await repo.ledger()).length, 2);
    expect(await repo.reconcile(), isEmpty);

    // The same request after a restart is still applied only once.
    await repo.record(opening);
    expect((await repo.stockLines(warehouseId: wh.id)).single.onHandE4, 50000);
    expect((await repo.ledger()).length, 2);
  });
}