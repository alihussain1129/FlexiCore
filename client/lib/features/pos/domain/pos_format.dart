/// Plain text helpers for POS amounts. Every value is an integer E4
/// (1.0000 is 10000). No floating point is used anywhere.
class PosFormat {
  const PosFormat._();

  /// 12500 -> "1.25", 10000 -> "1". For text fields and quantities.
  static String plain(int valueE4) {
    final sign = valueE4 < 0 ? '-' : '';
    final abs = valueE4.abs();
    final whole = abs ~/ 10000;
    var fraction = (abs % 10000).toString().padLeft(4, '0');
    while (fraction.endsWith('0')) {
      fraction = fraction.substring(0, fraction.length - 1);
    }
    return fraction.isEmpty ? '$sign$whole' : '$sign$whole.$fraction';
  }

  /// 12345000 -> "1,234.50". At least two decimals, up to four.
  static String money(int valueE4) {
    final sign = valueE4 < 0 ? '-' : '';
    final abs = valueE4.abs();
    final whole = abs ~/ 10000;
    var fraction = (abs % 10000).toString().padLeft(4, '0');
    while (fraction.length > 2 && fraction.endsWith('0')) {
      fraction = fraction.substring(0, fraction.length - 1);
    }
    return '$sign${_group(whole)}.$fraction';
  }

  static String _group(int whole) {
    final digits = whole.toString();
    final out = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) out.write(',');
      out.write(digits[i]);
    }
    return out.toString();
  }

  /// "12.5" -> 125000. Up to four decimals. Null when the text is not a
  /// plain non-negative number.
  static int? parseE4(String input) {
    final match = RegExp(r'^(\d{1,12})(?:\.(\d{1,4}))?$')
        .firstMatch(input.trim());
    if (match == null) return null;
    return int.parse(match.group(1)!) * 10000 +
        int.parse((match.group(2) ?? '').padRight(4, '0'));
  }

  /// "12.5" -> 1250 (basis points). Up to two decimals, at most 100%.
  static int? parsePercentBp(String input) {
    final match = RegExp(r'^(\d{1,3})(?:\.(\d{1,2}))?$')
        .firstMatch(input.trim());
    if (match == null) return null;
    final bp =
        int.parse(match.group(1)!) * 100 +
        int.parse((match.group(2) ?? '').padRight(2, '0'));
    return bp > 10000 ? null : bp;
  }

  /// "2026-10-22 09:05". The caller passes the local time.
  static String dateTime(DateTime t) {
    String two(int n) => n.toString().padLeft(2, '0');
    return '${t.year}-${two(t.month)}-${two(t.day)} ${two(t.hour)}:${two(t.minute)}';
  }
}
