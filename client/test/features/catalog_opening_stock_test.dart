import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/catalog/data/local_catalog_repository.dart';
import 'package:flexicore_client/features/catalog/domain/catalog_domain.dart';
import 'package:flexicore_client/features/catalog/presentation/catalog_controller.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/data/opening_stock_recorder.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'local-company';

class _Env {
  _Env(this.db, this.inventory, this.controller, this.warehouseId);
  final AppDatabase db;
  final LocalInventoryRepository inventory;
  final CatalogController controller;
  final String warehouseId;

  Future<int> onHandE4() async =>
      (await inventory.stockLines(warehouseId: warehouseId)).single.onHandE4;

  Future<int> openingRows() async {
    final row = await db
        .customSelect(
          "SELECT COUNT(*) AS c FROM stock_movements "
          "WHERE movement_type = 'opening'",
        )
        .getSingle();
    return row.read<int>('c');
  }

  Future<String?> record(String productId, String qty, String requestId) =>
      controller.recordOpeningStock(
        productId: productId,
        quantityText: qty,
        requestId: requestId,
      );
}

Future<_Env> _env({bool withRecorder = true}) async {
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  final inventory = LocalInventoryRepository(db, companyId: _company);
  final wh = await inventory.ensureDefaultWarehouse();
  final controller = CatalogController(
    LocalCatalogRepository(db, companyId: _company),
    openingStock: withRecorder ? localOpeningStockRecorder(inventory) : null,
  );
  return _Env(db, inventory, controller, wh.id);
}

Future<CatalogProduct> _newProduct(_Env e, {String sku = 'SKU-OPEN-1'}) async {
  final r = await e.controller.createProduct(
    sku: sku,
    name: 'Opening test product',
    salePrice: '10',
    costPrice: '',
  );
  expect(r.error, isNull);
  expect(r.product, isNotNull);
  return r.product!;
}

void main() {
  test('a new product gets exactly one opening movement', () async {
    final e = await _env();
    final p = await _newProduct(e);
    expect(await e.record(p.id, '12', generateUuidV4()), isNull);
    expect(await e.onHandE4(), 120000);
    expect(await e.openingRows(), 1);
    expect(await e.inventory.reconcile(), isEmpty);
  });

  test('retrying with the same request id never adds stock twice', () async {
    final e = await _env();
    final p = await _newProduct(e);
    final id = generateUuidV4();
    expect(await e.record(p.id, '5', id), isNull);
    await e.record(p.id, '5', id);
    expect(await e.onHandE4(), 50000);
    expect(await e.openingRows(), 1);
  });

  test('a second opening for the same product is refused', () async {
    final e = await _env();
    final p = await _newProduct(e);
    expect(await e.record(p.id, '5', generateUuidV4()), isNull);
    final message = await e.record(p.id, '7', generateUuidV4());
    expect(message, isNotNull);
    expect(message, contains('already'));
    expect(await e.onHandE4(), 50000);
    expect(await e.openingRows(), 1);
  });

  test('invalid quantities are refused and nothing is recorded', () async {
    final e = await _env();
    final p = await _newProduct(e);
    for (final bad in ['0', 'abc', '-1', '1,5', '']) {
      expect(
        await e.record(p.id, bad, generateUuidV4()),
        isNotNull,
        reason: 'quantity "$bad" should be refused',
      );
    }
    expect(await e.onHandE4(), 0);
    expect(await e.openingRows(), 0);
  });

  test('without a recorder the controller offers no opening stock', () async {
    final e = await _env(withRecorder: false);
    expect(e.controller.canRecordOpeningStock, isFalse);
    final p = await _newProduct(e);
    expect(await e.record(p.id, '3', generateUuidV4()), isNotNull);
    expect(await e.openingRows(), 0);
  });

  test('a duplicate SKU returns an error and no product', () async {
    final e = await _env();
    await _newProduct(e);
    final second = await e.controller.createProduct(
      sku: 'SKU-OPEN-1',
      name: 'Another product',
      salePrice: '10',
      costPrice: '',
    );
    expect(second.product, isNull);
    expect(second.error, isNotNull);
  });
}