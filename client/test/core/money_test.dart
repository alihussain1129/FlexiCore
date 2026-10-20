import 'package:flexicore_client/core/money.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Money.parseE4', () {
    test('parses whole and decimal amounts exactly', () {
      expect(Money.parseE4('12'), 120000);
      expect(Money.parseE4('12.5'), 125000);
      expect(Money.parseE4(' 12.50 '), 125000);
      expect(Money.parseE4('0.0001'), 1);
      expect(Money.parseE4('0'), 0);
      expect(Money.parseE4('1234.5678'), 12345678);
    });

    test('rejects invalid input', () {
      for (final bad in ['', ' ', '-1', '1.23456', '1,5', 'abc', '1.', '.5']) {
        expect(Money.parseE4(bad), isNull, reason: 'input "$bad"');
      }
      expect(Money.parseE4('123456789012345'), isNull); // 15 whole digits
    });
  });

  group('Money.formatE4', () {
    test('always shows at least two decimals and keeps precision', () {
      expect(Money.formatE4(0), '0.00');
      expect(Money.formatE4(120000), '12.00');
      expect(Money.formatE4(125000), '12.50');
      expect(Money.formatE4(123456), '12.3456');
      expect(Money.formatE4(1), '0.0001');
    });

    test('round-trips', () {
      for (final v in [0, 1, 9999, 10000, 123456, 999999999]) {
        expect(Money.parseE4(Money.formatE4(v)), v);
      }
    });
  });
}
