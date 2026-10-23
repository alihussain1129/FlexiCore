import 'package:flutter/material.dart';

import '../../../core/ids.dart';
import '../../../data/local/app_database.dart';
import '../data/local_inventory_repository.dart';
import '../domain/inventory_domain.dart';
import 'add_warehouse_dialog.dart';
import 'stock_ledger_screen.dart';

/// Stock tab: pick a product, then receive stock, enter opening stock, or
/// remove stock. Every save goes through [LocalInventoryRepository.record],
/// which writes the ledger row and the balance in one transaction.
class StockInScreen extends StatefulWidget {
  const StockInScreen({
    super.key,
    required this.db,
    required this.companyId,
    this.onChanged,
  });

  final AppDatabase db;
  final String companyId;

  /// Called after stock was saved, so other screens can reload.
  final VoidCallback? onChanged;

  @override
  State<StockInScreen> createState() => _StockInScreenState();
}

class _StockInScreenState extends State<StockInScreen> {
  late final LocalInventoryRepository _repo;
  final _search = TextEditingController();
  List<StockLine> _lines = [];
  List<MovementView> _recent = [];
  StockSummary? _summary;
  Warehouse? _warehouse;
  List<Warehouse> _warehouses = const [];
  static const _addWarehouseValue = '__add_warehouse__';
  String? _error;
  bool _loading = true;
  int _version = 0;

  @override
  void initState() {
    super.initState();
    _repo = LocalInventoryRepository(widget.db, companyId: widget.companyId);
    _search.addListener(_load);
    _init();
  }

  @override
  void dispose() {
    _search
      ..removeListener(_load)
      ..dispose();
    super.dispose();
  }

