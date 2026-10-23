import 'package:drift/drift.dart';
import 'package:drift/native.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/domain/inventory_domain.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_repository.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'pos-test-company';

void main() {
  driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;

  late AppDatabase db;
  late LocalInventoryRepository inventory;
  late LocalPosRepository pos;
  late Warehouse warehouse;

  Future<void> addProduct({
    String id = 'p1',
    int salePriceE4 = 25000,
    int costPriceE4 = 10000,
  }) async {
    await db
        .into(db.products)
        .insert(
          ProductsCompanion.insert(
            id: id,
            companyId: _company,
            sku: 'SKU-$id',
            name: 'Product $id',
            updatedAt: DateTime.utc(2026, 10, 22),
            salePriceE4: Value(salePriceE4),
            costPriceE4: Value(costPriceE4),
          ),
        );
  }

  Future<void> receiveStock(String productId, int quantityE4) async {
    await inventory.record(
      MovementRequest(
        productId: productId,
        warehouseId: warehouse.id,
        type: StockMovementType.receipt,
        quantityE4: quantityE4,
        requestId: 'opening-$productId',
        actor: 'POS test',
        unitCostE4: 10000,
      ),
    );
  }

  Future<int> countRows(String table) async {
    final result = await db
        .customSelect('SELECT COUNT(*) AS c FROM $table')
        .getSingle();
    return result.read<int>('c');
  }

  Future<int> onHand(String productId) async {
    final row =
        await (db.select(db.stockBalances)..where(
              (b) =>
                  b.companyId.equals(_company) &
                  b.productId.equals(productId) &
                  b.warehouseId.equals(warehouse.id),
            ))
            .getSingleOrNull();
    return row?.qtyE4 ?? 0;
  }

  PosCheckoutRequest request({
    String requestId = 'checkout-1',
    int quantityE4 = 20000,
    int amountPaidE4 = 50000,
    String paymentMethod = 'cash',
  }) {
    return PosCheckoutRequest(
      clientRequestId: requestId,
      items: [PosCartItem(productId: 'p1', quantityE4: quantityE4)],
      paymentMethod: paymentMethod,
      amountPaidE4: amountPaidE4,
      createdBy: 'POS test',
    );
  }

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    inventory = LocalInventoryRepository(db, companyId: _company);
    warehouse = await inventory.ensureDefaultWarehouse();
    pos = LocalPosRepository(
      db,
      companyId: _company,
      clock: () => DateTime.utc(2026, 10, 22, 12),
    );
    await addProduct();
  });

  tearDown(() async {
    await db.close();
  });

  test('checkout saves sale, line, payment and deducts stock', () async {
    await receiveStock('p1', 100000);

    final receipt = await pos.checkout(request());

    expect(receipt.id, isNotEmpty);
    expect(receipt.receiptNumber, startsWith('FC-'));
    expect(receipt.warehouseId, warehouse.id);
    expect(receipt.subtotalE4, 50000);
    expect(receipt.totalE4, 50000);
    expect(receipt.amountPaidE4, 50000);
    expect(receipt.paymentMethod, 'cash');
    expect(receipt.lines, hasLength(1));
    expect(receipt.lines.single.productId, 'p1');
    expect(receipt.lines.single.sku, 'SKU-p1');
    expect(receipt.lines.single.name, 'Product p1');
    expect(receipt.lines.single.quantityE4, 20000);
    expect(receipt.lines.single.unitPriceE4, 25000);
    expect(receipt.lines.single.lineTotalE4, 50000);

    expect(await countRows('sales'), 1);
    expect(await countRows('sale_lines'), 1);
    expect(await countRows('sale_payments'), 1);
    expect(await countRows('stock_movements'), 2);
    expect(await onHand('p1'), 80000);
    expect(await inventory.reconcile(), isEmpty);
  });

  test('insufficient stock leaves no partial checkout records', () async {
    await receiveStock('p1', 10000);

    await expectLater(pos.checkout(request()), throwsA(isA<PosException>()));

    expect(await countRows('sales'), 0);
    expect(await countRows('sale_lines'), 0);
    expect(await countRows('sale_payments'), 0);
    expect(await countRows('stock_movements'), 1);
    expect(await onHand('p1'), 10000);
  });

  test('retrying the same request returns the original sale once', () async {
    await receiveStock('p1', 100000);

    final first = await pos.checkout(request());
    final retry = await pos.checkout(request());

    expect(retry.id, first.id);
    expect(retry.receiptNumber, first.receiptNumber);
    expect(await countRows('sales'), 1);
    expect(await countRows('sale_lines'), 1);
    expect(await countRows('sale_payments'), 1);
    expect(await countRows('stock_movements'), 2);
    expect(await onHand('p1'), 80000);
  });

  test('reusing a request ID for a different cart is rejected', () async {
    await receiveStock('p1', 100000);
    await pos.checkout(request());

    await expectLater(
      pos.checkout(request(quantityE4: 30000)),
      throwsA(isA<PosException>()),
    );

    expect(await countRows('sales'), 1);
    expect(await countRows('sale_lines'), 1);
    expect(await countRows('sale_payments'), 1);
    expect(await countRows('stock_movements'), 2);
    expect(await onHand('p1'), 80000);
  });

  test('reusing a request ID with a different payment method is rejected', () async {
    await receiveStock('p1', 100000);
    await pos.checkout(request());

    await expectLater(
      pos.checkout(request(paymentMethod: 'card')),
      throwsA(isA<PosException>()),
    );

    expect(await countRows('sales'), 1);
    expect(await countRows('sale_lines'), 1);
    expect(await countRows('sale_payments'), 1);
    expect(await countRows('stock_movements'), 2);
    expect(await onHand('p1'), 80000);
  });

  test('reusing a request ID with a different amount paid is rejected', () async {
    await receiveStock('p1', 100000);
    await pos.checkout(request());

    await expectLater(
      pos.checkout(request(amountPaidE4: 40000)),
      throwsA(isA<PosException>()),
    );

    expect(await countRows('sales'), 1);
    expect(await countRows('sale_lines'), 1);
    expect(await countRows('sale_payments'), 1);
    expect(await countRows('stock_movements'), 2);
    expect(await onHand('p1'), 80000);
  });

  test('payment-write failure rolls back the entire checkout', () async {
    await receiveStock('p1', 100000);

    await db.customStatement(
      'CREATE TRIGGER t_fail_pos_payment '
      'BEFORE INSERT ON sale_payments '
      "BEGIN SELECT RAISE(ABORT, 'intentional POS test failure'); END",
    );

    await expectLater(pos.checkout(request()), throwsA(anything));

    expect(await countRows('sales'), 0);
    expect(await countRows('sale_lines'), 0);
    expect(await countRows('sale_payments'), 0);
    expect(await countRows('stock_movements'), 1);
    expect(await onHand('p1'), 100000);
    expect(await inventory.reconcile(), isEmpty);
  });
}
