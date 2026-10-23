import 'package:drift/native.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/catalog/data/local_catalog_repository.dart';
import 'package:flexicore_client/features/catalog/presentation/catalog_controller.dart';
import 'package:flexicore_client/features/catalog/presentation/catalog_screen.dart';
import 'package:flexicore_client/features/inventory/data/local_inventory_repository.dart';
import 'package:flexicore_client/features/inventory/data/opening_stock_recorder.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _company = 'local-company';

class _Rig {
  _Rig(this.db, this.inventory, this.controller, this.requestIds);
  final AppDatabase db;
  final LocalInventoryRepository inventory;
  final CatalogController controller;
  final List<String> requestIds;

  Future<int> openingRows() async {
    final row = await db
        .customSelect(
          "SELECT COUNT(*) AS c FROM stock_movements "
          "WHERE movement_type = 'opening'",
        )
        .getSingle();
    return row.read<int>('c');
  }
}

/// [failFirst] makes the first recorder call fail before anything is saved,
/// like a temporary error, so the retry path can be tested.
Future<_Rig> _rig(
  WidgetTester tester, {
  bool withRecorder = true,
  bool failFirst = false,
  Size size = const Size(1000, 1600),
}) async {
  await tester.binding.setSurfaceSize(size);
  addTearDown(() => tester.binding.setSurfaceSize(null));

  final db = AppDatabase(NativeDatabase.memory());
  addTearDown(db.close);
  final inventory = LocalInventoryRepository(db, companyId: _company);
  await inventory.ensureDefaultWarehouse();

  final real = localOpeningStockRecorder(inventory);
  final ids = <String>[];
  var calls = 0;
  Future<String?> wrapped({
    required String productId,
    required String quantityText,
    required String requestId,
  }) async {
    calls++;
    ids.add(requestId);
    if (failFirst && calls == 1) return 'Temporary failure.';
    return real(
      productId: productId,
      quantityText: quantityText,
      requestId: requestId,
    );
  }

  final controller = CatalogController(
    LocalCatalogRepository(db, companyId: _company),
    openingStock: withRecorder ? wrapped : null,
  );
  await controller.load();

  await tester.pumpWidget(
    MaterialApp(home: CatalogScreen(controller: controller)),
  );
  await tester.pumpAndSettle();
  return _Rig(db, inventory, controller, ids);
}

Finder _field(String labelPart) => find.byWidgetPredicate(
  (w) =>
      w is TextField &&
      (w.decoration?.labelText ?? '').toLowerCase().contains(
        labelPart.toLowerCase(),
      ),
  description: 'TextField labelled like "$labelPart"',
);

Finder _textLike(List<String> parts) => find.byWidgetPredicate((w) {
  if (w is! Text) return false;
  final t = (w.data ?? '').toLowerCase();
  return parts.any(t.contains);
}, description: 'Text containing one of $parts');

Finder get _saveButton => find.descendant(
  of: find.byType(AlertDialog),
  matching: find.byType(FilledButton),
);

Future<void> _openDialog(WidgetTester tester) async {
  await tester.tap(find.text('Add product').first);
  await tester.pumpAndSettle();
  expect(find.byType(AlertDialog), findsOneWidget);
}

Future<void> _fillProduct(WidgetTester tester, {String sku = 'DLG-1'}) async {
  await tester.enterText(_field('sku'), sku);
  await tester.enterText(_field('name'), 'Dialog product');
  await tester.enterText(_field('sale'), '10');
}

