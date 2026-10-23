import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../../../data/local/app_database.dart';
import '../data/local/local_pos_settings_repository.dart';
import '../data/local/local_returns_repository.dart';
import '../data/local/local_sales_history_repository.dart';
import '../domain/pos_format.dart';
import 'pos_receipt_text.dart';
import 'return_dialog.dart';
import 'return_receipt_text.dart';

String _statusLabel(String status) {
  switch (status) {
    case 'completed':
      return 'Completed';
    case 'partially_returned':
      return 'Partly returned';
    case 'returned':
      return 'Returned';
    case 'voided':
      return 'Voided';
    default:
      return status;
  }
}

/// The text that is copied when a saved sale is reprinted. It is the saved
/// receipt with a duplicate marker, so nobody mistakes it for the original.
String buildReprintText(
  SaleDetail detail, {
  String? businessName,
  String? footer,
}) {
  final head = StringBuffer('*** REPRINT - DUPLICATE COPY ***\n');
  if (detail.receipt.status != 'completed') {
    head.writeln('Status: ${_statusLabel(detail.receipt.status)}');
  }
  final body = buildReceiptText(
    detail.receipt,
    businessName: businessName,
    footer: footer,
  );
  return '$head$body';
}

enum _Range { today, yesterday, week, month, all, custom }

class SalesHistoryScreen extends StatefulWidget {
  const SalesHistoryScreen({
    super.key,
    required this.db,
    required this.companyId,
    this.businessName,
  });

  final AppDatabase db;
  final String companyId;
  final String? businessName;

  @override
  State<SalesHistoryScreen> createState() => _SalesHistoryScreenState();
}

class _SalesHistoryScreenState extends State<SalesHistoryScreen> {
  static const int _pageSize = 50;

  late final LocalSalesHistoryRepository _repo;
  late final LocalPosSettingsRepository _settingsRepo;
  late final LocalReturnsRepository _returnsRepo;
  final _search = TextEditingController();
  Timer? _debounce;
  int _version = 0;

  _Range _range = _Range.today;
  DateTimeRange? _custom;
  String? _method;
  final Set<String> _knownMethods = <String>{'Cash'};

