/// Pure money maths for a sale. No database and no widgets.
///
/// All amounts are integer E4 values (1.0000 is 10000). Rounding is half-up
/// to the nearest E4 unit and is applied once per line, so the lines always
/// add up exactly to the sale totals.
class PosPricingException implements Exception {
  const PosPricingException(this.message);

  final String message;

  @override
  String toString() => 'PosPricingException: $message';
}

class PosPriceInput {
  const PosPriceInput({
    required this.quantityE4,
    required this.unitPriceE4,
    this.lineDiscountE4 = 0,
  });

  final int quantityE4;
  final int unitPriceE4;
  final int lineDiscountE4;
}

class PosPricedLine {
  const PosPricedLine({
    required this.grossE4,
    required this.discountE4,
    required this.netE4,
    required this.taxE4,
  });

  /// Quantity times price, before any discount.
  final int grossE4;

  /// Line discount plus this line's share of the order discount.
  final int discountE4;

  /// Gross minus discount. This is the line total before tax.
  final int netE4;

  /// Tax added on top of the net amount.
  final int taxE4;
}

class PosPricing {
  const PosPricing._({
    required this.lines,
    required this.subtotalE4,
    required this.discountE4,
    required this.taxE4,
    required this.totalE4,
  });

  final List<PosPricedLine> lines;
  final int subtotalE4;
  final int discountE4;
  final int taxE4;
  final int totalE4;

  static const int _maxInt = 0x7fffffffffffffff;

  /// Percentage of an E4 amount, rate in basis points (1500 is 15%).
  static int percentOfE4(int amountE4, int rateBp) =>
      (amountE4 * rateBp + 5000) ~/ 10000;

  static String percentLabel(int rateBp) {
    final whole = rateBp ~/ 100;
    final fraction = rateBp % 100;
    if (fraction == 0) return '$whole%';
    final f = fraction.toString().padLeft(2, '0');
    return '$whole.$f%';
  }

  /// Splits [total] across [weights] in proportion, using the largest
  /// remainder rule. The shares always add up to exactly [total] and no
  /// share is larger than its weight.
  static List<int> allocate(int total, List<int> weights) {
    if (total == 0 || weights.isEmpty) {
      return List<int>.filled(weights.length, 0);
    }
    var sum = 0;
    for (final w in weights) {
      sum += w;
    }
    if (sum <= 0) {
      throw const PosPricingException('Nothing to apply the discount to.');
    }
    final bigTotal = BigInt.from(total);
    final bigSum = BigInt.from(sum);
    final shares = <int>[];
    final remainders = <BigInt>[];
    var assigned = 0;
    for (final w in weights) {
      final scaled = bigTotal * BigInt.from(w);
      final share = (scaled ~/ bigSum).toInt();
      shares.add(share);
      remainders.add(scaled % bigSum);
      assigned += share;
    }
    var left = total - assigned;
    final order = List<int>.generate(weights.length, (i) => i)
      ..sort((a, b) {
        final c = remainders[b].compareTo(remainders[a]);
        return c != 0 ? c : a.compareTo(b);
      });
    for (final i in order) {
      if (left == 0) break;
      if (remainders[i] > BigInt.zero) {
        shares[i] += 1;
        left -= 1;
      }
    }
    return shares;
  }

  static PosPricing calculate({
    required List<PosPriceInput> lines,
    int orderDiscountE4 = 0,
    int taxRateBp = 0,
  }) {
    if (orderDiscountE4 < 0) {
      throw const PosPricingException('Discount cannot be negative.');
    }
    if (taxRateBp < 0 || taxRateBp > 10000) {
      throw const PosPricingException('Tax rate must be between 0% and 100%.');
    }

    final gross = <int>[];
    final afterLineDiscount = <int>[];
    var subtotal = 0;
    var afterLines = 0;

    for (final line in lines) {
      if (line.quantityE4 <= 0 || line.unitPriceE4 < 0) {
        throw const PosPricingException('Invalid quantity or price.');
      }
      if (line.lineDiscountE4 < 0) {
        throw const PosPricingException('Discount cannot be negative.');
      }
      final product = line.quantityE4 * line.unitPriceE4;
      if (product < 0 || product > _maxInt - 5000) {
        throw const PosPricingException('Line total is too large.');
      }
      final g = (product + 5000) ~/ 10000;
      if (line.lineDiscountE4 > g) {
        throw const PosPricingException(
          'A line discount cannot exceed the line amount.',
        );
      }
      if (g > _maxInt - subtotal) {
        throw const PosPricingException('Cart total is too large.');
      }
      subtotal += g;
      gross.add(g);
      afterLineDiscount.add(g - line.lineDiscountE4);
      afterLines += g - line.lineDiscountE4;
    }

    if (orderDiscountE4 > afterLines) {
      throw const PosPricingException(
        'The discount cannot exceed the sale amount.',
      );
    }

    final shares = allocate(orderDiscountE4, afterLineDiscount);
    final priced = <PosPricedLine>[];
    var discountTotal = 0;
    var taxTotal = 0;
    var total = 0;

    for (var i = 0; i < lines.length; i++) {
      final net = afterLineDiscount[i] - shares[i];
      if (taxRateBp > 0 && net > (_maxInt - 5000) ~/ taxRateBp) {
        throw const PosPricingException('Line total is too large.');
      }
      final tax = percentOfE4(net, taxRateBp);
      final discount = lines[i].lineDiscountE4 + shares[i];
      priced.add(
        PosPricedLine(
          grossE4: gross[i],
          discountE4: discount,
          netE4: net,
          taxE4: tax,
        ),
      );
      discountTotal += discount;
      taxTotal += tax;
      total += net + tax;
    }

    return PosPricing._(
      lines: priced,
      subtotalE4: subtotal,
      discountE4: discountTotal,
      taxE4: taxTotal,
      totalE4: total,
    );
  }
}