void main() {
  testWidgets('the dialog opens with product fields and an opening stock field', (
    tester,
  ) async {
    await _rig(tester);
    await _openDialog(tester);
    expect(_field('sku'), findsOneWidget);
    expect(_field('name'), findsOneWidget);
    expect(_field('sale'), findsOneWidget);
    expect(_field('opening'), findsOneWidget);
  });

  testWidgets('no opening stock field when the host gives no recorder', (
    tester,
  ) async {
    await _rig(tester, withRecorder: false);
    await _openDialog(tester);
    expect(_field('opening'), findsNothing);
  });

  testWidgets('editing an existing product offers no opening stock field', (
    tester,
  ) async {
    final r = await _rig(tester);
    final made = await r.controller.createProduct(
      sku: 'EDIT-1',
      name: 'Dialog product',
      salePrice: '10',
      costPrice: '',
    );
    expect(made.error, isNull);
    await tester.pumpAndSettle();

    await tester.tap(find.text('Dialog product'));
    await tester.pumpAndSettle();
    if (find.byType(AlertDialog).evaluate().isEmpty) {
      final icon = find.byIcon(Icons.edit_outlined);
      final icon2 = find.byIcon(Icons.edit);
      await tester.tap(
        icon.evaluate().isNotEmpty ? icon.first : icon2.first,
      );
      await tester.pumpAndSettle();
    }
    expect(find.byType(AlertDialog), findsOneWidget);
    expect(_field('sku'), findsOneWidget);
    expect(_field('opening'), findsNothing);
  });

  testWidgets('invalid opening quantities are refused and no product is created', (
    tester,
  ) async {
    final r = await _rig(tester);
    await _openDialog(tester);
    await _fillProduct(tester);

    for (final bad in ['0', '-1', 'abc', '1,5']) {
      await tester.enterText(_field('opening'), bad);
      await tester.tap(_saveButton);
      await tester.pumpAndSettle();

      expect(find.byType(AlertDialog), findsOneWidget, reason: '"$bad"');
      expect(
        find.textContaining('Opening stock must be a number'),
        findsOneWidget,
        reason: '"$bad"',
      );
      expect(r.controller.products, isEmpty, reason: '"$bad"');
      expect(await r.openingRows(), 0, reason: '"$bad"');
    }
  });

  testWidgets('a valid quantity creates the product and one opening entry', (
    tester,
  ) async {
    final r = await _rig(tester);
    await _openDialog(tester);
    await _fillProduct(tester);
    await tester.enterText(_field('opening'), '8');
    await tester.tap(_saveButton);
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsNothing);
    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 1);
    final wh = await r.inventory.ensureDefaultWarehouse();
    final lines = await r.inventory.stockLines(warehouseId: wh.id);
    expect(lines.single.onHandE4, 80000);
    expect(await r.inventory.reconcile(), isEmpty);
  });

  testWidgets('leaving the field empty creates the product with no stock', (
    tester,
  ) async {
    final r = await _rig(tester);
    await _openDialog(tester);
    await _fillProduct(tester);
    await tester.tap(_saveButton);
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsNothing);
    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 0);
  });

  testWidgets('a failed opening entry keeps the product and retry adds it once', (
    tester,
  ) async {
    final r = await _rig(tester, failFirst: true);
    await _openDialog(tester);
    await _fillProduct(tester);
    await tester.enterText(_field('opening'), '5');
    await tester.tap(_saveButton);
    await tester.pumpAndSettle();

    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 0);
    expect(find.textContaining('The product was saved'), findsOneWidget);
    expect(find.text('Retry opening stock'), findsOneWidget);

    await tester.tap(find.text('Retry opening stock'));
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsNothing);
    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 1);
    expect(r.requestIds, hasLength(2));
    expect(r.requestIds[0], r.requestIds[1]);
  });

  testWidgets('a duplicate SKU shows an error and creates no second product', (
    tester,
  ) async {
    final r = await _rig(tester);
    final first = await r.controller.createProduct(
      sku: 'DUP-1',
      name: 'First product',
      salePrice: '10',
      costPrice: '',
    );
    expect(first.error, isNull);
    await tester.pumpAndSettle();

    await _openDialog(tester);
    await _fillProduct(tester, sku: 'DUP-1');
    await tester.enterText(_field('opening'), '4');
    await tester.tap(_saveButton);
    await tester.pumpAndSettle();

    expect(find.byType(AlertDialog), findsOneWidget);
    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 0);
    expect(
      _textLike(['already', 'duplicate', 'exists', 'taken', 'in use']),
      findsWidgets,
    );
  });

  testWidgets('the dialog fits a 360 px wide phone and still saves', (
    tester,
  ) async {
    final r = await _rig(tester, size: const Size(360, 740));
    await _openDialog(tester);
    expect(tester.takeException(), isNull);

    expect(find.text('Cancel'), findsOneWidget);
    expect(_saveButton, findsOneWidget);
    expect(tester.getRect(find.text('Cancel')).bottom, lessThanOrEqualTo(740.0));
    expect(tester.getRect(_saveButton).bottom, lessThanOrEqualTo(740.0));
    expect(tester.getRect(_saveButton).right, lessThanOrEqualTo(360.0));

    await _fillProduct(tester);
    await tester.enterText(_field('opening'), '3');
    await tester.tap(_saveButton);
    await tester.pumpAndSettle();

    expect(tester.takeException(), isNull);
    expect(find.byType(AlertDialog), findsNothing);
    expect(r.controller.products, hasLength(1));
    expect(await r.openingRows(), 1);
  });
}