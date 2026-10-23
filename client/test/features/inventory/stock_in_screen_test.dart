import 'package:drift/drift.dart' show Value, Variable;
import 'package:drift/native.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/presentation/stock_in_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets(
    'receiving stock reviews quantity, saves ledger and refreshes screen',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1000, 1400));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final db = AppDatabase(NativeDatabase.memory());
      addTearDown(db.close);

      await db
          .into(db.products)
          .insert(
            ProductsCompanion.insert(
              id: 'stock-widget-product',
              companyId: 'local-company',
              sku: 'SKU-STOCK-1',
              name: 'Widget Test Product',
              updatedAt: DateTime(2026, 10, 21),
              costPriceE4: const Value(0),
              deletedAt: const Value(null),
            ),
          );

      var changedCalls = 0;

      await tester.pumpWidget(
        MaterialApp(
          home: StockInScreen(
            db: db,
            companyId: 'local-company',
            onChanged: () => changedCalls++,
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('Stock'), findsOneWidget);
      expect(find.text('Widget Test Product'), findsOneWidget);
      expect(find.textContaining('On hand: 0'), findsOneWidget);

      // Tapping the product row opens the Receive stock form.
      await tester.tap(find.text('Widget Test Product'));
      await tester.pumpAndSettle();

      expect(find.text('Receive stock'), findsOneWidget);
      expect(find.textContaining('On hand now: 0 in '), findsOneWidget);

      await tester.enterText(find.widgetWithText(TextField, 'Quantity'), '12');

      await tester.tap(find.text('Review Stock'));
      await tester.pumpAndSettle();

      expect(find.text('On hand after'), findsOneWidget);
      expect(find.text('12'), findsWidgets);
      expect(find.text('Confirm and save'), findsOneWidget);

      await tester.tap(find.text('Confirm and save'));
      await tester.pumpAndSettle();

      // The form closes, the screen reloads, and the callback fires.
      expect(find.text('Receive stock'), findsNothing);
      expect(changedCalls, 1);
      expect(find.textContaining('On hand: 12'), findsOneWidget);
      expect(find.text('Recent stock movements'), findsOneWidget);

      final repo = LocalInventoryRepository(db, companyId: 'local-company');
      final warehouse = await repo.ensureDefaultWarehouse();
      final lines = await repo.stockLines(warehouseId: warehouse.id);
      final product = lines.singleWhere(
        (line) => line.productId == 'stock-widget-product',
      );

      expect(product.onHandE4, 120000);
      expect(await repo.reconcile(), isEmpty);

      final movementRows = await db
          .customSelect(
            'SELECT movement_type, qty_e4, client_request_id '
            'FROM stock_movements WHERE product_id = ?',
            variables: const [Variable<String>('stock-widget-product')],
          )
          .get();

      expect(movementRows, hasLength(1));
      expect(movementRows.single.read<String>('movement_type'), 'receipt');
      expect(movementRows.single.read<int>('qty_e4'), 120000);
      expect(movementRows.single.read<String>('client_request_id'), isNotEmpty);
    },
  );

  testWidgets('zero quantity is rejected before saving', (tester) async {
    await tester.binding.setSurfaceSize(const Size(1000, 1400));
    addTearDown(() => tester.binding.setSurfaceSize(null));

    final db = AppDatabase(NativeDatabase.memory());
    addTearDown(db.close);

    await db
        .into(db.products)
        .insert(
          ProductsCompanion.insert(
            id: 'stock-widget-zero',
            companyId: 'local-company',
            sku: 'SKU-ZERO',
            name: 'Zero Quantity Product',
            updatedAt: DateTime(2026, 10, 21),
            costPriceE4: const Value(0),
            deletedAt: const Value(null),
          ),
        );

    await tester.pumpWidget(
      MaterialApp(
        home: StockInScreen(db: db, companyId: 'local-company'),
      ),
    );
    await tester.pumpAndSettle();

    await tester.tap(find.text('Zero Quantity Product'));
    await tester.pumpAndSettle();

    await tester.enterText(find.widgetWithText(TextField, 'Quantity'), '0');
    await tester.tap(find.text('Review Stock'));
    await tester.pumpAndSettle();

    expect(
      find.text('Enter a quantity above zero (up to 4 decimals).'),
      findsOneWidget,
    );
    expect(find.text('Confirm and save'), findsNothing);

    final count = await db
        .customSelect('SELECT COUNT(*) AS c FROM stock_movements')
        .getSingle();

    expect(count.read<int>('c'), 0);
  });

  testWidgets(
    'opening stock and stock removal update balance and reject excess removal',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1000, 1400));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final db = AppDatabase(NativeDatabase.memory());
      addTearDown(db.close);

      await db
          .into(db.products)
          .insert(
            ProductsCompanion.insert(
              id: 'stock-widget-opening-issue',
              companyId: 'local-company',
              sku: 'SKU-OPEN-ISSUE',
              name: 'Opening and Removal Product',
              updatedAt: DateTime(2026, 10, 21),
              costPriceE4: const Value(0),
              deletedAt: const Value(null),
            ),
          );

      var changedCalls = 0;

      await tester.pumpWidget(
        MaterialApp(
          home: StockInScreen(
            db: db,
            companyId: 'local-company',
            onChanged: () => changedCalls++,
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Record the initial stock through the Opening stock action.
      await tester.tap(find.byTooltip('Stock actions').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Opening stock'));
      await tester.pumpAndSettle();

      await tester.enterText(find.widgetWithText(TextField, 'Quantity'), '8');
      await tester.enterText(
        find.widgetWithText(TextField, 'Entered by'),
        'Test Operator',
      );
      await tester.tap(find.text('Review Stock'));
      await tester.pumpAndSettle();

      expect(find.text('On hand after'), findsOneWidget);
      expect(find.text('8'), findsWidgets);

      await tester.tap(find.text('Confirm and save'));
      await tester.pumpAndSettle();

      expect(changedCalls, 1);
      expect(find.textContaining('On hand: 8'), findsOneWidget);

      // Remove three units, with a required reason and operator.
      await tester.tap(find.byTooltip('Stock actions').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Remove stock (damaged, lost)'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextField, 'Quantity to remove'),
        '3',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Reason (required)'),
        'Damaged during handling',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Entered by'),
        'Test Operator',
      );

      await tester.tap(find.text('Review Stock'));
      await tester.pumpAndSettle();

      expect(find.text('On hand after'), findsOneWidget);
      expect(find.text('5'), findsWidgets);

      await tester.tap(find.text('Confirm and save'));
      await tester.pumpAndSettle();

      expect(changedCalls, 2);
      expect(find.textContaining('On hand: 5'), findsOneWidget);

      final repo = LocalInventoryRepository(db, companyId: 'local-company');
      final warehouse = await repo.ensureDefaultWarehouse();
      final lines = await repo.stockLines(warehouseId: warehouse.id);
      final product = lines.singleWhere(
        (line) => line.productId == 'stock-widget-opening-issue',
      );

      expect(product.onHandE4, 50000);
      expect(await repo.reconcile(), isEmpty);

      final movementRows = await db
          .customSelect(
            'SELECT movement_type, qty_e4, reason '
            'FROM stock_movements WHERE product_id = ? ORDER BY created_at',
            variables: const [Variable<String>('stock-widget-opening-issue')],
          )
          .get();

      expect(movementRows, hasLength(2));
      expect(movementRows[0].read<String>('movement_type'), 'opening');
      expect(movementRows[0].read<int>('qty_e4'), 80000);
      expect(movementRows[1].read<String>('movement_type'), 'issue');
      expect(movementRows[1].read<int>('qty_e4'), -30000);
      expect(movementRows[1].read<String>('reason'), 'Damaged during handling');

      // Attempt to remove more than the five units remaining.
      await tester.tap(find.byTooltip('Stock actions').first);
      await tester.pumpAndSettle();
      await tester.tap(find.text('Remove stock (damaged, lost)'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.widgetWithText(TextField, 'Quantity to remove'),
        '6',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Reason (required)'),
        'Excess removal test',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Entered by'),
        'Test Operator',
      );

      await tester.tap(find.text('Review Stock'));
      await tester.pumpAndSettle();

      expect(find.text('Only 5 on hand.'), findsOneWidget);
      expect(find.text('Confirm and save'), findsNothing);

      final finalCount = await db
          .customSelect(
            'SELECT COUNT(*) AS c FROM stock_movements WHERE product_id = ?',
            variables: const [Variable<String>('stock-widget-opening-issue')],
          )
          .getSingle();

      expect(finalCount.read<int>('c'), 2);
      expect(
        (await repo.stockLines(warehouseId: warehouse.id))
            .singleWhere(
              (line) => line.productId == 'stock-widget-opening-issue',
            )
            .onHandE4,
        50000,
      );
    },
  );
}
