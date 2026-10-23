import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../core/ids.dart';
import '../data/local/local_returns_repository.dart';
import '../domain/pos_format.dart';
import 'return_receipt_text.dart';

/// Saves a return. The screen passes the repository's processReturn, tests
/// pass a fake, so this dialog never needs a database of its own.
typedef ReturnSubmit = Future<ReturnReceipt> Function(ReturnRequest request);

String _rs(int e4) => 'Rs ${PosFormat.money(e4)}';

class _Preview {
  const _Preview(this.quote, this.problem);

  final ReturnQuote? quote;
  final String? problem;
}

/// Choose what comes back, check the refund, confirm. Pops the saved
/// [ReturnReceipt] on success and null when cancelled.
class ReturnDialog extends StatefulWidget {
  const ReturnDialog({
    super.key,
    required this.sale,
    required this.onSubmit,
    required this.defaultCashier,
    this.requestId,
  });

  final ReturnableSale sale;
  final ReturnSubmit onSubmit;
  final String defaultCashier;

  /// One id per attempt: posting the same dialog twice cannot create two
  /// returns. Tests may pass a fixed value.
  final String? requestId;

  @override
  State<ReturnDialog> createState() => _ReturnDialogState();
}

class _ReturnDialogState extends State<ReturnDialog> {
  late final String _requestId = widget.requestId ?? generateUuidV4();
  final Map<String, TextEditingController> _qty = {};
  final Map<String, bool> _restock = {};
  final TextEditingController _reason = TextEditingController();
  late final TextEditingController _method;
  late final TextEditingController _cashier;
  bool _review = false;
  bool _busy = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _method = TextEditingController(text: widget.sale.paymentMethod);
    _cashier = TextEditingController(text: widget.defaultCashier);
    for (final line in widget.sale.lines) {
      if (line.remainingQtyE4 > 0) {
        _qty[line.saleLineId] = TextEditingController();
        _restock[line.saleLineId] = true;
      }
    }
  }

  @override
  void dispose() {
    for (final c in _qty.values) {
      c.dispose();
    }
    _reason.dispose();
    _method.dispose();
    _cashier.dispose();
    super.dispose();
  }

  Map<String, int> _readQuantities() {
    final out = <String, int>{};
    for (final line in widget.sale.lines) {
      final c = _qty[line.saleLineId];
      if (c == null) continue;
      final text = c.text.trim();
      if (text.isEmpty) continue;
      final q = PosFormat.parseE4(text);
      if (q == null) {
        throw ReturnException('Enter a valid quantity for ${line.name}.');
      }
      if (q > 0) out[line.saleLineId] = q;
    }
    return out;
  }

  _Preview _preview() {
    try {
      final quantities = _readQuantities();
      if (quantities.isEmpty) return const _Preview(null, null);
      return _Preview(widget.sale.quote(quantities), null);
    } on ReturnException catch (e) {
      return _Preview(null, e.message);
    }
  }

  void _returnAll() {
    setState(() {
      _error = null;
      for (final line in widget.sale.lines) {
        final c = _qty[line.saleLineId];
        if (c != null) c.text = PosFormat.plain(line.remainingQtyE4);
      }
    });
  }

  void _toReview() {
    final preview = _preview();
    String? problem = preview.problem;
    if (problem == null && preview.quote == null) {
      problem = 'Choose at least one item to return.';
    }
    if (problem == null && _reason.text.trim().isEmpty) {
      problem = 'Enter the reason for the return.';
    }
    if (problem == null && _reason.text.trim().length > 120) {
      problem = 'The reason can be up to 120 characters.';
    }
    if (problem == null && _method.text.trim().isEmpty) {
      problem = 'Enter the refund method.';
    }
    if (problem == null && _cashier.text.trim().isEmpty) {
      problem = 'Enter the cashier name.';
    }
    setState(() {
      _error = problem;
      if (problem == null) _review = true;
    });
  }

  Future<void> _confirm() async {
    if (_busy) return;
    final quote = _preview().quote;
    if (quote == null) {
      setState(() {
        _review = false;
        _error = 'Choose at least one item to return.';
      });
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final receipt = await widget.onSubmit(
        ReturnRequest(
          clientRequestId: _requestId,
          saleId: widget.sale.saleId,
          lines: [
            for (final l in quote.lines)
              ReturnLineRequest(
                saleLineId: l.saleLineId,
                quantityE4: l.quantityE4,
                restock: _restock[l.saleLineId] ?? true,
              ),
          ],
          refundMethod: _method.text.trim(),
          reason: _reason.text.trim(),
          createdBy: _cashier.text.trim(),
        ),
      );
      if (!mounted) return;
      Navigator.of(context).pop(receipt);
    } on ReturnException catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _busy = false;
        _error = 'The return could not be saved: $e';
      });
    }
  }

  Widget _row(String label, String value, {bool bold = false}) {
    final style = bold ? const TextStyle(fontWeight: FontWeight.w700) : null;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text(value, style: style),
        ],
      ),
    );
  }

  Widget _errorText(String message) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Text(
        message,
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      ),
    );
  }

  Widget _lineEditor(ReturnableLine line) {
    final theme = Theme.of(context);
    final left = line.remainingQtyE4;
    final sub =
        'Sold ${PosFormat.plain(line.soldQtyE4)}  -  '
        'returned ${PosFormat.plain(line.returnedQtyE4)}  -  '
        'left ${PosFormat.plain(left)}';
    if (left <= 0) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 6),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(line.name, style: theme.textTheme.titleSmall),
            Text(
              '$sub. Nothing left to return.',
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
      );
    }
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(line.name, style: theme.textTheme.titleSmall),
          Text(sub, style: theme.textTheme.bodySmall),
          const SizedBox(height: 6),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              SizedBox(
                width: 110,
                child: TextField(
                  key: ValueKey('return-qty-${line.saleLineId}'),
                  controller: _qty[line.saleLineId],
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  onChanged: (_) => setState(() => _error = null),
                  decoration: const InputDecoration(
                    labelText: 'Return qty',
                    border: OutlineInputBorder(),
                    isDense: true,
                  ),
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Switch(
                    key: ValueKey('return-restock-${line.saleLineId}'),
                    value: _restock[line.saleLineId] ?? true,
                    onChanged: (v) =>
                        setState(() => _restock[line.saleLineId] = v),
                  ),
                  const Text('Put back in stock'),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _previewCard(ReturnQuote quote) {
    return Card(
      margin: const EdgeInsets.only(top: 8),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _row('Goods', _rs(quote.netE4)),
            if (quote.taxE4 > 0) _row('Tax', _rs(quote.taxE4)),
            _row('Return value', _rs(quote.valueE4), bold: true),
            _row('Refund now', _rs(quote.refundE4), bold: true),
            if (quote.creditE4 > 0) ...[
              _row('Reduces balance due', _rs(quote.creditE4)),
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'The customer had not paid in full, so part of this '
                  'return reduces what they still owe.',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  List<Widget> _editBody(_Preview preview) {
    final theme = Theme.of(context);
    final message = preview.problem ?? _error;
    return [
      Text(
        'Sale ${widget.sale.receiptNumber}',
        style: theme.textTheme.titleSmall,
      ),
      const SizedBox(height: 4),
      Text(
        'Choose how many of each item come back. The goods and their tax '
        'are refunded.',
        style: theme.textTheme.bodySmall,
      ),
      const SizedBox(height: 8),
      for (final line in widget.sale.lines) _lineEditor(line),
      Align(
        alignment: Alignment.centerLeft,
        child: TextButton(
          key: const Key('return-all'),
          onPressed: _returnAll,
          child: const Text('Return everything that is left'),
        ),
      ),
      if (preview.quote != null) _previewCard(preview.quote!),
      const SizedBox(height: 12),
      TextField(
        key: const Key('return-reason'),
        controller: _reason,
        maxLength: 120,
        onChanged: (_) => setState(() => _error = null),
        decoration: const InputDecoration(
          labelText: 'Reason for the return',
          border: OutlineInputBorder(),
          isDense: true,
        ),
      ),
      const SizedBox(height: 8),
      TextField(
        key: const Key('return-method'),
        controller: _method,
        onChanged: (_) => setState(() => _error = null),
        decoration: const InputDecoration(
          labelText: 'Refund method',
          border: OutlineInputBorder(),
          isDense: true,
        ),
      ),
      const SizedBox(height: 8),
      TextField(
        key: const Key('return-cashier'),
        controller: _cashier,
        onChanged: (_) => setState(() => _error = null),
        decoration: const InputDecoration(
          labelText: 'Cashier',
          border: OutlineInputBorder(),
          isDense: true,
        ),
      ),
      if (message != null) _errorText(message),
    ];
  }

  List<Widget> _reviewBody(ReturnQuote? quote) {
    final theme = Theme.of(context);
    if (quote == null) return [const Text('Nothing to return.')];
    final byId = {for (final l in widget.sale.lines) l.saleLineId: l};
    return [
      Text(
        'Sale ${widget.sale.receiptNumber}',
        style: theme.textTheme.titleSmall,
      ),
      const SizedBox(height: 8),
      for (final q in quote.lines)
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(byId[q.saleLineId]?.name ?? ''),
                    Text(
                      '${PosFormat.plain(q.quantityE4)} returned  -  '
                      '${(_restock[q.saleLineId] ?? true) ? 'put back in stock' : 'not put back in stock'}',
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              Text(_rs(q.netE4 + q.taxE4)),
            ],
          ),
        ),
      const Divider(height: 20),
      _row('Return value', _rs(quote.valueE4)),
      _row('Refund (${_method.text.trim()})', _rs(quote.refundE4), bold: true),
      if (quote.creditE4 > 0) _row('Reduces balance due', _rs(quote.creditE4)),
      const SizedBox(height: 8),
      Text('Reason: ${_reason.text.trim()}'),
      Text('Cashier: ${_cashier.text.trim()}'),
      const SizedBox(height: 8),
      Text(
        'A saved return cannot be edited or deleted afterwards.',
        style: theme.textTheme.bodySmall,
      ),
      if (_error != null) _errorText(_error!),
    ];
  }

  @override
  Widget build(BuildContext context) {
    final preview = _preview();
    return PopScope(
      canPop: !_busy,
      child: AlertDialog(
        title: Text(_review ? 'Confirm return' : 'Return items'),
        content: SizedBox(
          width: 440,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: _review
                  ? _reviewBody(preview.quote)
                  : _editBody(preview),
            ),
          ),
        ),
        actions: _review
            ? [
                TextButton(
                  key: const Key('return-back'),
                  onPressed: _busy
                      ? null
                      : () => setState(() {
                          _review = false;
                          _error = null;
                        }),
                  child: const Text('Back'),
                ),
                FilledButton(
                  key: const Key('return-confirm'),
                  onPressed: _busy ? null : _confirm,
                  child: _busy
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : const Text('Confirm refund'),
                ),
              ]
            : [
                TextButton(
                  onPressed: _busy ? null : () => Navigator.of(context).pop(),
                  child: const Text('Cancel'),
                ),
                FilledButton(
                  key: const Key('return-review'),
                  onPressed: _busy ? null : _toReview,
                  child: const Text('Review return'),
                ),
              ],
      ),
    );
  }
}

/// Shown after a return is saved: the return receipt, ready to copy.
class ReturnReceiptDialog extends StatefulWidget {
  const ReturnReceiptDialog({
    super.key,
    required this.receipt,
    this.businessName,
    this.footer,
  });

  final ReturnReceipt receipt;
  final String? businessName;
  final String? footer;

  @override
  State<ReturnReceiptDialog> createState() => _ReturnReceiptDialogState();
}

class _ReturnReceiptDialogState extends State<ReturnReceiptDialog> {
  bool _copied = false;

  String get _text => buildReturnReceiptText(
    widget.receipt,
    businessName: widget.businessName,
    footer: widget.footer,
  );

  Future<void> _copy() async {
    await Clipboard.setData(ClipboardData(text: _text));
    if (!mounted) return;
    setState(() => _copied = true);
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('Return recorded'),
      content: SizedBox(
        width: 440,
        child: SingleChildScrollView(
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SelectableText(
              _text,
              style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
            ),
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Close'),
        ),
        FilledButton.icon(
          onPressed: _copy,
          icon: Icon(_copied ? Icons.check : Icons.copy),
          label: Text(_copied ? 'Copied' : 'Copy receipt'),
        ),
      ],
    );
  }
}
