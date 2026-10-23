import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_repository.dart';
import 'package:flexicore_client/features/pos/domain/pos_format.dart';
import 'package:flexicore_client/features/pos/presentation/pos_receipt_text.dart';

PosReceipt _receipt({required int paid, int change = 0}) => PosReceipt(
  id: 's1',
  receiptNumber: 'FC-1',
  warehouseId: 'w1',
  createdAt: DateTime.utc(2026, 10, 22, 9, 5),
  subtotalE4: 2000000,
  totalE4: 2185000,
  amountPaidE4: paid,
  paymentMethod: 'Cash',
  discountE4: 100000,
  taxE4: 285000,
  changeE4: change,
  taxRateBp: 1500,
  lines: const [
    PosReceiptLine(
      productId: 'p1',
      sku: 'SKU1',
      name: 'Widget',
      quantityE4: 20000,
      unitPriceE4: 1000000,
      lineTotalE4: 1900000,
      discountE4: 100000,
      taxE4: 285000,
    ),
  ],
);

void main() {
  group('PosFormat', () {
    test('money groups thousands and keeps at least two decimals', () {
      expect(PosFormat.money(1000000), '100.00');
      expect(PosFormat.money(12345000), '1,234.50');
      expect(PosFormat.money(5), '0.0005');
      expect(PosFormat.money(0), '0.00');
      expect(PosFormat.money(-12500), '-1.25');
    });

    test('plain trims trailing zeros', () {
      expect(PosFormat.plain(12500), '1.25');
      expect(PosFormat.plain(10000), '1');
      expect(PosFormat.plain(0), '0');
      expect(PosFormat.plain(1), '0.0001');
    });

    test('parseE4 accepts plain numbers and refuses everything else', () {
      expect(PosFormat.parseE4('12.5'), 125000);
      expect(PosFormat.parseE4(' 7 '), 70000);
      expect(PosFormat.parseE4('0.0001'), 1);
      expect(PosFormat.parseE4('1.23456'), isNull);
      expect(PosFormat.parseE4(''), isNull);
      expect(PosFormat.parseE4('abc'), isNull);
      expect(PosFormat.parseE4('-1'), isNull);
    });

    test('parsePercentBp gives basis points and stops at 100%', () {
      expect(PosFormat.parsePercentBp('15'), 1500);
      expect(PosFormat.parsePercentBp('12.5'), 1250);
      expect(PosFormat.parsePercentBp('100'), 10000);
      expect(PosFormat.parsePercentBp('100.01'), isNull);
      expect(PosFormat.parsePercentBp('101'), isNull);
      expect(PosFormat.parsePercentBp('1.234'), isNull);
    });

    test('dateTime pads to two digits', () {
      expect(
        PosFormat.dateTime(DateTime(2026, 10, 22, 9, 5)),
        '2026-10-22 09:05',
      );
    });
  });

  group('buildReceiptText', () {
    test('shows discount, tax, paid and change for a cash sale', () {
      final text = buildReceiptText(
        _receipt(paid: 2185000, change: 2815000),
        businessName: 'EverFine',
        footer: 'Thank you',
      );
      expect(text, contains('EverFine'));
      expect(text, contains('Receipt: FC-1'));
      expect(text, contains('Widget'));
      expect(text, contains('Tax (15%)'));
      expect(text, contains('28.50'));
      expect(text, contains('218.50'));
      expect(text, contains('Change'));
      expect(text, contains('281.50'));
      expect(text, contains('Thank you'));
      expect(text, isNot(contains('Remaining due')));
    });

    test('shows the remaining due for a part-paid sale', () {
      final text = buildReceiptText(_receipt(paid: 1000000));
      expect(text, contains('Remaining due'));
      expect(text, contains('118.50'));
      expect(text, isNot(contains('Change')));
    });
  });
}