  Future<void> _init() async {
    try {
      final w = await _repo.ensureDefaultWarehouse();
      final all = await _repo.warehouses();
      if (!mounted) return;
      _warehouse = w;
      _warehouses = all;
      await _load();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not open stock: $e';
        _loading = false;
      });
    }
  }

  Future<void> _load() async {
    final w = _warehouse;
    if (w == null) return;
    final v = ++_version;
    try {
      final lines = await _repo.stockLines(
        warehouseId: w.id,
        search: _search.text,
      );
      final recent = await _repo.recentMovements(limit: 8);
      final summary = await _repo.summary();
      if (!mounted || v != _version) return;
      setState(() {
        _lines = lines;
        _recent = recent;
        _summary = summary;
        _error = null;
        _loading = false;
      });
    } catch (e) {
      if (!mounted || v != _version) return;
      setState(() {
        _error = 'Could not load stock: $e';
        _loading = false;
      });
    }
  }

  Future<void> _open(StockLine line, StockMovementType type) async {
    final w = _warehouse;
    if (w == null) return;
    final saved = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      useSafeArea: true,
      showDragHandle: true,
      builder: (context) =>
          _MovementSheet(repo: _repo, warehouse: w, line: line, type: type),
    );
    if (saved != true || !mounted) return;
    await _load();
    widget.onChanged?.call();
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text('Stock saved for ${line.name}.')));
  }

  Future<void> _chooseWarehouse(String value) async {
    if (value == _addWarehouseValue) {
      final created = await showDialog<Warehouse>(
        context: context,
        builder: (context) => AddWarehouseDialog(repo: _repo),
      );
      if (created == null || !mounted) return;
      final all = await _repo.warehouses();
      if (!mounted) return;
      setState(() {
        _warehouses = all;
        _warehouse = created;
        _loading = true;
      });
      await _load();
      return;
    }
    final w = _warehouses.where((x) => x.id == value).firstOrNull;
    if (w == null) return;
    setState(() {
      _warehouse = w;
      _loading = true;
    });
    await _load();
  }

  Future<void> _openLedger() async {
    final w = _warehouse;
    if (w == null) return;
    await Navigator.of(context).push(
      MaterialPageRoute<void>(
        builder: (context) => StockLedgerScreen(
          db: widget.db,
          companyId: widget.companyId,
          warehouse: w,
          onChanged: widget.onChanged,
        ),
      ),
    );
    if (!mounted) return;
    await _load();
  }

  String _qty(int e4) => formatQtyE4(e4);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final summary = _summary;
    return Scaffold(
      appBar: AppBar(
        actions: [
          IconButton(
            tooltip: 'Stock ledger',
            onPressed: _warehouse == null ? null : _openLedger,
            icon: const Icon(Icons.receipt_long_outlined),
          ),
        ],
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Stock'),
            if (_warehouse != null)
              Text(_warehouse!.name, style: theme.textTheme.labelSmall),
          ],
        ),
      ),
      body: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          TextField(
            controller: _search,
            decoration: InputDecoration(
              labelText: 'Search products',
              hintText: 'Name, SKU or barcode',
              prefixIcon: const Icon(Icons.search),
              suffixIcon: _search.text.isEmpty
                  ? null
                  : IconButton(
                      tooltip: 'Clear search',
                      onPressed: _search.clear,
                      icon: const Icon(Icons.close),
                    ),
              isDense: true,
              border: const OutlineInputBorder(),
            ),
          ),
          if (_warehouse != null) ...[
            const SizedBox(height: 8),
            Align(
              alignment: Alignment.centerLeft,
              child: PopupMenuButton<String>(
                tooltip: 'Choose or add a warehouse',
                onSelected: _chooseWarehouse,
                itemBuilder: (context) => [
                  for (final w in _warehouses)
                    PopupMenuItem<String>(
                      value: w.id,
                      child: Text('${w.name} (${w.code})'),
                    ),
                  const PopupMenuDivider(),
                  const PopupMenuItem<String>(
                    value: _addWarehouseValue,
                    child: Text('Add warehouse...'),
                  ),
                ],
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 6),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Icons.warehouse_outlined, size: 18),
                      const SizedBox(width: 6),
                      Flexible(
                        child: Text(
                          'Warehouse: ${_warehouse!.name}',
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      const Icon(Icons.arrow_drop_down),
                    ],
                  ),
                ),
              ),
            ),
          ],
          if (summary != null) ...[
            const SizedBox(height: 8),
            Text(
              '${summary.productCount} products  |  '
              '${summary.stockedCount} in stock  |  '
              '${summary.outOfStockCount} out of stock',
              style: theme.textTheme.bodySmall,
            ),
          ],
          if (_error != null) ...[
            const SizedBox(height: 8),
            Text(_error!, style: TextStyle(color: scheme.error)),
          ],
          const SizedBox(height: 8),
          Card(
            margin: EdgeInsets.zero,
            clipBehavior: Clip.antiAlias,
            child: _loading
                ? const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  )
                : _lines.isEmpty
                ? const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(
                      child: Text('No products found. Add products first.'),
                    ),
                  )
                : Column(
                    children: [
                      for (var i = 0; i < _lines.length; i++) ...[
                        if (i > 0) const Divider(height: 1),
                        _lineTile(_lines[i], scheme),
                      ],
                    ],
                  ),
          ),
          if (_recent.isNotEmpty) ...[
            const SizedBox(height: 16),
            Text('Recent stock movements', style: theme.textTheme.titleSmall),
            const SizedBox(height: 4),
            Card(
              margin: EdgeInsets.zero,
              clipBehavior: Clip.antiAlias,
              child: Column(
                children: [
                  for (var i = 0; i < _recent.length; i++) ...[
                    if (i > 0) const Divider(height: 1),
                    ListTile(
                      dense: true,
                      title: Text(
                        _recent[i].productName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      subtitle: Text(
                        '${_recent[i].type.label}  |  '
                        '${formatTimestamp(_recent[i].occurredAt.toLocal())}'
                        '${(_recent[i].reason ?? '').isEmpty ? '' : '  |  ${_recent[i].reason}'}',
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      trailing: Text(
                        '${_recent[i].qtyE4 > 0 ? '+' : ''}${_qty(_recent[i].qtyE4)}',
                        style: TextStyle(
                          fontWeight: FontWeight.w700,
                          color: _recent[i].qtyE4 < 0 ? scheme.error : null,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _lineTile(StockLine line, ColorScheme scheme) {
    final unit = line.unitCode == null ? '' : ' ${line.unitCode}';
    final quantity = '${_qty(line.onHandE4)}$unit';

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: InkWell(
                  onTap: () => _open(line, StockMovementType.receipt),
                  borderRadius: BorderRadius.circular(12),
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 2),
                    child: Row(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Container(
                          width: 44,
                          height: 44,
                          decoration: BoxDecoration(
                            color: line.outOfStock
                                ? scheme.errorContainer
                                : scheme.primaryContainer,
                            borderRadius: BorderRadius.circular(12),
                          ),
                          child: Icon(
                            line.outOfStock
                                ? Icons.inventory_2_outlined
                                : Icons.inventory_2,
                            color: line.outOfStock
                                ? scheme.onErrorContainer
                                : scheme.onPrimaryContainer,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                line.name,
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: Theme.of(context)
                                    .textTheme.titleSmall
                                    ?.copyWith(fontWeight: FontWeight.w700),
                              ),
                              const SizedBox(height: 3),
                              Text(
                                'SKU: ${line.sku}',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                              const SizedBox(height: 7),
                              Wrap(
                                spacing: 6,
                                runSpacing: 4,
                                crossAxisAlignment: WrapCrossAlignment.center,
                                children: [
                                  Text(
                                    'On hand: $quantity',
                                    style: TextStyle(
                                      fontWeight: FontWeight.w700,
                                      color: line.outOfStock
                                          ? scheme.error
                                          : scheme.onSurface,
                                    ),
                                  ),
                                  if (line.outOfStock)
                                    Container(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 8,
                                        vertical: 3,
                                      ),
                                      decoration: BoxDecoration(
                                        color: scheme.errorContainer,
                                        borderRadius: BorderRadius.circular(20),
                                      ),
                                      child: Text(
                                        'Out of stock',
                                        style: TextStyle(
                                          color: scheme.onErrorContainer,
                                          fontSize: 11,
                                          fontWeight: FontWeight.w600,
                                        ),
                                      ),
                                    ),
                                ],
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              PopupMenuButton<StockMovementType>(
                tooltip: 'Stock actions',
                onSelected: (type) => _open(line, type),
                itemBuilder: (context) => const [
                  PopupMenuItem(
                    value: StockMovementType.receipt,
                    child: Text('Receive stock'),
                  ),
                  PopupMenuItem(
                    value: StockMovementType.opening,
                    child: Text('Opening stock'),
                  ),
                  PopupMenuItem(
                    value: StockMovementType.issue,
                    child: Text('Remove stock (damaged, lost)'),
                  ),
                  PopupMenuItem(
                    value: StockMovementType.adjustment,
                    child: Text('Adjust stock (count correction)'),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 10),
          LayoutBuilder(
            builder: (context, constraints) {
              final compact = constraints.maxWidth < 340;

              final addButton = FilledButton.icon(
                onPressed: () => _open(line, StockMovementType.receipt),
                icon: const Icon(Icons.add, size: 18),
                label: const Text('Add stock'),
              );

              final removeButton = OutlinedButton.icon(
                onPressed: line.onHandE4 <= 0
                    ? null
                    : () => _open(line, StockMovementType.issue),
                icon: const Icon(Icons.remove, size: 18),
                label: const Text('Remove stock'),
              );

              if (compact) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    addButton,
                    const SizedBox(height: 6),
                    removeButton,
                  ],
                );
              }

              return Row(
                children: [
                  Expanded(child: addButton),
                  const SizedBox(width: 8),
                  Expanded(child: removeButton),
                ],
              );
            },
          ),
        ],
      ),
    );
  }}

class _MovementSheet extends StatefulWidget {
  const _MovementSheet({
    required this.repo,
    required this.warehouse,
    required this.line,
    required this.type,
  });

  final LocalInventoryRepository repo;
  final Warehouse warehouse;
  final StockLine line;
  final StockMovementType type;

  @override
  State<_MovementSheet> createState() => _MovementSheetState();
}

class _MovementSheetState extends State<_MovementSheet> {
  late final TextEditingController _qty;
  late final TextEditingController _cost;
  final _note = TextEditingController();
  final _actor = TextEditingController(text: 'Owner');

  /// One id per sheet: saving twice (double tap, retry) applies only once.
  final String _requestId = generateUuidV4();

  String? _error;
  bool _saving = false;
  bool _review = false;
  int _qtyE4 = 0;
  int _countedE4 = 0;
  int? _costE4;
  bool _decrease = false;
  DateTime? _date;

  bool get _isIssue => widget.type == StockMovementType.issue;
  bool get _isAdjust => widget.type == StockMovementType.adjustment;
  bool get _needsReason => _isIssue || _isAdjust;
  bool get _removes => _isIssue || (_isAdjust && _decrease);
  bool get _hasCost => !_isIssue && !_isAdjust;

  String get _help => switch (widget.type) {
    StockMovementType.opening =>
      'Use this once to record the stock you already have when you start.',
    StockMovementType.receipt => 'Use this when new stock has arrived.',
    StockMovementType.issue => 'Use this for damaged, lost or expired items.',
    StockMovementType.adjustment =>
      'Count what is on the shelf and enter it. FlexiCore works out the difference.',
    _ => '',
  };

  String get _title => switch (widget.type) {
    StockMovementType.receipt => 'Receive stock',
    StockMovementType.opening => 'Opening stock',
    StockMovementType.issue => 'Remove stock',
    StockMovementType.adjustment => 'Adjust stock',
    _ => 'Stock change',
  };

  @override
  void initState() {
    super.initState();
    _qty = TextEditingController();
    _cost = TextEditingController(
      text: widget.line.costPriceE4 > 0
          ? formatQtyE4(widget.line.costPriceE4)
          : '',
    );
  }

  @override
  void dispose() {
    _qty.dispose();
    _cost.dispose();
    _note.dispose();
    _actor.dispose();
    super.dispose();
  }

  void _goReview() {
    final qty = parseQtyE4(_qty.text);
    if (qty == null || (!_isAdjust && qty <= 0)) {
      setState(
        () => _error = _isAdjust
            ? 'Enter the quantity you counted (0 or more, up to 4 decimals).'
            : 'Enter a quantity above zero (up to 4 decimals).',
      );
      return;
    }
    if (_isAdjust) {
      if (qty == widget.line.onHandE4) {
        setState(
          () => _error =
              'The count matches the system quantity. Nothing to adjust.',
        );
        return;
      }
      _decrease = qty < widget.line.onHandE4;
    }
    int? cost;
    if (_hasCost && _cost.text.trim().isNotEmpty) {
      cost = parseQtyE4(_cost.text);
      if (cost == null) {
        setState(() => _error = 'Enter a valid unit cost, or leave it empty.');
        return;
      }
    }
    if (_needsReason && _note.text.trim().isEmpty) {
      setState(
        () => _error = _isAdjust
            ? 'Please give a reason for the adjustment.'
            : 'Please give a reason (damaged, lost, ...).',
      );
      return;
    }
    if (_removes) {
      if (qty > widget.line.onHandE4) {
        setState(
          () => _error = 'Only ${formatQtyE4(widget.line.onHandE4)} on hand.',
        );
        return;
      }
    }
    if (_actor.text.trim().isEmpty) {
      setState(() => _error = 'Enter who is making this change.');
      return;
    }
    setState(() {
      _countedE4 = qty;
      _qtyE4 = _isAdjust ? (qty - widget.line.onHandE4).abs() : qty;
      _costE4 = cost;
      _error = null;
      _review = true;
    });
  }

  Future<void> _save() async {
    if (_saving) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await widget.repo.record(
        MovementRequest(
          productId: widget.line.productId,
          warehouseId: widget.warehouse.id,
          type: widget.type,
          quantityE4: _isAdjust && _decrease ? -_qtyE4 : _qtyE4,
          unitCostE4: _costE4,
          reason: _note.text.trim().isEmpty ? null : _note.text.trim(),
          requestId: _requestId,
          actor: _actor.text.trim(),
          occurredAt: _occurredAt(),
        ),
      );
      if (!mounted) return;
      Navigator.of(context).pop(true);
    } on InventoryException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _saving = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not save: $e';
        _saving = false;
      });
    }
  }

  DateTime? _occurredAt() {
    final d = _date;
    if (d == null) return null;
    final now = DateTime.now();
    return DateTime(d.year, d.month, d.day, now.hour, now.minute, now.second);
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _date ?? now,
      firstDate: DateTime(2000),
      lastDate: now,
    );
    if (picked == null || !mounted) return;
    setState(() => _date = picked);
  }

  Widget _row(String label, String value, {bool bold = false}) {
    final style = bold ? const TextStyle(fontWeight: FontWeight.w700) : null;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(child: Text(label)),
          const SizedBox(width: 12),
          Flexible(
            child: Text(value, style: style, textAlign: TextAlign.end),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final insets = MediaQuery.viewInsetsOf(context).bottom;
    final unit = widget.line.unitCode == null ? '' : ' ${widget.line.unitCode}';
    final onHand = widget.line.onHandE4;
    final signed = _removes ? -_qtyE4 : _qtyE4;

    return Padding(
      padding: EdgeInsets.fromLTRB(16, 0, 16, 16 + insets),
      child: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(_title, style: theme.textTheme.titleLarge),
            const SizedBox(height: 4),
            Text(
              '${widget.line.name}  (SKU ${widget.line.sku})',
              style: theme.textTheme.bodyMedium,
            ),
            Text(
              'On hand now: ${formatQtyE4(onHand)}$unit in ${widget.warehouse.name}',
              style: theme.textTheme.bodySmall,
            ),
            const SizedBox(height: 6),
            Text(
              _help,
              style: theme.textTheme.bodySmall?.copyWith(
                color: scheme.onSurfaceVariant,
              ),
            ),
            const SizedBox(height: 14),
            if (!_review) ...[
              TextField(
                controller: _qty,
                autofocus: true,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(
                  labelText: _isAdjust
                      ? 'Counted quantity'
                      : (_isIssue ? 'Quantity to remove' : 'Quantity'),
                  helperText: _isAdjust
                      ? 'What you actually have on the shelf'
                      : null,
                  suffixText: widget.line.unitCode,
                  border: const OutlineInputBorder(),
                ),
              ),

              if (_hasCost) ...[
                const SizedBox(height: 10),
                TextField(
                  controller: _cost,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Unit cost (optional)',
                    prefixText: 'Rs ',
                    border: OutlineInputBorder(),
                  ),
                ),
              ],
              const SizedBox(height: 10),
              TextField(
                controller: _note,
                maxLength: kMaxReasonLength,
                decoration: InputDecoration(
                  labelText: _needsReason
                      ? 'Reason (required)'
                      : 'Reference or note (optional)',
                  border: const OutlineInputBorder(),
                ),
              ),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: _pickDate,
                  icon: const Icon(Icons.event),
                  label: Text(
                    _date == null
                        ? 'Date: today (tap to change)'
                        : 'Date: ${formatTimestamp(_date!).substring(0, 10)}',
                  ),
                ),
              ),
              TextField(
                controller: _actor,
                decoration: const InputDecoration(
                  labelText: 'Entered by',
                  border: OutlineInputBorder(),
                ),
              ),
            ] else ...[
              Card(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    children: [
                      _row('Warehouse', widget.warehouse.name),
                      if (_isAdjust) ...[
                        _row('System quantity', '${formatQtyE4(onHand)}$unit'),
                        _row(
                          'Counted quantity',
                          '${formatQtyE4(_countedE4)}$unit',
                        ),
                        _row(
                          'Difference',
                          '${_removes ? '-' : '+'}${formatQtyE4(_qtyE4)}$unit',
                        ),
                      ] else
                        _row(
                          _removes ? 'Remove' : 'Add',
                          '${formatQtyE4(_qtyE4)}$unit',
                        ),
                      if (_costE4 != null)
                        _row(
                          'Unit cost',
                          'Rs ${formatQtyE4(_costE4!, minDecimals: 2)}',
                        ),
                      if (_note.text.trim().isNotEmpty)
                        _row('Note', _note.text.trim()),
                      _row(
                        'Date',
                        _date == null
                            ? 'Today'
                            : formatTimestamp(_date!).substring(0, 10),
                      ),
                      _row('Entered by', _actor.text.trim()),
                      const Divider(),
                      _row(
                        'On hand after',
                        '${formatQtyE4(onHand + signed)}$unit',
                        bold: true,
                      ),
                    ],
                  ),
                ),
              ),
            ],
            if (_error != null) ...[
              const SizedBox(height: 10),
              Text(_error!, style: TextStyle(color: scheme.error)),
            ],
            const SizedBox(height: 14),
            if (!_review)
              FilledButton(onPressed: _goReview, child: const Text('Review'))
            else
              Row(
                children: [
                  Expanded(
                    child: OutlinedButton(
                      onPressed: _saving
                          ? null
                          : () => setState(() => _review = false),
                      child: const Text('Back'),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: FilledButton(
                      onPressed: _saving ? null : _save,
                      child: Text(_saving ? 'Saving...' : 'Confirm and save'),
                    ),
                  ),
                ],
              ),
          ],
        ),
      ),
    );
  }
}
