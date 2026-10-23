import 'package:flutter/material.dart';

import '../../../core/ids.dart';
import '../../../data/local/app_database.dart';
import '../data/local_inventory_repository.dart';
import '../domain/inventory_domain.dart';
import '../domain/stock_breakdown.dart';

/// One warehouse: balances built up from the ledger, and the movement list
/// with filters. A movement can be reversed from here (a new opposite entry
/// is added; the original stays).
class StockLedgerScreen extends StatefulWidget {
  const StockLedgerScreen({
    super.key,
    required this.db,
    required this.companyId,
    required this.warehouse,
    this.onChanged,
  });

  final AppDatabase db;
  final String companyId;
  final Warehouse warehouse;
  final VoidCallback? onChanged;

  @override
  State<StockLedgerScreen> createState() => _StockLedgerScreenState();
}

class _StockLedgerScreenState extends State<StockLedgerScreen> {
  late final LocalInventoryRepository _repo;
  List<StockBreakdownLine> _balances = [];
  List<MovementView> _moves = [];
  List<String> _problems = [];
  StockMovementType? _type;
  DateTimeRange? _range;
  String? _error;
  bool _loading = true;
  int _version = 0;

  @override
  void initState() {
    super.initState();
    _repo = LocalInventoryRepository(widget.db, companyId: widget.companyId);
    _load();
  }

  Future<void> _load() async {
    final v = ++_version;
    try {
      final balances = await _repo.breakdown(warehouseId: widget.warehouse.id);
      final range = _range;
      final moves = await _repo.ledger(
        warehouseId: widget.warehouse.id,
        type: _type,
        from: range?.start,
        to: range?.end.add(const Duration(days: 1)),
        limit: 200,
      );
      final problems = await _repo.reconcile();
      if (!mounted || v != _version) return;
      setState(() {
        _balances = balances;
        _moves = moves;
        _problems = problems;
        _error = null;
        _loading = false;
      });
    } catch (e) {
      if (!mounted || v != _version) return;
      setState(() {
        _error = 'Could not load the ledger: $e';
        _loading = false;
      });
    }
  }

