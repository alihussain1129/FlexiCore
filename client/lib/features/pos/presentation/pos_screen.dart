import 'dart:convert';
import 'dart:math' as math;

import 'package:drift/drift.dart' hide Column;
import 'package:flutter/material.dart';
import '../../../core/read_only.dart';
import 'package:flutter/services.dart';

import '../../../core/ids.dart';
import '../../../data/local/app_database.dart';
import '../../catalog/data/local_catalog_repository.dart';
import '../../catalog/domain/catalog_domain.dart';
import '../../inventory/data/local_inventory_repository.dart';
import '../data/local/local_pos_repository.dart';
import '../data/local/local_pos_settings_repository.dart';
import '../domain/pos_format.dart';
import '../domain/pos_pricing.dart';
import 'pos_receipt_text.dart';

class PosScreen extends StatefulWidget {
  const PosScreen({
    super.key,
    required this.db,
    required this.companyId,
    this.businessName,
    this.refresh,
  });

  final AppDatabase db;
  final String companyId;
  final String? businessName;

  /// Fires when stock changes elsewhere (for example a stock entry).
  final Listenable? refresh;

  @override
  State<PosScreen> createState() => _PosScreenState();
}

class _CartLine {
  _CartLine(this.product);

  final CatalogProduct product;
  int quantityE4 = 10000;
  int lineDiscountE4 = 0;
}

class _PosScreenState extends State<PosScreen> {
  late final LocalCatalogRepository _catalog;
  late final LocalPosRepository _pos;
  late final LocalPosSettingsRepository _settingsRepo;

  final _search = TextEditingController();
  final _paid = TextEditingController(text: '0');
  final _cashier = TextEditingController(text: 'Cashier');
  final _discount = TextEditingController();
  final _discountReason = TextEditingController();
  final Map<String, _CartLine> _cart = {};

  List<CatalogProduct> _products = [];
  final Map<String, int> _stockByProduct = {};
  PosConfig _config = PosConfig.defaults;
  String? _warehouseId;
  String? _error;
  String _paymentMethod = 'Cash';
  bool _discountPercent = false;
  bool _loading = true;
  bool _saving = false;
  String? _pendingCheckoutId;
  String? _pendingCheckoutFingerprint;
  bool _checkoutOutcomeUncertain = false;

  bool get _checkoutLocked => _saving || _checkoutOutcomeUncertain;
  bool _paymentEdited = false;
  int _searchVersion = 0;

  @override
  void initState() {
    super.initState();
    _catalog = LocalCatalogRepository(widget.db, companyId: widget.companyId);
    _pos = LocalPosRepository(widget.db, companyId: widget.companyId);
    _settingsRepo = LocalPosSettingsRepository(
      widget.db,
      companyId: widget.companyId,
    );
    _search.addListener(_loadProducts);
    widget.refresh?.addListener(_loadProducts);
    _initialize();
  }

