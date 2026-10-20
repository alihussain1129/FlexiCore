/// Amounts are stored as integer ten-thousandths ("E4"), matching the server's
/// numeric(18,4). Parsing and formatting never use floating point.
class Money {
  Money._();

  static final RegExp _amount = RegExp(r'^(\d{1,14})(?:\.(\d{1,4}))?$');

  /// "12" -> 120000, "12.5" -> 125000, "0.0001" -> 1. Null when invalid
  /// (empty, negative, more than 4 decimals, more than 14 whole digits).
  static int? parseE4(String input) {
    final match = _amount.firstMatch(input.trim());
    if (match == null) return null;
    final whole = int.parse(match.group(1)!);
    final fraction = (match.group(2) ?? '').padRight(4, '0');
    return whole * 10000 + int.parse(fraction);
  }

  /// 125000 -> "12.50", 123456 -> "12.3456". Always at least two decimals.
  static String formatE4(int value) {
    final whole = value ~/ 10000;
    var fraction = (value % 10000).toString().padLeft(4, '0');
    while (fraction.length > 2 && fraction.endsWith('0')) {
      fraction = fraction.substring(0, fraction.length - 1);
    }
    return '$whole.$fraction';
  }
}
