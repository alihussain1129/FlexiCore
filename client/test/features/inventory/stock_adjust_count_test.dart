import 'package:drift/native.dart';
import 'package:flexicore_client/core/ids.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/domain/inventory_domain.dart';
import 'package:flexicore_client/features/inventory/presentation/stock_in_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'local-company';

class _S {
  _S(this.db, this.repo, this.warehouseId);
  final AppDatabase db;
  final LocalInventoryRepository repo;
  final String warehouseId;

  Future<int> onHandE4() async {
    final lines = await repo.stockLines(warehouseId: warehouseId);
    return lines.single.onHandE4;
  }
}

Future<_S> _setup(WidgetTester tester, int receivedE4) async {
  await tester.binding.setSurfaceSize(const Size(800, 1200));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: 'c1',
          companyId: _company,
          sku: 'SKU-COUNT-1',
          name: 'Count test product',
          updatedAt: DateTime(2026, 10, 23),
        ),
      );
  final repo = LocalInventoryRepository(db, companyId: _company);
  final wh = await repo.ensureDefaultWarehouse();
  await repo.record(
    MovementRequest(
      productId: 'c1',
      warehouseId: wh.id,
      type: StockMovementType.receipt,
      quantityE4: receivedE4,
      requestId: generateUuidV4(),
      actor: 'Tester',
    ),
  );
  return _S(db, repo, wh.id);
}

Future<void> _tap(WidgetTester tester, Finder f) async {
  await tester.ensureVisible(f);
  await tester.pumpAndSettle();
  await tester.tap(f);
  await tester.pumpAndSettle();
}

Future<void> _openAdjust(WidgetTester tester, _S s) async {
  await tester.pumpWidget(
    MaterialApp(home: StockInScreen(db: s.db, companyId: _company)),
  );
  await tester.pumpAndSettle();
  await tester.tap(find.byTooltip('Stock actions').first);
  await tester.pumpAndSettle();
  await tester.tap(find.text('Adjust stock (count correction)'));
  await tester.pumpAndSettle();
}

Future<void> _fill(WidgetTester tester, String counted) async {
  await tester.enterText(
    find.widgetWithText(TextField, 'Counted quantity'),
    counted,
  );
  await tester.enterText(
    find.widgetWithText(TextField, 'Reason (required)'),
    'Physical count',
  );
}

void main() {
  testWidgets('a lower count reduces stock by the difference', (tester) async {
    final s = await _setup(tester, 100000);
    await _openAdjust(tester, s);
    await _fill(tester, '7');
    await _tap(tester, find.text('Review Stock'));
    expect(find.text('System quantity'), findsOneWidget);
    expect(find.text('Counted quantity'), findsOneWidget);
    expect(find.text('-3'), findsOneWidget);
    await _tap(tester, find.text('Confirm and save'));
    expect(find.textContaining('On hand: 7'), findsOneWidget);
    expect(await s.onHandE4(), 70000);
  });

  testWidgets('a count equal to the system quantity is refused', (tester) async {
    final s = await _setup(tester, 100000);
    await _openAdjust(tester, s);
    await _fill(tester, '10');
    await _tap(tester, find.text('Review Stock'));
    expect(find.textContaining('Nothing to adjust'), findsOneWidget);
    expect(find.text('System quantity'), findsNothing);
    expect(await s.onHandE4(), 100000);
  });

  testWidgets('a higher count increases stock by the difference', (tester) async {
    final s = await _setup(tester, 100000);
    await _openAdjust(tester, s);
    await _fill(tester, '12.5');
    await _tap(tester, find.text('Review Stock'));
    expect(find.text('+2.5'), findsOneWidget);
    await _tap(tester, find.text('Confirm and save'));
    expect(find.textContaining('On hand: 12.5'), findsOneWidget);
    expect(await s.onHandE4(), 125000);
  });
}