  void _snack(String text) {
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(text)));
  }

  String _q(int e4) => formatQtyE4(e4);
  String _signed(int e4) => '${e4 > 0 ? '+' : ''}${formatQtyE4(e4)}';
  String _d(DateTime d) => formatTimestamp(d).substring(0, 10);

  Future<void> _pickRange() async {
    final now = DateTime.now();
    final picked = await showDateRangePicker(
      context: context,
      firstDate: DateTime(2000),
      lastDate: DateTime(now.year + 1),
      initialDateRange: _range,
    );
    if (picked == null || !mounted) return;
    setState(() => _range = picked);
    await _load();
  }

  Future<void> _setType(StockMovementType? t) async {
    setState(() => _type = t);
    await _load();
  }

  Future<void> _clearRange() async {
    setState(() => _range = null);
    await _load();
  }

  Future<void> _reverse(MovementView m) async {
    // Not disposed on purpose: the dialog is still animating out when the
    // future completes, and a disposed controller would throw there.
    final reason = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Reverse this movement?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${m.type.label} of ${_q(m.qtyE4.abs())} for ${m.productName}. '
              'A new opposite entry is added. The original stays in the ledger.',
            ),
            const SizedBox(height: 12),
            TextField(
              controller: reason,
              maxLength: 150,
              decoration: const InputDecoration(
                labelText: 'Reason (optional)',
                border: OutlineInputBorder(),
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Reverse'),
          ),
        ],
      ),
    );
    final note = reason.text.trim();
    if (ok != true || !mounted) return;
    try {
      await _repo.reverse(
        movementId: m.id,
        requestId: generateUuidV4(),
        actor: 'Owner',
        reason: note.isEmpty ? null : note,
      );
      widget.onChanged?.call();
      await _load();
      if (!mounted) return;
      _snack('Movement reversed.');
    } on InventoryException catch (e) {
      if (!mounted) return;
      _snack(e.message);
    } catch (e) {
      if (!mounted) return;
      _snack('Could not reverse: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: Text(
            'Stock ledger - ${widget.warehouse.name}',
            overflow: TextOverflow.ellipsis,
          ),
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Balances'),
              Tab(text: 'Movements'),
            ],
          ),
        ),
        body: _loading
            ? const Center(child: CircularProgressIndicator())
            : TabBarView(
                children: [_balancesTab(scheme), _movementsTab(scheme)],
              ),
      ),
    );
  }

  Widget _balancesTab(ColorScheme scheme) {
    return ListView(
      padding: const EdgeInsets.all(12),
      children: [
        if (_error != null)
          Text(_error!, style: TextStyle(color: scheme.error)),
        if (_problems.isNotEmpty)
          Card(
            color: scheme.errorContainer,
            margin: const EdgeInsets.only(bottom: 8),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Text(
                'The ledger and the balances do not match:\n'
                '${_problems.take(3).join('\n')}',
                style: TextStyle(color: scheme.onErrorContainer),
              ),
            ),
          ),
        if (_balances.isEmpty)
          const Padding(
            padding: EdgeInsets.all(24),
            child: Center(
              child: Text('No stock movements in this warehouse yet.'),
            ),
          )
        else
          Card(
            margin: EdgeInsets.zero,
            clipBehavior: Clip.antiAlias,
            child: Column(
              children: [
                for (var i = 0; i < _balances.length; i++) ...[
                  if (i > 0) const Divider(height: 1),
                  _balanceTile(_balances[i]),
                ],
              ],
            ),
          ),
      ],
    );
  }

  // Ledger responsive layout v1
  Widget _balanceTile(StockBreakdownLine b) {
    final unit = b.unitCode == null ? '' : ' ${b.unitCode}';
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            b.name,
            style: theme.textTheme.titleSmall,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
          ),
          const SizedBox(height: 4),
          Text('SKU ${b.sku}', style: theme.textTheme.bodySmall),
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 4,
            children: [
              Text('Opening ${_q(b.openingE4)}'),
              Text('Received ${_q(b.receivedE4)}'),
              Text('Issued ${_q(-b.issuedE4)}'),
              Text('Adjusted ${_signed(b.otherE4)}'),
            ],
          ),
          const SizedBox(height: 8),
          Align(
            alignment: Alignment.centerRight,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                Text('On hand', style: theme.textTheme.labelSmall),
                Text(
                  '${_q(b.onHandE4)}$unit',
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _movementsTab(ColorScheme scheme) {
    final range = _range;
    return ListView(
      padding: const EdgeInsets.all(12),
      children: [
        Wrap(
          spacing: 8,
          runSpacing: 4,
          children: [
            ChoiceChip(
              label: const Text('All'),
              selected: _type == null,
              onSelected: (_) => _setType(null),
            ),
            for (final t in StockMovementType.values)
              ChoiceChip(
                label: Text(t.label),
                selected: _type == t,
                onSelected: (_) => _setType(t),
              ),
          ],
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            OutlinedButton.icon(
              onPressed: _pickRange,
              icon: const Icon(Icons.date_range),
              label: Text(
                range == null
                    ? 'Any date'
                    : '${_d(range.start)} to ${_d(range.end)}',
              ),
            ),
            if (range != null)
              TextButton(onPressed: _clearRange, child: const Text('Clear')),
          ],
        ),
        const SizedBox(height: 8),
        if (_error != null)
          Text(_error!, style: TextStyle(color: scheme.error)),
        if (_moves.isEmpty)
          const Padding(
            padding: EdgeInsets.all(24),
            child: Center(child: Text('No movements match these filters.')),
          )
        else ...[
          Card(
            margin: EdgeInsets.zero,
            clipBehavior: Clip.antiAlias,
            child: Column(
              children: [
                for (var i = 0; i < _moves.length; i++) ...[
                  if (i > 0) const Divider(height: 1),
                  _moveTile(_moves[i], scheme),
                ],
              ],
            ),
          ),
          if (_moves.length >= 200)
            const Padding(
              padding: EdgeInsets.only(top: 8),
              child: Text('Showing the latest 200. Use the filters to narrow.'),
            ),
        ],
      ],
    );
  }

  String _detail(MovementView m) {
    final parts = <String>[m.warehouseName, 'by ${m.createdBy}'];
    final cost = m.unitCostE4;
    if (cost != null) parts.add('cost ${formatQtyE4(cost, minDecimals: 2)}');
    final note = m.reason ?? '';
    if (note.isNotEmpty) parts.add(note);
    return parts.join('  |  ');
  }

  Widget _moveTile(MovementView m, ColorScheme scheme) {
    final theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  m.productName,
                  style: theme.textTheme.titleSmall,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 4),
                Text(
                  '${m.type.label}${m.isReversed ? ' (reversed)' : ''}\n'
                  '${formatTimestamp(m.occurredAt.toLocal())}',
                  style: theme.textTheme.bodySmall,
                ),
                const SizedBox(height: 4),
                Text(
                  _detail(m),
                  style: theme.textTheme.bodySmall,
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                _signed(m.qtyE4),
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  color: m.qtyE4 < 0 ? scheme.error : null,
                ),
              ),
              if (m.canReverse)
                IconButton(
                  tooltip: 'Reverse this movement',
                  icon: const Icon(Icons.undo),
                  visualDensity: VisualDensity.compact,
                  constraints: const BoxConstraints(
                    minWidth: 40,
                    minHeight: 40,
                  ),
                  padding: EdgeInsets.zero,
                  onPressed: () => _reverse(m),
                ),
            ],
          ),
        ],
      ),
    );
  }
}