  Future<void> _initialize() async {
    try {
      await LocalInventoryRepository(
        widget.db,
        companyId: widget.companyId,
      ).ensureDefaultWarehouse();

      final config = await _settingsRepo.load();
      final warehouse =
          await (widget.db.select(widget.db.warehouses)..where(
                (w) =>
                    w.companyId.equals(widget.companyId) &
                    w.deletedAt.isNull() &
                    w.isDefault.equals(true),
              ))
              .getSingleOrNull();

      if (!mounted) return;
      setState(() {
        _config = config;
        _warehouseId = warehouse?.id;
        _error = warehouse == null
            ? 'No default warehouse found. Configure a warehouse before checkout.'
            : null;
      });
      await _loadProducts();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Could not prepare POS: $e';
        _loading = false;
      });
    }
  }

  Future<void> _loadProducts() async {
    final version = ++_searchVersion;
    if (mounted) setState(() => _loading = true);
    try {
      final products = await _catalog.listProducts(query: _search.text);
      final stockByProduct = <String, int>{
        for (final product in products) product.id: 0,
      };
      final warehouseId = _warehouseId;
      if (warehouseId != null && products.isNotEmpty) {
        final productIds = products.map((product) => product.id).toList();
        final balances =
            await (widget.db.select(widget.db.stockBalances)..where(
                  (balance) =>
                      balance.companyId.equals(widget.companyId) &
                      balance.warehouseId.equals(warehouseId) &
                      balance.productId.isIn(productIds),
                ))
                .get();
        for (final balance in balances) {
          stockByProduct[balance.productId] = balance.qtyE4;
        }
      }
      if (!mounted || version != _searchVersion) return;
      setState(() {
        _products = products;
        _stockByProduct
          ..clear()
          ..addAll(stockByProduct);
        _loading = false;
        if (_error?.startsWith('Could not load products:') ?? false) {
          _error = null;
        }
      });
    } catch (e) {
      if (!mounted || version != _searchVersion) return;
      setState(() {
        _error = 'Could not load products: $e';
        _loading = false;
      });
    }
  }

  // ---- money maths (same engine the repository uses at checkout) ----

  int _gross(_CartLine line) =>
      (line.quantityE4 * line.product.salePriceE4 + 5000) ~/ 10000;

  /// The order discount in E4, or -1 when the text is not a valid number.
  int _orderDiscountE4() {
    final raw = _discount.text.trim();
    if (raw.isEmpty) return 0;
    if (_discountPercent) {
      final bp = PosFormat.parsePercentBp(raw);
      if (bp == null) return -1;
      final afterLines = _cart.values.fold<int>(
        0,
        (sum, line) => sum + _gross(line) - line.lineDiscountE4,
      );
      return PosPricing.percentOfE4(afterLines, bp);
    }
    return PosFormat.parseE4(raw) ?? -1;
  }

  ({PosPricing? pricing, String? error}) _price() {
    if (_cart.isEmpty) return (pricing: null, error: null);
    final discount = _orderDiscountE4();
    if (discount < 0) return (pricing: null, error: 'Enter a valid discount.');
    try {
      final pricing = PosPricing.calculate(
        lines: [
          for (final line in _cart.values)
            PosPriceInput(
              quantityE4: line.quantityE4,
              unitPriceE4: line.product.salePriceE4,
              lineDiscountE4: line.lineDiscountE4,
            ),
        ],
        orderDiscountE4: discount,
        taxRateBp: _config.taxRateBp,
      );
      if (pricing.discountE4 * 10000 >
          pricing.subtotalE4 * _config.maxDiscountBp) {
        return (
          pricing: pricing,
          error:
              'Discount is above the allowed maximum of '
              '${PosPricing.percentLabel(_config.maxDiscountBp)}.',
        );
      }
      return (pricing: pricing, error: null);
    } on PosPricingException catch (e) {
      return (pricing: null, error: e.message);
    }
  }

  void _syncPaid() {
    if (_paymentEdited) return;
    final total = _price().pricing?.totalE4 ?? 0;
    _paid.text = PosFormat.plain(total);
  }

  void _clampDiscount(_CartLine line) {
    final gross = _gross(line);
    if (line.lineDiscountE4 > gross) line.lineDiscountE4 = gross;
  }

  void _clearSale() {
    _cart.clear();
    _discount.clear();
    _discountReason.clear();
    _paymentEdited = false;
    _paid.text = '0';
  }

  // ---- cart actions ----

  void _add(CatalogProduct product) {
    if (_checkoutLocked) return;
    final availableE4 = _stockByProduct[product.id] ?? 0;
    final currentE4 = _cart[product.id]?.quantityE4 ?? 0;
    if (availableE4 <= 0) {
      _message('No available stock for ${product.name} in this warehouse.');
      return;
    }
    if (currentE4 + 10000 > availableE4) {
      _message(
        'Not enough stock for ${product.name}. '
        'Available: ${PosFormat.plain(availableE4)}.',
      );
      return;
    }
    setState(() {
      final line = _cart[product.id];
      if (line == null) {
        _cart[product.id] = _CartLine(product);
      } else {
        line.quantityE4 += 10000;
      }
      _syncPaid();
    });
  }

  void _changeQuantity(String id, int delta) {
    if (_checkoutLocked) return;
    final line = _cart[id];
    if (line == null) return;
    final next = line.quantityE4 + delta;
    final availableE4 = _stockByProduct[id] ?? 0;
    if (delta > 0 && next > availableE4) {
      _message(
        'Not enough stock for ${line.product.name}. '
        'Available: ${PosFormat.plain(availableE4)}.',
      );
      return;
    }
    setState(() {
      if (next <= 0) {
        _cart.remove(id);
      } else {
        line.quantityE4 = next;
        _clampDiscount(line);
      }
      _syncPaid();
    });
  }

  Future<void> _editLine(_CartLine line) async {
    if (_checkoutLocked) return;
    final result = await showDialog<_LineEdit>(
      context: context,
      builder: (context) => _LineEditDialog(
        name: line.product.name,
        quantityE4: line.quantityE4,
        discountE4: line.lineDiscountE4,
        availableE4: _stockByProduct[line.product.id] ?? 0,
        unitPriceE4: line.product.salePriceE4,
      ),
    );
    if (result == null || !mounted || _checkoutLocked) return;
    setState(() {
      if (result.remove) {
        _cart.remove(line.product.id);
      } else {
        line.quantityE4 = result.quantityE4;
        line.lineDiscountE4 = result.discountE4;
      }
      _syncPaid();
    });
  }

  Future<void> _openSettings() async {
    if (_checkoutLocked) return;
    final result = await showDialog<PosConfig>(
      context: context,
      builder: (context) => _SettingsDialog(config: _config),
    );
    if (result == null || _checkoutLocked) return;
    try {
      final saved = await _settingsRepo.save(result);
      if (!mounted) return;
      setState(() {
        _config = saved;
        _syncPaid();
      });
      _message('POS settings saved.');
    } catch (e) {
      _message('Could not save settings: $e');
    }
  }

  void _message(String value) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(value)));
  }

  String _checkoutFingerprint(PosCheckoutRequest request) {
    final items = request.items
        .map((item) => <Object?>[item.productId, item.quantityE4])
        .toList()
      ..sort((a, b) => (a[0] as String).compareTo(b[0] as String));

    final discounts = request.lineDiscountsE4.entries.toList()
      ..sort((a, b) => a.key.compareTo(b.key));

    return jsonEncode(<String, Object?>{
      'items': items,
      'paymentMethod': request.paymentMethod,
      'amountPaidE4': request.amountPaidE4,
      'createdBy': request.createdBy,
      'warehouseId': request.warehouseId,
      'orderDiscountE4': request.orderDiscountE4,
      'lineDiscountsE4': <String, int>{
        for (final entry in discounts) entry.key: entry.value,
      },
      'discountReason': request.discountReason,
      'cashTenderedE4': request.cashTenderedE4,
    });
  }

  Future<void> _checkout() async {
    if (_saving) return;
    if (_cart.isEmpty) {
      _message('Add a product to the cart first.');
      return;
    }
    if (_warehouseId == null) {
      _message('A default warehouse is required.');
      return;
    }

    final priced = _price();
    final pricing = priced.pricing;
    if (pricing == null || priced.error != null) {
      _message(priced.error ?? 'Check the sale details.');
      return;
    }

    final received = PosFormat.parseE4(_paid.text);
    if (received == null) {
      _message('Enter a valid amount received (up to four decimals).');
      return;
    }

    final cashier = _cashier.text.trim();
    if (cashier.isEmpty) {
      _message('Enter the cashier name.');
      return;
    }

    final isCash = _paymentMethod == 'Cash';
    final total = pricing.totalE4;
    if (!isCash && received > total) {
      _message('The payment cannot be more than the total for this method.');
      return;
    }
    final applied = isCash ? math.min(received, total) : received;

    if (applied < total) {
      final ok = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Sale not fully paid'),
          content: Text(
            'Rs ${PosFormat.money(total - applied)} will stay unpaid on this '
            'sale. Continue?',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Back'),
            ),
            FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Save sale'),
            ),
          ],
        ),
      );
      if (ok != true || !mounted) return;
    }

    final items = _cart.values
        .map(
          (line) => PosCartItem(
            productId: line.product.id,
            quantityE4: line.quantityE4,
          ),
        )
        .toList();
    final lineDiscounts = <String, int>{
      for (final line in _cart.values)
        if (line.lineDiscountE4 > 0) line.product.id: line.lineDiscountE4,
    };
    final reason = _discountReason.text.trim();
    final request = PosCheckoutRequest(
      clientRequestId: _pendingCheckoutId ?? generateUuidV4(),
      items: items,
      paymentMethod: _paymentMethod,
      amountPaidE4: applied,
      createdBy: cashier,
      warehouseId: _warehouseId,
      orderDiscountE4: _orderDiscountE4(),
      lineDiscountsE4: lineDiscounts,
      discountReason: reason.isEmpty ? null : reason,
      cashTenderedE4: isCash ? received : null,
    );
    final fingerprint = _checkoutFingerprint(request);

    if (_checkoutOutcomeUncertain &&
        _pendingCheckoutFingerprint != fingerprint) {
      _message(
        'This sale has an uncertain save outcome. Retry the unchanged sale '
        'or resolve it before starting another sale.',
      );
      return;
    }

    _pendingCheckoutId ??= request.clientRequestId;
    _pendingCheckoutFingerprint ??= fingerprint;

    if (_pendingCheckoutFingerprint != fingerprint) {
      _pendingCheckoutId = generateUuidV4();
      _pendingCheckoutFingerprint = fingerprint;
    }

    setState(() => _saving = true);

    late final PosReceipt receipt;
    try {
      receipt = await _pos.checkout(request);
    } catch (e) {
      if (!mounted) return;
      if (e is ReadOnlyLicenseException) {
        _message(e.toString());
        return;
      }
      setState(() => _checkoutOutcomeUncertain = true);
      _message(
        'The save outcome could not be confirmed. Do not change this sale; '
        'retry it to check the same request. ${e is PosException ? e.message : e}',
      );
      return;
    } finally {
      if (mounted) setState(() => _saving = false);
    }

    // A returned receipt confirms that checkout succeeded. Clear retry state
    // before showing the receipt, so a display error cannot cause a new sale.
    _pendingCheckoutId = null;
    _pendingCheckoutFingerprint = null;
    _checkoutOutcomeUncertain = false;

    if (!mounted) return;
    setState(_clearSale);

    try {
      await showDialog<void>(
        context: context,
        builder: (context) => _ReceiptDialog(
          receipt: receipt,
          businessName: widget.businessName,
          footer: _config.receiptFooter,
        ),
      );
      await _loadProducts();
    } catch (e) {
      if (mounted) {
        _message('Sale saved, but receipt display or product refresh failed: $e');
      }
    }
  }

  @override
  void dispose() {
    widget.refresh?.removeListener(_loadProducts);
    _search
      ..removeListener(_loadProducts)
      ..dispose();
    _paid.dispose();
    _cashier.dispose();
    _discount.dispose();
    _discountReason.dispose();
    super.dispose();
  }

  // ---- layout ----

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Point of Sale'),
            if (widget.businessName?.trim().isNotEmpty ?? false)
              Text(
                widget.businessName!,
                style: Theme.of(context).textTheme.labelSmall,
              ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'POS settings',
            onPressed: _openSettings,
            icon: const Icon(Icons.tune),
          ),
          Padding(
            padding: const EdgeInsets.only(left: 4, right: 14),
            child: Center(child: _localBadge()),
          ),
        ],
      ),
      body: LayoutBuilder(
        builder: (context, constraints) {
          if (constraints.maxWidth >= 900) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(flex: 6, child: _productPanel()),
                  const SizedBox(width: 16),
                  Expanded(flex: 5, child: _cartPanel()),
                ],
              ),
            );
          }
          return _narrowBody(constraints);
        },
      ),
    );
  }

  /// Working offline is normal for this app, so it is shown as a quiet
  /// status, not as an error-looking chip.
  Widget _localBadge() {
    final muted = Theme.of(context).colorScheme.onSurfaceVariant;
    return Tooltip(
      message: 'Works without internet. Sales are saved on this device.',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(Icons.offline_pin_outlined, size: 18, color: muted),
          const SizedBox(width: 4),
          Text(
            'Local',
            style: Theme.of(context).textTheme.labelMedium
                ?.copyWith(color: muted),
          ),
        ],
      ),
    );
  }

  Widget _searchField() {
    return TextField(
      controller: _search,
      enabled: !_checkoutLocked,
      decoration: InputDecoration(
        labelText: 'Search products',
        hintText: 'Name, SKU or barcode',
        prefixIcon: const Icon(Icons.search),
        suffixIcon: _search.text.isEmpty
            ? null
            : IconButton(
                tooltip: 'Clear search',
                onPressed: _checkoutLocked ? null : _search.clear,
                icon: const Icon(Icons.close),
              ),
        isDense: true,
        border: const OutlineInputBorder(),
      ),
    );
  }

  Widget _productTile(CatalogProduct product) {
    final scheme = Theme.of(context).colorScheme;
    final stock = _stockByProduct[product.id] ?? 0;
    return ListTile(
      title: Text(product.name, maxLines: 2, overflow: TextOverflow.ellipsis),
      subtitle: Text(
        'SKU: ${product.sku}  |  Stock: ${PosFormat.plain(stock)}',
        style: stock <= 0 ? TextStyle(color: scheme.error) : null,
      ),
      leading: const CircleAvatar(child: Icon(Icons.inventory_2_outlined)),
      trailing: Text(
        'Rs ${PosFormat.money(product.salePriceE4)}',
        style: const TextStyle(fontWeight: FontWeight.bold),
      ),
      onTap: _checkoutLocked ? null : () => _add(product),
    );
  }

  Widget _productHeaderRow() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
      child: Row(
        children: [
          Text('Products', style: Theme.of(context).textTheme.titleMedium),
          const Spacer(),
          Text('${_products.length} found'),
        ],
      ),
    );
  }

  Widget _cartHeaderRow() {
    return Padding(
      padding: const EdgeInsets.fromLTRB(12, 6, 8, 6),
      child: Row(
        children: [
          const Icon(Icons.shopping_cart_outlined),
          const SizedBox(width: 8),
          Text('Current sale', style: Theme.of(context).textTheme.titleMedium),
          const Spacer(),
          TextButton(
            onPressed: _checkoutLocked || _cart.isEmpty ? null : () => setState(_clearSale),
            child: const Text('Clear'),
          ),
        ],
      ),
    );
  }

  Widget _cartLineTile(_CartLine line) {
    final lineTotal = _gross(line) - line.lineDiscountE4;
    return ListTile(
      dense: true,
      onTap: _checkoutLocked ? null : () => _editLine(line),
      title: Text(
        line.product.name,
        maxLines: 1,
        overflow: TextOverflow.ellipsis,
      ),
      subtitle: Text(
        '${PosFormat.plain(line.quantityE4)} \u00D7 '
        'Rs ${PosFormat.money(line.product.salePriceE4)}'
        '${line.lineDiscountE4 > 0 ? '  (-${PosFormat.money(line.lineDiscountE4)})' : ''}'
        '  =  Rs ${PosFormat.money(lineTotal)}',
      ),
      trailing: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          IconButton(
            tooltip: 'Decrease quantity',
            onPressed: _checkoutLocked ? null : () => _changeQuantity(line.product.id, -10000),
            icon: const Icon(Icons.remove_circle_outline),
          ),
          Text(PosFormat.plain(line.quantityE4)),
          IconButton(
            tooltip: 'Increase quantity',
            onPressed: _checkoutLocked ? null : () => _changeQuantity(line.product.id, 10000),
            icon: const Icon(Icons.add_circle_outline),
          ),
        ],
      ),
    );
  }

  Widget _checkoutButton() {
    final priced = _price();
    return FilledButton.icon(
      onPressed: _saving || _cart.isEmpty || priced.error != null || isReadOnlyOf(context)
          ? null
          : _checkout,
      icon: _saving
          ? const SizedBox(
              width: 18,
              height: 18,
              child: CircularProgressIndicator(strokeWidth: 2),
            )
          : const Icon(Icons.point_of_sale),
      label: Text(_saving ? 'Saving sale...' : (_checkoutOutcomeUncertain ? 'Retry same sale' : 'Complete sale')),
    );
  }

  /// Subtotal, discount, tax, total, payment, cash received and cashier.
  /// It has no scroll view of its own: the caller decides how it scrolls.
  Widget _paymentSection() {
    final scheme = Theme.of(context).colorScheme;
    final priced = _price();
    final pricing = priced.pricing;
    final total = pricing?.totalE4 ?? 0;
    final received = PosFormat.parseE4(_paid.text);
    final isCash = _paymentMethod == 'Cash';
    final overpaid =
        !isCash && pricing != null && received != null && received > total;
    int? change;
    int? due;
    if (pricing != null && received != null) {
      if (isCash && received > total) {
        change = received - total;
      } else if (received < total) {
        due = total - received;
      }
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _totalRow('Subtotal', pricing?.subtotalE4 ?? 0),
        const SizedBox(height: 8),
        Row(
          children: [
            Expanded(
              child: TextField(
                controller: _discount,
                enabled: !_checkoutLocked,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: InputDecoration(
                  labelText: _discountPercent
                      ? 'Discount (%)'
                      : 'Discount (Rs)',
                  isDense: true,
                  border: const OutlineInputBorder(),
                ),
                onChanged: (_) => setState(_syncPaid),
              ),
            ),
            const SizedBox(width: 8),
            SegmentedButton<bool>(
              segments: const <ButtonSegment<bool>>[
                ButtonSegment<bool>(value: false, label: Text('Rs')),
                ButtonSegment<bool>(value: true, label: Text('%')),
              ],
              selected: <bool>{_discountPercent},
              showSelectedIcon: false,
              onSelectionChanged: _checkoutLocked ? null : (selection) => setState(() {
                _discountPercent = selection.first;
                _syncPaid();
              }),
            ),
          ],
        ),
        if (_discount.text.trim().isNotEmpty) ...[
          const SizedBox(height: 8),
          TextField(
            controller: _discountReason,
            enabled: !_checkoutLocked,
            inputFormatters: [LengthLimitingTextInputFormatter(120)],
            decoration: const InputDecoration(
              labelText: 'Discount reason (optional)',
              isDense: true,
              border: OutlineInputBorder(),
            ),
          ),
        ],
        if (pricing != null && pricing.discountE4 > 0) ...[
          const SizedBox(height: 8),
          _totalRow('Discount', pricing.discountE4, prefix: '-'),
        ],
        if (pricing != null && _config.taxRateBp > 0) ...[
          const SizedBox(height: 4),
          _totalRow(
            'Tax (${PosPricing.percentLabel(_config.taxRateBp)})',
            pricing.taxE4,
          ),
        ],
        if (priced.error != null) ...[
          const SizedBox(height: 6),
          Align(
            alignment: Alignment.centerLeft,
            child: Text(priced.error!, style: TextStyle(color: scheme.error)),
          ),
        ],
        const SizedBox(height: 8),
        _totalRow('TOTAL', total, bold: true),
        const SizedBox(height: 10),
        Row(
          children: [
            const Expanded(child: Text('Payment method')),
            DropdownButton<String>(
              value: _paymentMethod,
              items: const [
                DropdownMenuItem(value: 'Cash', child: Text('Cash')),
                DropdownMenuItem(value: 'Card', child: Text('Card')),
                DropdownMenuItem(value: 'Bank', child: Text('Bank')),
                DropdownMenuItem(value: 'Other', child: Text('Other')),
              ],
              onChanged: _checkoutLocked
                  ? null
                  : (value) {
                      if (value != null) {
                        setState(() => _paymentMethod = value);
                      }
                    },
            ),
          ],
        ),
        const SizedBox(height: 6),
        Row(
          children: [
            Expanded(child: Text(isCash ? 'Cash received' : 'Amount received')),
            SizedBox(
              width: 150,
              child: TextField(
                controller: _paid,
                enabled: !_checkoutLocked,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                textAlign: TextAlign.end,
                decoration: const InputDecoration(
                  prefixText: 'Rs ',
                  isDense: true,
                  border: OutlineInputBorder(),
                ),
                onChanged: (_) => setState(() => _paymentEdited = true),
              ),
            ),
          ],
        ),
        if (overpaid)
          Align(
            alignment: Alignment.centerRight,
            child: Text(
              'More than the total',
              style: TextStyle(color: scheme.error),
            ),
          ),
        if (change != null) ...[
          const SizedBox(height: 6),
          _totalRow('Change', change, bold: true, color: Colors.green.shade700),
        ],
        if (due != null) ...[
          const SizedBox(height: 6),
          _totalRow('Remaining due', due, color: scheme.error),
        ],
        const SizedBox(height: 10),
        TextField(
          controller: _cashier,
          enabled: !_checkoutLocked,
          decoration: const InputDecoration(
            labelText: 'Cashier name',
            isDense: true,
            border: OutlineInputBorder(),
          ),
        ),
      ],
    );
  }

  /// Phone layout: everything scrolls together, and the total with the
  /// Complete sale button stays pinned above the keyboard.
  Widget _narrowBody(BoxConstraints constraints) {
    final scheme = Theme.of(context).colorScheme;
    final total = _price().pricing?.totalE4 ?? 0;
    final listMax = math.max(
      150.0,
      math.min(320.0, constraints.maxHeight * 0.38),
    );
    return Column(
      children: [
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(12),
            children: [
              Card(
                margin: EdgeInsets.zero,
                clipBehavior: Clip.antiAlias,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Padding(
                      padding: const EdgeInsets.all(12),
                      child: _searchField(),
                    ),
                    if (_error != null)
                      Padding(
                        padding: const EdgeInsets.fromLTRB(12, 0, 12, 8),
                        child: Text(
                          _error!,
                          style: TextStyle(color: scheme.error),
                        ),
                      ),
                    _productHeaderRow(),
                    const Divider(height: 1),
                    if (_loading)
                      const Padding(
                        padding: EdgeInsets.all(20),
                        child: Center(child: CircularProgressIndicator()),
                      )
                    else if (_products.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(20),
                        child: Center(
                          child: Text('No products found. Add products first.'),
                        ),
                      )
                    else
                      ConstrainedBox(
                        constraints: BoxConstraints(maxHeight: listMax),
                        child: ListView.separated(
                          shrinkWrap: true,
                          itemCount: _products.length,
                          separatorBuilder: (_, _) => const Divider(height: 1),
                          itemBuilder: (context, index) =>
                              _productTile(_products[index]),
                        ),
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 10),
              Card(
                margin: EdgeInsets.zero,
                clipBehavior: Clip.antiAlias,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _cartHeaderRow(),
                    const Divider(height: 1),
                    if (_cart.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Center(
                          child: Text('Your cart is empty. Tap a product.'),
                        ),
                      )
                    else
                      for (final line in _cart.values) _cartLineTile(line),
                  ],
                ),
              ),
              const SizedBox(height: 10),
              Card(
                margin: EdgeInsets.zero,
                child: Padding(
                  padding: const EdgeInsets.all(12),
                  child: _paymentSection(),
                ),
              ),
            ],
          ),
        ),
        Material(
          elevation: 8,
          color: scheme.surface,
          child: SafeArea(
            top: false,
            child: Padding(
              padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
              child: Row(
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        'Total',
                        style: Theme.of(context).textTheme.labelMedium,
                      ),
                      Text(
                        'Rs ${PosFormat.money(total)}',
                        style: Theme.of(context).textTheme.titleLarge
                            ?.copyWith(fontWeight: FontWeight.w800),
                      ),
                    ],
                  ),
                  const SizedBox(width: 16),
                  Expanded(child: _checkoutButton()),
                ],
              ),
            ),
          ),
        ),
      ],
    );
  }

  /// Wide layout (desktop and tablets): products on the left, sale on the right.
  Widget _productPanel() {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          Padding(padding: const EdgeInsets.all(12), child: _searchField()),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Text(_error!, style: TextStyle(color: scheme.error)),
            ),
          _productHeaderRow(),
          const Divider(height: 1),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _products.isEmpty
                ? const Center(
                    child: Text('No products found. Add products first.'),
                  )
                : ListView.separated(
                    itemCount: _products.length,
                    separatorBuilder: (_, _) => const Divider(height: 1),
                    itemBuilder: (context, index) =>
                        _productTile(_products[index]),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _cartPanel() {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          _cartHeaderRow(),
          const Divider(height: 1),
          Expanded(
            flex: 2,
            child: _cart.isEmpty
                ? const Center(child: Text('Your cart is empty'))
                : ListView(
                    children: [
                      for (final line in _cart.values) _cartLineTile(line),
                    ],
                  ),
          ),
          const Divider(height: 1),
          Expanded(
            flex: 3,
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(12),
              child: Column(
                children: [
                  _paymentSection(),
                  const SizedBox(height: 10),
                  SizedBox(width: double.infinity, child: _checkoutButton()),
                  const SizedBox(height: 4),
                  Text(
                    'Stock is validated when the sale is saved.',
                    style: Theme.of(context).textTheme.bodySmall,
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _totalRow(
    String label,
    int value, {
    bool bold = false,
    Color? color,
    String prefix = '',
  }) {
    var style = bold
        ? Theme.of(context).textTheme.titleMedium
              ?.copyWith(fontWeight: FontWeight.w800)
        : null;
    if (color != null) {
      style = (style ?? const TextStyle()).copyWith(color: color);
    }
    return Row(
      children: [
        Expanded(child: Text(label, style: style)),
        Text('${prefix}Rs ${PosFormat.money(value)}', style: style),
      ],
    );
  }
}

class _LineEdit {
  const _LineEdit({
    this.remove = false,
    this.quantityE4 = 0,
    this.discountE4 = 0,
  });

  final bool remove;
  final int quantityE4;
  final int discountE4;
}

class _LineEditDialog extends StatefulWidget {
  const _LineEditDialog({
    required this.name,
    required this.quantityE4,
    required this.discountE4,
    required this.availableE4,
    required this.unitPriceE4,
  });

  final String name;
  final int quantityE4;
  final int discountE4;
  final int availableE4;
  final int unitPriceE4;

  @override
  State<_LineEditDialog> createState() => _LineEditDialogState();
}

class _LineEditDialogState extends State<_LineEditDialog> {
  late final TextEditingController _qty = TextEditingController(
    text: PosFormat.plain(widget.quantityE4),
  );
  late final TextEditingController _disc = TextEditingController(
    text: widget.discountE4 == 0 ? '' : PosFormat.plain(widget.discountE4),
  );
  String? _error;

  @override
  void dispose() {
    _qty.dispose();
    _disc.dispose();
    super.dispose();
  }

  void _apply() {
    final quantity = PosFormat.parseE4(_qty.text);
    if (quantity == null || quantity <= 0) {
      setState(
        () => _error = 'Enter a quantity above zero (up to 4 decimals).',
      );
      return;
    }
    if (quantity > widget.availableE4) {
      setState(
        () => _error = 'Only ${PosFormat.plain(widget.availableE4)} in stock.',
      );
      return;
    }
    final discountText = _disc.text.trim();
    final int? discount = discountText.isEmpty
        ? 0
        : PosFormat.parseE4(discountText);
    if (discount == null) {
      setState(() => _error = 'Enter a valid discount amount.');
      return;
    }
    final gross = (quantity * widget.unitPriceE4 + 5000) ~/ 10000;
    if (discount > gross) {
      setState(
        () => _error =
            'The discount cannot be more than the line amount '
            '(Rs ${PosFormat.money(gross)}).',
      );
      return;
    }
    Navigator.of(context)
        .pop(_LineEdit(quantityE4: quantity, discountE4: discount));
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.name, maxLines: 2, overflow: TextOverflow.ellipsis),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Price: Rs ${PosFormat.money(widget.unitPriceE4)}'),
            const SizedBox(height: 12),
            TextField(
              controller: _qty,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: InputDecoration(
                labelText: 'Quantity',
                helperText: 'In stock: ${PosFormat.plain(widget.availableE4)}',
                border: const OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _disc,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Line discount (Rs)',
                border: OutlineInputBorder(),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () =>
              Navigator.of(context).pop(const _LineEdit(remove: true)),
          child: const Text('Remove'),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(onPressed: _apply, child: const Text('Apply')),
      ],
    );
  }
}

class _SettingsDialog extends StatefulWidget {
  const _SettingsDialog({required this.config});

  final PosConfig config;

  @override
  State<_SettingsDialog> createState() => _SettingsDialogState();
}

class _SettingsDialogState extends State<_SettingsDialog> {
  late final TextEditingController _tax = TextEditingController(
    text: _bpText(widget.config.taxRateBp),
  );
  late final TextEditingController _maxDiscount = TextEditingController(
    text: _bpText(widget.config.maxDiscountBp),
  );
  late final TextEditingController _footer = TextEditingController(
    text: widget.config.receiptFooter ?? '',
  );
  String? _error;

  static String _bpText(int bp) =>
      PosPricing.percentLabel(bp).replaceAll('%', '');

  @override
  void dispose() {
    _tax.dispose();
    _maxDiscount.dispose();
    _footer.dispose();
    super.dispose();
  }

  void _save() {
    final tax = PosFormat.parsePercentBp(_tax.text);
    final maxDiscount = PosFormat.parsePercentBp(_maxDiscount.text);
    final footer = _footer.text.trim();
    if (tax == null) {
      setState(
        () =>
            _error = 'Enter the tax rate as a percent, for example 15 or 12.5.',
      );
      return;
    }
    if (maxDiscount == null) {
      setState(
        () => _error = 'Enter the maximum discount as a percent, 0 to 100.',
      );
      return;
    }
    if (footer.length > 200) {
      setState(
        () => _error = 'The receipt footer is too long (200 characters).',
      );
      return;
    }
    Navigator.of(context).pop(
      PosConfig(
        taxRateBp: tax,
        maxDiscountBp: maxDiscount,
        receiptFooter: footer.isEmpty ? null : footer,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: const Text('POS settings'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text('Tax is added on top of the product prices.'),
            const SizedBox(height: 12),
            TextField(
              controller: _tax,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Tax rate',
                suffixText: '%',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _maxDiscount,
              keyboardType: const TextInputType.numberWithOptions(
                decimal: true,
              ),
              decoration: const InputDecoration(
                labelText: 'Maximum discount per sale',
                suffixText: '%',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _footer,
              maxLines: 2,
              decoration: const InputDecoration(
                labelText: 'Receipt footer (optional)',
                border: OutlineInputBorder(),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(onPressed: _save, child: const Text('Save')),
      ],
    );
  }
}

class _ReceiptDialog extends StatefulWidget {
  const _ReceiptDialog({
    required this.receipt,
    required this.businessName,
    required this.footer,
  });

  final PosReceipt receipt;
  final String? businessName;
  final String? footer;

  @override
  State<_ReceiptDialog> createState() => _ReceiptDialogState();
}

class _ReceiptDialogState extends State<_ReceiptDialog> {
  bool _copied = false;

  Future<void> _copy() async {
    final text = buildReceiptText(
      widget.receipt,
      businessName: widget.businessName,
      footer: widget.footer,
    );
    await Clipboard.setData(ClipboardData(text: text));
    if (!mounted) return;
    setState(() => _copied = true);
  }

  @override
  Widget build(BuildContext context) {
    final r = widget.receipt;
    final due = r.totalE4 - r.amountPaidE4;
    final scheme = Theme.of(context).colorScheme;
    return AlertDialog(
      title: const Row(
        children: [
          Icon(Icons.check_circle, color: Colors.green),
          SizedBox(width: 8),
          Expanded(child: Text('Sale saved')),
        ],
      ),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.businessName?.trim().isNotEmpty ?? false)
                Text(
                  widget.businessName!,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              const Text('SALES RECEIPT'),
              const SizedBox(height: 6),
              SelectableText('Receipt: ${r.receiptNumber}'),
              SelectableText(
                'Date: ${PosFormat.dateTime(r.createdAt.toLocal())}',
              ),
              const Divider(height: 20),
              for (final line in r.lines)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              line.name,
                              style: const TextStyle(
                                fontWeight: FontWeight.w600,
                              ),
                            ),
                            Text(
                              '${PosFormat.plain(line.quantityE4)} \u00D7 '
                              'Rs ${PosFormat.money(line.unitPriceE4)}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                            if (line.discountE4 > 0)
                              Text(
                                'Discount -Rs ${PosFormat.money(line.discountE4)}',
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 12),
                      Text('Rs ${PosFormat.money(line.lineTotalE4)}'),
                    ],
                  ),
                ),
              const Divider(height: 20),
              _row(context, 'Subtotal', r.subtotalE4),
              if (r.discountE4 > 0)
                _row(context, 'Discount', r.discountE4, prefix: '-'),
              if (r.taxE4 > 0)
                _row(
                  context,
                  'Tax (${PosPricing.percentLabel(r.taxRateBp)})',
                  r.taxE4,
                ),
              _row(context, 'Total', r.totalE4, bold: true),
              _row(context, 'Paid (${r.paymentMethod})', r.amountPaidE4),
              if (r.changeE4 > 0)
                _row(
                  context,
                  'Change',
                  r.changeE4,
                  bold: true,
                  color: Colors.green.shade700,
                ),
              if (due > 0)
                _row(context, 'Remaining due', due, color: scheme.error),
              if (widget.footer?.trim().isNotEmpty ?? false) ...[
                const SizedBox(height: 12),
                Text(widget.footer!.trim()),
              ],
              const SizedBox(height: 12),
              const Text('This receipt reflects the sale saved locally.'),
            ],
          ),
        ),
      ),
      actions: [
        TextButton.icon(
          onPressed: _copy,
          icon: Icon(_copied ? Icons.check : Icons.copy),
          label: Text(_copied ? 'Copied' : 'Copy receipt'),
        ),
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Close'),
        ),
      ],
    );
  }

  Widget _row(
    BuildContext context,
    String label,
    int value, {
    bool bold = false,
    Color? color,
    String prefix = '',
  }) {
    var style = bold
        ? Theme.of(context).textTheme.titleMedium
              ?.copyWith(fontWeight: FontWeight.w800)
        : null;
    if (color != null) {
      style = (style ?? const TextStyle()).copyWith(color: color);
    }
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text('${prefix}Rs ${PosFormat.money(value)}', style: style),
        ],
      ),
    );
  }
}