  List<SaleRow> _sales = [];
  SalesSummary _summary = SalesSummary.empty;
  List<DailyTotal> _daily = [];
  ReturnsSummary _returns = ReturnsSummary.empty;
  bool _loading = true;
  bool _loadingMore = false;
  bool _hasMore = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _repo = LocalSalesHistoryRepository(widget.db, companyId: widget.companyId);
    _settingsRepo = LocalPosSettingsRepository(
      widget.db,
      companyId: widget.companyId,
    );
    _returnsRepo = LocalReturnsRepository(
      widget.db,
      companyId: widget.companyId,
    );
    _load();
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _search.dispose();
    super.dispose();
  }

  static DateTime _day(DateTime d, int add) =>
      DateTime(d.year, d.month, d.day + add);

  (DateTime?, DateTime?) _bounds() {
    final now = DateTime.now();
    final today = _day(now, 0);
    switch (_range) {
      case _Range.today:
        return (today, _day(today, 1));
      case _Range.yesterday:
        return (_day(today, -1), today);
      case _Range.week:
        return (_day(today, -6), _day(today, 1));
      case _Range.month:
        return (
          DateTime(now.year, now.month, 1),
          DateTime(now.year, now.month + 1, 1),
        );
      case _Range.all:
        return (null, null);
      case _Range.custom:
        final c = _custom;
        if (c == null) return (null, null);
        return (_day(c.start, 0), _day(c.end, 1));
    }
  }

  SalesFilter _filter() {
    final (from, to) = _bounds();
    return SalesFilter(
      text: _search.text,
      from: from,
      toExclusive: to,
      paymentMethod: _method,
    );
  }

  Future<void> _load() async {
    final v = ++_version;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final filter = _filter();
      final sales = await _repo.search(filter, limit: _pageSize);
      final summary = await _repo.summary(filter);
      final daily = await _repo.dailyTotals(filter);
      final returns = await _returnsRepo.summary(
        from: filter.from,
        toExclusive: filter.toExclusive,
        refundMethod: filter.paymentMethod,
      );
      if (!mounted || v != _version) return;
      setState(() {
        _sales = sales;
        _summary = summary;
        _daily = daily;
        _returns = returns;
        _hasMore = sales.length == _pageSize;
        _knownMethods.addAll(summary.byMethod.map((m) => m.method));
        if (_method != null) _knownMethods.add(_method!);
        _loading = false;
      });
    } catch (e) {
      if (!mounted || v != _version) return;
      setState(() {
        _loading = false;
        _error = 'Could not load sales: $e';
      });
    }
  }

  Future<void> _loadMore() async {
    if (_loadingMore) return;
    final v = _version;
    setState(() => _loadingMore = true);
    try {
      final more = await _repo.search(
        _filter(),
        limit: _pageSize,
        offset: _sales.length,
      );
      if (!mounted || v != _version) return;
      setState(() {
        _sales = [..._sales, ...more];
        _hasMore = more.length == _pageSize;
        _loadingMore = false;
      });
    } catch (e) {
      if (!mounted || v != _version) return;
      setState(() {
        _loadingMore = false;
        _error = 'Could not load more sales: $e';
      });
    }
  }

  void _onSearchChanged(String _) {
    setState(() {});
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 300), _load);
  }

  void _pickRange(_Range r) {
    setState(() => _range = r);
    _load();
  }

  Future<void> _pickCustom() async {
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
      initialDateRange: _custom,
    );
    if (picked == null || !mounted) return;
    setState(() {
      _custom = picked;
      _range = _Range.custom;
    });
    _load();
  }

  Future<void> _open(SaleRow sale) async {
    try {
      final detail = await _repo.detail(sale.id);
      final config = await _settingsRepo.load();
      final returns = await _returnsRepo.returnsForSale(sale.id);
      final returnable = await _returnsRepo.returnable(sale.id);
      if (!mounted) return;
      final changed = await showDialog<bool>(
        context: context,
        builder: (_) => _SaleDetailDialog(
          detail: detail,
          returns: returns,
          returnable: returnable,
          onSubmitReturn: _returnsRepo.processReturn,
          businessName: widget.businessName,
          footer: config.receiptFooter,
        ),
      );
      if (changed == true && mounted) {
        await _load();
      }
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = 'Could not open this sale: $e');
    }
  }

  static String _d(DateTime t) => PosFormat.dateTime(t).substring(0, 10);
  static String _rs(int e4) => 'Rs ${PosFormat.money(e4)}';

  String _rangeText() {
    final (from, to) = _bounds();
    if (from == null || to == null) return 'All dates';
    final last = _day(to, -1);
    return from == last ? _d(from) : '${_d(from)} to ${_d(last)}';
  }

  Widget _chip(String label, bool selected, VoidCallback onTap) => ChoiceChip(
    label: Text(label),
    selected: selected,
    onSelected: (_) => onTap(),
  );

  Widget _filters() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        TextField(
          controller: _search,
          textInputAction: TextInputAction.search,
          onChanged: _onSearchChanged,
          onSubmitted: (_) => _load(),
          decoration: InputDecoration(
            prefixIcon: const Icon(Icons.search),
            hintText: 'Receipt number, cashier or product',
            border: const OutlineInputBorder(),
            isDense: true,
            suffixIcon: _search.text.isEmpty
                ? null
                : IconButton(
                    tooltip: 'Clear',
                    icon: const Icon(Icons.clear),
                    onPressed: () {
                      _search.clear();
                      _onSearchChanged('');
                    },
                  ),
          ),
        ),
        const SizedBox(height: 8),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final chip in <Widget>[
                _chip(
                  'Today',
                  _range == _Range.today,
                  () => _pickRange(_Range.today),
                ),
                _chip(
                  'Yesterday',
                  _range == _Range.yesterday,
                  () => _pickRange(_Range.yesterday),
                ),
                _chip(
                  'Last 7 days',
                  _range == _Range.week,
                  () => _pickRange(_Range.week),
                ),
                _chip(
                  'This month',
                  _range == _Range.month,
                  () => _pickRange(_Range.month),
                ),
                _chip(
                  'All',
                  _range == _Range.all,
                  () => _pickRange(_Range.all),
                ),
                ActionChip(
                  avatar: const Icon(Icons.date_range, size: 18),
                  label: Text(
                    _range == _Range.custom ? _rangeText() : 'Pick dates',
                  ),
                  onPressed: _pickCustom,
                ),
              ])
                Padding(padding: const EdgeInsets.only(right: 8), child: chip),
            ],
          ),
        ),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              for (final chip in <Widget>[
                _chip('Any method', _method == null, () {
                  setState(() => _method = null);
                  _load();
                }),
                for (final m in (_knownMethods.toList()..sort()))
                  _chip(m, _method == m, () {
                    setState(() => _method = m);
                    _load();
                  }),
              ])
                Padding(padding: const EdgeInsets.only(right: 8), child: chip),
            ],
          ),
        ),
      ],
    );
  }

  Widget _stat(String label, String value, {Color? color}) {
    final theme = Theme.of(context);
    return SizedBox(
      width: 150,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: theme.textTheme.bodySmall),
          Text(
            value,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: color,
            ),
          ),
        ],
      ),
    );
  }

  Widget _summaryCard() {
    final s = _summary;
    final theme = Theme.of(context);
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(_rangeText(), style: theme.textTheme.labelLarge),
            const SizedBox(height: 10),
            Wrap(
              spacing: 16,
              runSpacing: 12,
              children: [
                _stat('Sales', '${s.saleCount}'),
                _stat('Total', _rs(s.totalE4)),
                _stat('Received', _rs(s.paidE4)),
                if (s.outstandingE4 > 0)
                  _stat(
                    'Still due',
                    _rs(s.outstandingE4),
                    color: theme.colorScheme.error,
                  ),
                if (s.discountE4 > 0) _stat('Discounts', _rs(s.discountE4)),
                if (s.taxE4 > 0) _stat('Tax', _rs(s.taxE4)),
              ],
            ),
            if (s.byMethod.isNotEmpty) ...[
              const Divider(height: 24),
              Text('By payment method', style: theme.textTheme.labelLarge),
              const SizedBox(height: 6),
              for (final m in s.byMethod)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          '${m.method}  (${m.paymentCount} '
                          '${m.paymentCount == 1 ? 'payment' : 'payments'})',
                        ),
                      ),
                      Text(_rs(m.amountE4)),
                    ],
                  ),
                ),
            ],
            if (_returns.count > 0) ...[
              const Divider(height: 24),
              Text(
                'Returns made in this period',
                style: theme.textTheme.labelLarge,
              ),
              const SizedBox(height: 10),
              Wrap(
                spacing: 16,
                runSpacing: 12,
                children: [
                  _stat('Returns', '${_returns.count}'),
                  _stat('Goods returned', _rs(_returns.valueE4)),
                  _stat('Refunded', _rs(_returns.refundE4)),
                  _stat('Net sales', _rs(s.totalE4 - _returns.valueE4)),
                ],
              ),
              for (final m
                  in (_returns.refundsByMethod.entries.toList()
                    ..sort((a, b) => a.key.compareTo(b.key))))
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Row(
                    children: [
                      Expanded(child: Text('Refunded in ${m.key}')),
                      Text(_rs(m.value)),
                    ],
                  ),
                ),
              const SizedBox(height: 6),
              Text(
                'Net sales = sales total minus goods returned in this period.',
                style: theme.textTheme.bodySmall,
              ),
            ],
            if (s.excludedCount > 0) ...[
              const SizedBox(height: 8),
              Text(
                '${s.excludedCount} voided '
                '${s.excludedCount == 1 ? 'sale is' : 'sales are'} not counted '
                'in these totals.',
                style: theme.textTheme.bodySmall,
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _dailyCard() {
    return Card(
      margin: EdgeInsets.zero,
      child: ExpansionTile(
        title: Text('Daily totals (${_daily.length} days)'),
        children: [
          for (final d in _daily)
            ListTile(
              dense: true,
              title: Text(_d(d.day)),
              subtitle: Text(
                '${d.saleCount} ${d.saleCount == 1 ? 'sale' : 'sales'}',
              ),
              trailing: Text(_rs(d.totalE4)),
            ),
        ],
      ),
    );
  }

  Widget _tile(SaleRow sale) {
    final due = sale.totalE4 - sale.amountPaidE4;
    final scheme = Theme.of(context).colorScheme;
    final extra = due > 0 || sale.status != 'completed';
    return ListTile(
      onTap: () => _open(sale),
      isThreeLine: extra,
      title: Text(sale.receiptNumber),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${PosFormat.dateTime(sale.createdAt.toLocal())}  -  '
            '${sale.createdBy}  -  ${sale.paymentMethod}',
          ),
          if (extra)
            Text(
              [
                if (sale.status != 'completed') _statusLabel(sale.status),
                if (due > 0) 'Due ${_rs(due)}',
              ].join('  -  '),
              style: TextStyle(color: scheme.error),
            ),
        ],
      ),
      trailing: Text(
        _rs(sale.totalE4),
        style: const TextStyle(fontWeight: FontWeight.w700),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final children = <Widget>[
      _filters(),
      const SizedBox(height: 12),
      if (_error != null)
        Padding(
          padding: const EdgeInsets.only(bottom: 8),
          child: Text(
            _error!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        ),
      _summaryCard(),
      if (_daily.length > 1) ...[const SizedBox(height: 8), _dailyCard()],
      const SizedBox(height: 8),
      if (_loading)
        const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        )
      else if (_sales.isEmpty)
        const Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: Text('No sales match these filters.')),
        )
      else ...[
        for (final sale in _sales) _tile(sale),
        if (_hasMore)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 8),
            child: Center(
              child: _loadingMore
                  ? const CircularProgressIndicator()
                  : OutlinedButton(
                      onPressed: _loadMore,
                      child: const Text('Load more'),
                    ),
            ),
          ),
      ],
    ];
    return Material(
      type: MaterialType.transparency,
      child: SafeArea(
        bottom: false,
        child: ListView(padding: const EdgeInsets.all(12), children: children),
      ),
    );
  }
}

