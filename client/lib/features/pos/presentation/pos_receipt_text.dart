import '../data/local/local_pos_repository.dart';
import '../domain/pos_format.dart';
import '../domain/pos_pricing.dart';

/// A plain text receipt, ready to copy. Same numbers as the saved sale.
String buildReceiptText(
  PosReceipt receipt, {
  String? businessName,
  String? footer,
  int width = 40,
}) {
  final out = StringBuffer();

  void center(String text) {
    final pad = (width - text.length) ~/ 2;
    final spaces = pad > 0 ? ' ' * pad : '';
    out.writeln('$spaces$text');
  }

  void row(String left, String right) {
    final gap = width - left.length - right.length;
    final spaces = gap > 0 ? ' ' * gap : ' ';
    out.writeln('$left$spaces$right');
  }

  String money(int value) => PosFormat.money(value);

  final name = businessName?.trim();
  if (name != null && name.isNotEmpty) center(name);
  center('SALES RECEIPT');
  out.writeln('Receipt: ${receipt.receiptNumber}');
  out.writeln('Date: ${PosFormat.dateTime(receipt.createdAt.toLocal())}');
  out.writeln('-' * width);

  for (final line in receipt.lines) {
    out.writeln(line.name);
    final gross = (line.quantityE4 * line.unitPriceE4 + 5000) ~/ 10000;
    row(
      '  ${PosFormat.plain(line.quantityE4)} x ${money(line.unitPriceE4)}',
      money(gross),
    );
    if (line.discountE4 > 0) {
      row('  Discount', '-${money(line.discountE4)}');
    }
  }

  out.writeln('-' * width);
  row('Subtotal', money(receipt.subtotalE4));
  if (receipt.discountE4 > 0) {
    row('Discount', '-${money(receipt.discountE4)}');
  }
  if (receipt.taxE4 > 0) {
    row(
      'Tax (${PosPricing.percentLabel(receipt.taxRateBp)})',
      money(receipt.taxE4),
    );
  }
  row('TOTAL', money(receipt.totalE4));
  row('Paid (${receipt.paymentMethod})', money(receipt.amountPaidE4));
  if (receipt.changeE4 > 0) row('Change', money(receipt.changeE4));
  final due = receipt.totalE4 - receipt.amountPaidE4;
  if (due > 0) row('Remaining due', money(due));

  final foot = footer?.trim();
  if (foot != null && foot.isNotEmpty) {
    out.writeln('-' * width);
    center(foot);
  }
  return out.toString();
}
