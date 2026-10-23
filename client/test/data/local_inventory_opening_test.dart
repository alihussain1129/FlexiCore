import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
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

Future<_Env> _env() async {
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  for (final id in ['p1', 'p2']) {
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
  final repo = LocalInventoryRepository(db, companyId: _company);
  return _Env(db, repo, await repo.ensureDefaultWarehouse());
}

MovementRequest _req(
  _Env e,
  StockMovementType type,
  int qty, {
  String? id,
  String product = 'p1',
  String? warehouseId,
  String? reason,
}) => MovementRequest(
  productId: product,
  warehouseId: warehouseId ?? e.wh.id,
  type: type,
  quantityE4: qty,
  requestId: id ?? generateUuidV4(),
  actor: 'Tester',
  reason: reason,
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

void main() {
  test('a second opening for the same product and warehouse is refused', () async {
    final e = await _env();
    await e.repo.record(
      _req(e, StockMovementType.opening, 50000, id: 'open-1'),
    );
    await expectLater(
      e.repo.record(_req(e, StockMovementType.opening, 10000)),
      throwsA(isA<InventoryException>()),
    );
    // A retry of the first opening is still a harmless no-op.
    await e.repo.record(
      _req(e, StockMovementType.opening, 50000, id: 'open-1'),
    );
    // Receipts and adjustments are not affected by the rule.
    await e.repo.record(_req(e, StockMovementType.receipt, 20000));
    await e.repo.record(
      _req(e, StockMovementType.adjustment, -5000, reason: 'Count'),
    );
    expect(await _onHand(e), 65000);
    expect(await _count(e.db), 3);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('another warehouse or another product can have its own opening', () async {
    final e = await _env();
    final w2 = await e.repo.createWarehouse(code: 'W2', name: 'Second');
    await e.repo.record(_req(e, StockMovementType.opening, 50000));
    await e.repo.record(
      _req(e, StockMovementType.opening, 30000, warehouseId: w2.id),
    );
    await e.repo.record(
      _req(e, StockMovementType.opening, 10000, product: 'p2'),
    );
    expect(await _onHand(e), 50000);
    expect(await _onHand(e, warehouseId: w2.id), 30000);
    expect(await _onHand(e, product: 'p2'), 10000);
    expect(await e.repo.reconcile(), isEmpty);
  });

  test('after the opening is reversed a new opening is allowed', () async {
    final e = await _env();
    await e.repo.record(_req(e, StockMovementType.opening, 50000));
    final first = (await e.repo.movementsForProduct('p1')).single;
    await e.repo.reverse(
      movementId: first.id,
      requestId: 'rev-open',
      actor: 'Tester',
      reason: 'Wrong count',
    );
    expect(await _onHand(e), 0);
    await e.repo.record(_req(e, StockMovementType.opening, 70000));
    expect(await _onHand(e), 70000);
    await expectLater(
      e.repo.record(_req(e, StockMovementType.opening, 1000)),
      throwsA(isA<InventoryException>()),
    );
    expect(await e.repo.reconcile(), isEmpty);
  });
}