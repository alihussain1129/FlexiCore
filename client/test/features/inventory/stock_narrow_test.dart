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
const _longName =
    'Extra long product name for the narrow phone layout check with many words';
const _longNote =
    'A very long note that explains in great detail why this stock movement happened today';

class _Setup {
  _Setup(this.db, this.repo, this.wh);
  final AppDatabase db;
  final LocalInventoryRepository repo;
  final Warehouse wh;
}

Future<_Setup> _setup(WidgetTester tester) async {
  await tester.binding.setSurfaceSize(const Size(360, 640));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: 'n1',
          companyId: _company,
          sku: 'SKU-LONG-0001',
          name: _longName,
          updatedAt: DateTime(2026, 10, 21),
        ),
      );
  final repo = LocalInventoryRepository(db, companyId: _company);
  final wh = await repo.ensureDefaultWarehouse();
  return _Setup(db, repo, wh);
}

Future<void> _rec(
  _Setup s,
  StockMovementType type,
  int qty, {
  String? reason,
}) => s.repo.record(
  MovementRequest(
    productId: 'n1',
    warehouseId: s.wh.id,
    type: type,
    quantityE4: qty,
    requestId: generateUuidV4(),
    actor: 'Tester',
    reason: reason,
  ),
);

void _fits(WidgetTester tester, String where) {
  expect(tester.takeException(), isNull, reason: 'layout problem at: $where');
}

Future<void> _tap(WidgetTester tester, Finder f) async {
  await tester.ensureVisible(f);
  await tester.pumpAndSettle();
  await tester.tap(f);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('stock screen and its forms fit a narrow phone', (tester) async {
    final s = await _setup(tester);
    await _rec(s, StockMovementType.receipt, 100000, reason: _longNote);

    await tester.pumpWidget(
      MaterialApp(home: StockInScreen(db: s.db, companyId: _company)),
    );
    await tester.pumpAndSettle();
    _fits(tester, 'stock list');

    await tester.tap(find.byTooltip('Stock actions').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Remove stock (damaged, lost)'));
    await tester.pumpAndSettle();
    _fits(tester, 'remove form');

    await tester.enterText(
      find.widgetWithText(TextField, 'Quantity to remove'),
      '1',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Reason (required)'),
      _longNote,
    );
    await _tap(tester, find.text('Review Stock'));
    expect(find.text('On hand after'), findsOneWidget);
    _fits(tester, 'remove review step');

    await _tap(tester, find.text('Confirm and save'));
    expect(find.textContaining('On hand: 9'), findsOneWidget);
    _fits(tester, 'stock list after saving');

    await tester.tap(find.byTooltip('Stock actions').first);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Adjust stock (count correction)'));
    await tester.pumpAndSettle();
    _fits(tester, 'adjust form');
    await tester.enterText(
      find.widgetWithText(TextField, 'Counted quantity'),
      '5',
    );
    await tester.enterText(
      find.widgetWithText(TextField, 'Reason (required)'),
      _longNote,
    );
    await _tap(tester, find.text('Review Stock'));
    expect(find.text('Difference'), findsOneWidget);
    _fits(tester, 'adjust review step');

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    await tester.tap(find.textContaining('Warehouse:'));
    await tester.pumpAndSettle();
    _fits(tester, 'warehouse menu');
    await tester.tap(find.text('Add warehouse...'));
    await tester.pumpAndSettle();
    _fits(tester, 'add warehouse dialog');
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
  });

  testWidgets('ledger screen fits a narrow phone', (tester) async {
    final s = await _setup(tester);
    await _rec(s, StockMovementType.receipt, 100000, reason: _longNote);
    await _rec(s, StockMovementType.issue, 20000, reason: _longNote);
    await _rec(s, StockMovementType.adjustment, -10000, reason: _longNote);

    await tester.pumpWidget(
      MaterialApp(
        home: StockLedgerScreen(db: s.db, companyId: _company, warehouse: s.wh),
      ),
    );
    await tester.pumpAndSettle();
    _fits(tester, 'balances tab');

    await _tap(tester, find.text('Movements'));
    _fits(tester, 'movements tab');

    await _tap(tester, find.widgetWithText(ChoiceChip, 'Issued'));
    _fits(tester, 'issued filter');
    await _tap(tester, find.widgetWithText(ChoiceChip, 'All'));
    _fits(tester, 'all filter');

    await tester.tap(find.byTooltip('Reverse this movement').first);
    await tester.pumpAndSettle();
    expect(find.text('Reverse this movement?'), findsOneWidget);
    _fits(tester, 'reverse dialog');
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
  });
}