class _SaleDetailDialog extends StatefulWidget {
  const _SaleDetailDialog({
    required this.detail,
    required this.returns,
    required this.returnable,
    required this.onSubmitReturn,
    required this.businessName,
    required this.footer,
  });

  final SaleDetail detail;
  final List<ReturnReceipt> returns;
  final ReturnableSale returnable;
  final ReturnSubmit onSubmitReturn;
  final String? businessName;
  final String? footer;

  @override
  State<_SaleDetailDialog> createState() => _SaleDetailDialogState();
}

class _SaleDetailDialogState extends State<_SaleDetailDialog> {
  bool _copied = false;
  String? _copiedReturnId;

  Future<void> _startReturn() async {
    final receipt = await showDialog<ReturnReceipt>(
      context: context,
      barrierDismissible: false,
      builder: (_) => ReturnDialog(
        sale: widget.returnable,
        defaultCashier: widget.detail.createdBy,
        onSubmit: widget.onSubmitReturn,
      ),
    );
    if (receipt == null || !mounted) return;
    await showDialog<void>(
      context: context,
      builder: (_) => ReturnReceiptDialog(
        receipt: receipt,
        businessName: widget.businessName,
        footer: widget.footer,
      ),
    );
    if (!mounted) return;
    Navigator.of(context).pop(true);
  }

