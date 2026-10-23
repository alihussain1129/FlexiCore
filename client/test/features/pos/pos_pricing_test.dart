import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/features/pos/domain/pos_pricing.dart';

void main() {
  group('PosPricing.calculate', () {
    test('no discount and no tax keeps the plain line total', () {
      final p = PosPricing.calculate(
        lines: const [PosPriceInput(quantityE4: 20000, unitPriceE4: 1000000)],
      );
      expect(p.subtotalE4, 2000000);
      expect(p.discountE4, 0);
      expect(p.taxE4, 0);
      expect(p.totalE4, 2000000);
    });

    test('a fractional line total rounds half up to one E4 unit', () {
      final up = PosPricing.calculate(
        lines: const [PosPriceInput(quantityE4: 5000, unitPriceE4: 1)],
      );
      final down = PosPricing.calculate(
        lines: const [PosPriceInput(quantityE4: 4999, unitPriceE4: 1)],
      );
      expect(up.totalE4, 1);
      expect(down.totalE4, 0);
    });

    test('tax is added on top of the net amount', () {
      final p = PosPricing.calculate(
        lines: const [PosPriceInput(quantityE4: 20000, unitPriceE4: 1000000)],
        taxRateBp: 1500,
      );
      expect(p.taxE4, 300000);
      expect(p.totalE4, 2300000);
      expect(p.lines.single.taxE4, 300000);
    });

    test('tax rounds half up per line', () {
      PosPricing one(int price) => PosPricing.calculate(
        lines: [PosPriceInput(quantityE4: 10000, unitPriceE4: price)],
        taxRateBp: 1000,
      );
      expect(one(5).taxE4, 1);
      expect(one(4).taxE4, 0);
    });

    test('an order discount is shared exactly and lowers the tax', () {
      final p = PosPricing.calculate(
        lines: const [
          PosPriceInput(quantityE4: 10000, unitPriceE4: 100000),
          PosPriceInput(quantityE4: 10000, unitPriceE4: 100000),
          PosPriceInput(quantityE4: 10000, unitPriceE4: 100000),
        ],
        orderDiscountE4: 100000,
        taxRateBp: 1000,
      );
      final shares = p.lines.map((l) => l.discountE4).toList();
      expect(shares.reduce((a, b) => a + b), 100000);
      expect(shares.every((s) => s == 33333 || s == 33334), isTrue);
      expect(p.discountE4, 100000);
      expect(p.totalE4, p.subtotalE4 - p.discountE4 + p.taxE4);
      final lineSum = p.lines.fold<int>(0, (a, l) => a + l.netE4 + l.taxE4);
      expect(lineSum, p.totalE4);
    });

    test('line and order discounts combine and still add up', () {
      final p = PosPricing.calculate(
        lines: const [
          PosPriceInput(
            quantityE4: 20000,
            unitPriceE4: 500000,
            lineDiscountE4: 50000,
          ),
          PosPriceInput(quantityE4: 10000, unitPriceE4: 250000),
        ],
        orderDiscountE4: 70000,
        taxRateBp: 500,
      );
      expect(p.discountE4, 120000);
      expect(p.totalE4, p.subtotalE4 - p.discountE4 + p.taxE4);
    });

    test('bad inputs are refused', () {
      const line = PosPriceInput(quantityE4: 10000, unitPriceE4: 100000);
      expect(
        () => PosPricing.calculate(
          lines: const [
            PosPriceInput(
              quantityE4: 10000,
              unitPriceE4: 100000,
              lineDiscountE4: 100001,
            ),
          ],
        ),
        throwsA(isA<PosPricingException>()),
      );
      expect(
        () =>
            PosPricing.calculate(lines: const [line], orderDiscountE4: 100001),
        throwsA(isA<PosPricingException>()),
      );
      expect(
        () => PosPricing.calculate(lines: const [line], orderDiscountE4: -1),
        throwsA(isA<PosPricingException>()),
      );
      expect(
        () => PosPricing.calculate(lines: const [line], taxRateBp: 10001),
        throwsA(isA<PosPricingException>()),
      );
      expect(
        () => PosPricing.calculate(
          lines: const [PosPriceInput(quantityE4: 0, unitPriceE4: 1)],
        ),
        throwsA(isA<PosPricingException>()),
      );
    });
  });

  group('PosPricing.allocate', () {
    test('shares add up and the largest remainder gets the spare unit', () {
      expect(PosPricing.allocate(1, [1, 1, 1]), [1, 0, 0]);
      expect(PosPricing.allocate(0, [5, 5]), [0, 0]);
      final shares = PosPricing.allocate(100, [3333, 3333, 3334]);
      expect(shares.reduce((a, b) => a + b), 100);
    });
  });

  test('percentLabel formats basis points', () {
    expect(PosPricing.percentLabel(1500), '15%');
    expect(PosPricing.percentLabel(1250), '12.50%');
  });
}
