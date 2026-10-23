import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/domain/inventory_domain.dart';
import 'package:flexicore_client/features/inventory/presentation/stock_in_screen.dart';
import 'package:flexicore_client/features/inventory/presentation/stock_ledger_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'local-company';

Future<void> _seed(AppDatabase db, String id, String name) async {
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: id,
          companyId: _company,
          sku: 'SKU-$id',
          name: name,
          updatedAt: DateTime(2026, 10, 21),
        ),
      );
}

Future<void> _receive(
  LocalInventoryRepository repo,
  Warehouse wh,
  String productId,
  int qtyE4,
) => repo.record(
  MovementRequest(
    productId: productId,
    warehouseId: wh.id,
    type: StockMovementType.receipt,
    quantityE4: qtyE4,
    requestId: generateUuidV4(),
    actor: 'Tester',
  ),
);

void main() {
  testWidgets('ledger shows balances and movements and reverses a movement', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(1000, 1400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final db = AppDatabase(NativeDatabase.memory());
    addTearDown(db.close);
    await _seed(db, 'led1', 'Ledger Product');
    final repo = LocalInventoryRepository(db, companyId: _company);
    final wh = await repo.ensureDefaultWarehouse();
    await _receive(repo, wh, 'led1', 100000);

    var changed = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: StockLedgerScreen(
          db: db,
          companyId: _company,
          warehouse: wh,
          onChanged: () => changed++,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Ledger Product'), findsWidgets);
    expect(find.textContaining('Received 10'), findsOneWidget);

    await tester.tap(find.text('Movements'));
    await tester.pumpAndSettle();
    expect(find.byTooltip('Reverse this movement'), findsOneWidget);

    await tester.tap(find.byTooltip('Reverse this movement'));
    await tester.pumpAndSettle();
    expect(find.text('Reverse this movement?'), findsOneWidget);
    await tester.tap(find.widgetWithText(FilledButton, 'Reverse'));
    await tester.pumpAndSettle();

    expect(find.text('Movement reversed.'), findsOneWidget);
    expect(changed, 1);
    expect(find.byTooltip('Reverse this movement'), findsNothing);
    final line = (await repo.stockLines(
      warehouseId: wh.id,
    )).singleWhere((l) => l.productId == 'led1');
    expect(line.onHandE4, 0);
    expect(await repo.reconcile(), isEmpty);
  });

  testWidgets('adjustment needs a reason and can decrease stock', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(1000, 1400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final db = AppDatabase(NativeDatabase.memory());
    addTearDown(db.close);
    await _seed(db, 'adj1', 'Adjust Product');
    final repo = LocalInventoryRepository(db, companyId: _company);
    final wh = await repo.ensureDefaultWarehouse();
    await _receive(repo, wh, 'adj1', 100000);

    await tester.pumpWidget(
      MaterialApp(home: StockInScreen(db: db, companyId: _company)),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('On hand: 10'), findsOneWidget);

    await tester.tap(find.byTooltip('Stock actions').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Adjust stock (count correction)'));
    await tester.pumpAndSettle();

    // Counted 6 against 10 on hand: the screen works out a decrease of 4.
    await tester.enterText(
      find.widgetWithText(TextField, 'Counted quantity'),
      '6',
    );
    await tester.tap(find.text('Review Stock'));
    await tester.pumpAndSettle();
    expect(
      find.text('Please give a reason for the adjustment.'),
      findsOneWidget,
    );
    expect(find.text('Confirm and save'), findsNothing);

    await tester.enterText(
      find.widgetWithText(TextField, 'Reason (required)'),
      'Stock count',
    );
    await tester.tap(find.text('Review Stock'));
    await tester.pumpAndSettle();
    expect(find.text('On hand after'), findsOneWidget);
    await tester.tap(find.text('Confirm and save'));
    await tester.pumpAndSettle();

    expect(find.textContaining('On hand: 6'), findsOneWidget);
    final rows = await db
        .customSelect(
          'SELECT movement_type, qty_e4, reason FROM stock_movements '
          "WHERE movement_type = 'adjustment'",
        )
        .get();
    expect(rows, hasLength(1));
    expect(rows.single.read<int>('qty_e4'), -40000);
    expect(rows.single.read<String>('reason'), 'Stock count');
    expect(await repo.reconcile(), isEmpty);
  });

  testWidgets('the warehouse picker adds a warehouse and shows its own stock', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(1000, 1400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final db = AppDatabase(NativeDatabase.memory());
    addTearDown(db.close);
    await _seed(db, 'wh1', 'Warehouse Product');
    final repo = LocalInventoryRepository(db, companyId: _company);
    final wh = await repo.ensureDefaultWarehouse();
    await _receive(repo, wh, 'wh1', 50000);

    await tester.pumpWidget(
      MaterialApp(home: StockInScreen(db: db, companyId: _company)),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('On hand: 5'), findsOneWidget);

    await tester.tap(find.text('Warehouse: Main warehouse'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Add warehouse...'));
    await tester.pumpAndSettle();

    final code = find.widgetWithText(TextField, 'Code (for example SHOP2)');
    final name = find.widgetWithText(TextField, 'Name');
    await tester.enterText(code, 'main');
    await tester.enterText(name, 'Duplicate');
    await tester.tap(find.widgetWithText(FilledButton, 'Add'));
    await tester.pumpAndSettle();
    expect(
      find.text('A warehouse with that code already exists.'),
      findsOneWidget,
    );

    await tester.enterText(code, 'shop2');
    await tester.enterText(name, 'Shop two');
    await tester.tap(find.widgetWithText(FilledButton, 'Add'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Warehouse: Shop two'), findsOneWidget);
    expect(find.textContaining('On hand: 0'), findsOneWidget);
    expect((await repo.warehouses()).length, 2);
  });
  testWidgets('ledger remains usable on a narrow screen', (tester) async {
    await tester.binding.setSurfaceSize(const Size(320, 800));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    final db = AppDatabase(NativeDatabase.memory());
    addTearDown(db.close);
    await _seed(db, 'narrow-ledger', 'A Product With A Very Long Name');
    final repo = LocalInventoryRepository(db, companyId: _company);
    final wh = await repo.ensureDefaultWarehouse();
    await _receive(repo, wh, 'narrow-ledger', 125000);

    await tester.pumpWidget(
      MaterialApp(
        home: StockLedgerScreen(
          db: db,
          companyId: _company,
          warehouse: wh,
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('Balances'), findsOneWidget);
    expect(find.textContaining('On hand'), findsOneWidget);
    expect(tester.takeException(), isNull);

    await tester.tap(find.text('Movements'));
    await tester.pumpAndSettle();

    expect(find.textContaining('A Product With A Very Long Name'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
}