  Future<void> _copyReturn(ReturnReceipt r) async {
    final text = buildReturnReceiptText(
      r,
      businessName: widget.businessName,
      footer: widget.footer,
    );
    await Clipboard.setData(ClipboardData(text: text));
    if (!mounted) return;
    setState(() => _copiedReturnId = r.id);
  }

  Widget _returnTile(ReturnReceipt r) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(PosFormat.dateTime(r.createdAt.toLocal())),
                Text(
                  'Goods Rs ${PosFormat.money(r.totalE4)}  -  '
                  'Refund Rs ${PosFormat.money(r.refundE4)} (${r.refundMethod})',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
                Text(
                  'Reason: ${r.reason}',
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ),
          ),
          IconButton(
            tooltip: 'Copy return receipt',
            icon: Icon(_copiedReturnId == r.id ? Icons.check : Icons.copy),
            onPressed: () => _copyReturn(r),
          ),
        ],
      ),
    );
  }

  Future<void> _copy() async {
    final text = buildReprintText(
      widget.detail,
      businessName: widget.businessName,
      footer: widget.footer,
    );
    await Clipboard.setData(ClipboardData(text: text));
    if (!mounted) return;
    setState(() => _copied = true);
  }

  Widget _info(String label, String value) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 2),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: 96, child: Text(label)),
        Expanded(child: Text(value)),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) {
    final d = widget.detail;
    final r = d.receipt;
    final receiptText = buildReceiptText(
      r,
      businessName: widget.businessName,
      footer: widget.footer,
    );
    return AlertDialog(
      title: Text('Receipt ${r.receiptNumber}'),
      content: SizedBox(
        width: 440,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              _info('Date', PosFormat.dateTime(r.createdAt.toLocal())),
              _info('Cashier', d.createdBy),
              _info('Status', _statusLabel(r.status)),
              if (d.discountReason != null &&
                  d.discountReason!.trim().isNotEmpty)
                _info('Discount note', d.discountReason!),
              const Divider(height: 24),
              SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: SelectableText(
                  receiptText,
                  style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
                ),
              ),
              const Divider(height: 24),
              Text('Payments', style: Theme.of(context).textTheme.titleSmall),
              const SizedBox(height: 4),
              if (d.payments.isEmpty)
                const Text('No payment was recorded for this sale.')
              else
                for (final p in d.payments)
                  Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            '${p.method}  -  '
                            '${PosFormat.dateTime(p.receivedAt.toLocal())}',
                          ),
                        ),
                        Text('Rs ${PosFormat.money(p.amountE4)}'),
                      ],
                    ),
                  ),
              if (widget.returns.isNotEmpty) ...[
                const Divider(height: 24),
                Text('Returns', style: Theme.of(context).textTheme.titleSmall),
                const SizedBox(height: 4),
                for (final r in widget.returns) _returnTile(r),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Close'),
        ),
        if (widget.returnable.canReturn)
          OutlinedButton.icon(
            key: const Key('sale-return-button'),
            onPressed: _startReturn,
            icon: const Icon(Icons.assignment_return),
            label: const Text('Return'),
          ),
        FilledButton.icon(
          onPressed: _copy,
          icon: Icon(_copied ? Icons.check : Icons.copy),
          label: Text(_copied ? 'Copied' : 'Copy reprint'),
        ),
      ],
    );
  }
}
