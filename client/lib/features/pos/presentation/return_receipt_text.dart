import '../data/local/local_returns_repository.dart';
import '../domain/pos_format.dart';

/// A plain text return receipt, ready to copy. Same numbers as the saved return.
String buildReturnReceiptText(
  ReturnReceipt receipt, {
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
  if (name != null && name.isNotEmpty) {
    center(name);
  }
  center('RETURN RECEIPT');
  out.writeln('Return: ${receipt.returnNumber}');
  out.writeln('Original sale: ${receipt.saleReceiptNumber}');
  out.writeln('Date: ${PosFormat.dateTime(receipt.createdAt.toLocal())}');
  out.writeln('-' * width);

  for (final line in receipt.lines) {
    out.writeln(line.name);
    row('  ${PosFormat.plain(line.quantityE4)} returned', money(line.netE4));
    if (line.taxE4 > 0) {
      row('  Tax', money(line.taxE4));
    }
    out.writeln(
      line.restocked ? '  Put back in stock' : '  Not put back in stock',
    );
  }

  out.writeln('-' * width);
  row('Goods', money(receipt.netE4));
  if (receipt.taxE4 > 0) {
    row('Tax', money(receipt.taxE4));
  }
  row('RETURN VALUE', money(receipt.totalE4));
  row('Refund (${receipt.refundMethod})', money(receipt.refundE4));
  if (receipt.creditE4 > 0) {
    row('Credited to balance due', money(receipt.creditE4));
  }
  out.writeln('Reason: ${receipt.reason}');
  out.writeln('Cashier: ${receipt.createdBy}');

  final foot = footer?.trim();
  if (foot != null && foot.isNotEmpty) {
    out.writeln('-' * width);
    center(foot);
  }
  return out.toString();
}
