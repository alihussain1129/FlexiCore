import 'dart:async';

import 'package:flexicore_client/features/pos/data/local/local_returns_repository.dart';
import 'package:flexicore_client/features/pos/presentation/return_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

ReturnableSale _sale() => ReturnableSale(
  saleId: 's1',
  receiptNumber: 'FC-1',
  status: 'completed',
  paymentMethod: 'Cash',
  warehouseId: 'w1',
  createdAt: DateTime.utc(2026, 10, 22, 10),
  totalE4: 3000000,
  paidE4: 3000000,
  lines: const [
    ReturnableLine(
      saleLineId: 'l1',
      productId: 'p1',
      sku: 'SKU1',
      name: 'Widget',
      unitPriceE4: 1000000,
      soldQtyE4: 30000,
      soldNetE4: 3000000,
      soldTaxE4: 0,
      returnedQtyE4: 0,
      returnedNetE4: 0,
      returnedTaxE4: 0,
    ),
  ],
  previousValueE4: 0,
  previousRefundE4: 0,
);

ReturnReceipt _receipt() => ReturnReceipt(
  id: 'r1',
  returnNumber: 'RT-1',
  saleId: 's1',
  saleReceiptNumber: 'FC-1',
  createdAt: DateTime.utc(2026, 10, 22, 10),
  createdBy: 'Ali',
  reason: 'Faulty',
  refundMethod: 'Cash',
  netE4: 1000000,
  taxE4: 0,
  totalE4: 1000000,
  refundE4: 1000000,
  lines: const [
    ReturnReceiptLine(
      saleLineId: 'l1',
      productId: 'p1',
      sku: 'SKU1',
      name: 'Widget',
      quantityE4: 10000,
      netE4: 1000000,
      taxE4: 0,
      restocked: true,
    ),
  ],
);

Future<void> _open(
  WidgetTester tester, {
  required ReturnSubmit onSubmit,
  void Function(ReturnReceipt?)? onResult,
}) async {
  await tester.binding.setSurfaceSize(const Size(800, 1400));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: Builder(
          builder: (context) => Center(
            child: ElevatedButton(
              onPressed: () async {
                final r = await showDialog<ReturnReceipt>(
                  context: context,
                  builder: (_) => ReturnDialog(
                    sale: _sale(),
                    defaultCashier: 'Ali',
                    onSubmit: onSubmit,
                  ),
                );
                onResult?.call(r);
              },
              child: const Text('open'),
            ),
          ),
        ),
      ),
    ),
  );
  await tester.tap(find.text('open'));
  await tester.pumpAndSettle();
}

Future<void> _fill(WidgetTester tester, {String qty = '1'}) async {
  await tester.enterText(find.byKey(const ValueKey('return-qty-l1')), qty);
  await tester.enterText(find.byKey(const Key('return-reason')), 'Faulty');
  await tester.pump();
}

void main() {
  testWidgets('return everything shows a live refund preview', (tester) async {
    await _open(tester, onSubmit: (r) async => _receipt());
    await tester.tap(find.byKey(const Key('return-all')));
    await tester.pump();
    expect(find.text('Rs 300.00'), findsWidgets);
    expect(find.text('Refund now'), findsOneWidget);
  });

  testWidgets('a quantity above what is left shows a message', (tester) async {
    await _open(tester, onSubmit: (r) async => _receipt());
    await tester.enterText(find.byKey(const ValueKey('return-qty-l1')), '4');
    await tester.pump();
    expect(
      find.text('Only 3 of Widget can still be returned.'),
      findsOneWidget,
    );
  });

  testWidgets('a reason is required before the review step', (tester) async {
    await _open(tester, onSubmit: (r) async => _receipt());
    await tester.enterText(find.byKey(const ValueKey('return-qty-l1')), '1');
    await tester.pump();
    await tester.tap(find.byKey(const Key('return-review')));
    await tester.pump();
    expect(find.text('Enter the reason for the return.'), findsOneWidget);
    expect(find.byKey(const Key('return-confirm')), findsNothing);
  });

  testWidgets('confirming sends one request even if tapped twice', (
    tester,
  ) async {
    final calls = <ReturnRequest>[];
    final completer = Completer<ReturnReceipt>();
    ReturnReceipt? result;
    await _open(
      tester,
      onSubmit: (r) {
        calls.add(r);
        return completer.future;
      },
      onResult: (r) => result = r,
    );
    await _fill(tester);
    await tester.tap(find.byKey(const Key('return-review')));
    await tester.pumpAndSettle();
    expect(find.text('Confirm return'), findsOneWidget);

    await tester.tap(find.byKey(const Key('return-confirm')));
    await tester.pump();
    await tester.tap(
      find.byKey(const Key('return-confirm')),
      warnIfMissed: false,
    );
    await tester.pump();
    expect(calls, hasLength(1));

    final sent = calls.single;
    expect(sent.saleId, 's1');
    expect(sent.clientRequestId, isNotEmpty);
    expect(sent.lines.single.saleLineId, 'l1');
    expect(sent.lines.single.quantityE4, 10000);
    expect(sent.lines.single.restock, isTrue);
    expect(sent.refundMethod, 'Cash');
    expect(sent.reason, 'Faulty');
    expect(sent.createdBy, 'Ali');

    completer.complete(_receipt());
    await tester.pumpAndSettle();
    expect(result?.id, 'r1');
  });

  testWidgets('turning off put back in stock sends restock false', (
    tester,
  ) async {
    final calls = <ReturnRequest>[];
    await _open(
      tester,
      onSubmit: (r) async {
        calls.add(r);
        return _receipt();
      },
    );
    await _fill(tester);
    await tester.tap(find.byKey(const ValueKey('return-restock-l1')));
    await tester.pump();
    await tester.tap(find.byKey(const Key('return-review')));
    await tester.pumpAndSettle();
    expect(find.textContaining('not put back in stock'), findsOneWidget);
    await tester.tap(find.byKey(const Key('return-confirm')));
    await tester.pumpAndSettle();
    expect(calls.single.lines.single.restock, isFalse);
  });

  testWidgets('an error from saving is shown and the dialog stays open', (
    tester,
  ) async {
    await _open(
      tester,
      onSubmit: (r) async {
        throw const ReturnException('boom');
      },
    );
    await _fill(tester);
    await tester.tap(find.byKey(const Key('return-review')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('return-confirm')));
    await tester.pumpAndSettle();
    expect(find.text('boom'), findsOneWidget);
    expect(find.byKey(const Key('return-confirm')), findsOneWidget);
  });

  testWidgets('the return receipt dialog shows the receipt text', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(800, 1400));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: ReturnReceiptDialog(receipt: _receipt())),
      ),
    );
    expect(find.text('Return recorded'), findsOneWidget);
    expect(find.textContaining('RETURN RECEIPT'), findsOneWidget);
    expect(find.textContaining('Reason: Faulty'), findsOneWidget);
  });
}
