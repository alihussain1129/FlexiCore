import 'dart:math' as math;

import 'package:drift/drift.dart' hide Column;
import 'package:flutter/material.dart';

import '../../../core/ids.dart';
import '../../../data/local/app_database.dart';
import '../../catalog/data/local_catalog_repository.dart';
import '../../catalog/domain/catalog_domain.dart';
import '../../inventory/data/local_inventory_repository.dart';
import '../data/local/local_pos_repository.dart';

class PosScreen extends StatefulWidget {
  const PosScreen({
    super.key,
    required this.db,
    required this.companyId,
    this.businessName,
  });

  final AppDatabase db;
  final String companyId;
  final String? businessName;

  @override
  State<PosScreen> createState() => _PosScreenState();
}

class _CartLine {
  _CartLine(this.product);

  final CatalogProduct product;
  int quantityE4 = 10000;
}

class _PosScreenState extends State<PosScreen> {
  late final LocalCatalogRepository _catalog;
  late final LocalPosRepository _pos;

  final _search = TextEditingController();
  final _paid = TextEditingController(text: '0');
  final _cashier = TextEditingController(text: 'Cashier');
  final Map<String, _CartLine> _cart = {};

  List<CatalogProduct> _products = [];
  final Map<String, int> _stockByProduct = {};
  String? _warehouseId;
  String? _error;
  String _paymentMethod = 'Cash';
  bool _loading = true;
  bool _saving = false;
  bool _paymentEdited = false;
  int _searchVersion = 0;

  int get _totalE4 => _cart.values.fold<int>(
    0,
    (sum, line) => sum + _lineTotal(line.quantityE4, line.product.salePriceE4),
  );

  @override
  void initState() {
    super.initState();
    _catalog = LocalCatalogRepository(widget.db, companyId: widget.companyId);
    _pos = LocalPosRepository(widget.db, companyId: widget.companyId);
    _search.addListener(_loadProducts);
    _initialize();
  }

  Future<void> _initialize() async {
    try {
      await LocalInventoryRepository(
        widget.db,
        companyId: widget.companyId,
      ).ensureDefaultWarehouse();

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

  int _lineTotal(int quantityE4, int priceE4) =>
      (quantityE4 * priceE4 + 5000) ~/ 10000;

  String _money(int value) {
    final whole = value.abs() ~/ 10000;
    final fraction = (value.abs() % 10000).toString().padLeft(4, '0');
    final decimals = fraction.replaceFirst(RegExp(r'0+$'), '');
    return '${value < 0 ? '-' : ''}$whole${decimals.isEmpty ? '' : '.$decimals'}';
  }

  int _parseMoney(String input) {
    final match = RegExp(r'^(\d+)(?:\.(\d{1,4}))?$').firstMatch(input.trim());
    if (match == null) {
      throw const PosException(
        'Enter a valid amount with up to four decimal places.',
      );
    }
    return int.parse(match.group(1)!) * 10000 +
        int.parse((match.group(2) ?? '').padRight(4, '0'));
  }

  void _add(CatalogProduct product) {
    final availableE4 = _stockByProduct[product.id] ?? 0;
    final currentE4 = _cart[product.id]?.quantityE4 ?? 0;
    if (availableE4 <= 0) {
      _message('No available stock for ${product.name} in this warehouse.');
      return;
    }
    if (currentE4 + 10000 > availableE4) {
      _message(
        'Not enough stock for ${product.name}. '
        'Available: ${_money(availableE4)}.',
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
      if (!_paymentEdited) _paid.text = _money(_totalE4);
    });
  }

  void _changeQuantity(String id, int delta) {
    final line = _cart[id];
    if (line == null) return;
    final next = line.quantityE4 + delta;
    final availableE4 = _stockByProduct[id] ?? 0;
    if (delta > 0 && next > availableE4) {
      _message(
        'Not enough stock for ${line.product.name}. '
        'Available: ${_money(availableE4)}.',
      );
      return;
    }
    setState(() {
      if (next <= 0) {
        _cart.remove(id);
      } else {
        line.quantityE4 = next;
      }
      if (!_paymentEdited) _paid.text = _money(_totalE4);
    });
  }

  void _message(String value) {
    if (!mounted) return;
    ScaffoldMessenger.of(context)
      ..hideCurrentSnackBar()
      ..showSnackBar(SnackBar(content: Text(value)));
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

    late final int amountPaid;
    try {
      amountPaid = _parseMoney(_paid.text);
    } catch (e) {
      _message(e is PosException ? e.message : 'Enter a valid payment amount.');
      return;
    }

    final cashier = _cashier.text.trim();
    if (cashier.isEmpty) {
      _message('Enter the cashier name.');
      return;
    }

    final items = _cart.values
        .map(
          (line) => PosCartItem(
            productId: line.product.id,
            quantityE4: line.quantityE4,
          ),
        )
        .toList();

    setState(() => _saving = true);
    try {
      final receipt = await _pos.checkout(
        PosCheckoutRequest(
          clientRequestId: generateUuidV4(),
          items: items,
          paymentMethod: _paymentMethod,
          amountPaidE4: amountPaid,
          createdBy: cashier,
          warehouseId: _warehouseId,
        ),
      );

      if (!mounted) return;
      setState(() {
        _cart.clear();
        _paymentEdited = false;
        _paid.text = '0';
      });

      await showDialog<void>(
        context: context,
        builder: (context) => _ReceiptDialog(
          receipt: receipt,
          businessName: widget.businessName,
          money: _money,
        ),
      );
      await _loadProducts();
    } catch (e) {
      if (!mounted) return;
      _message(e is PosException ? e.message : 'Checkout failed: $e');
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  @override
  void dispose() {
    _search
      ..removeListener(_loadProducts)
      ..dispose();
    _paid.dispose();
    _cashier.dispose();
    super.dispose();
  }

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
        actions: const [
          Padding(
            padding: EdgeInsets.symmetric(horizontal: 12),
            child: Center(
              child: Chip(
                avatar: Icon(Icons.wifi_off, size: 17),
                label: Text('Offline'),
              ),
            ),
          ),
        ],
      ),
      body: LayoutBuilder(
        builder: (context, constraints) {
          final wide = constraints.maxWidth >= 900;
          final products = _productPanel();
          final cart = _cartPanel();

          if (wide) {
            return Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Expanded(flex: 6, child: products),
                  const SizedBox(width: 16),
                  Expanded(flex: 5, child: cart),
                ],
              ),
            );
          }

          return Padding(
            padding: const EdgeInsets.all(12),
            child: Column(
              children: [
                Expanded(child: products),
                const SizedBox(height: 10),
                SizedBox(
                  height: math.min(360, constraints.maxHeight * 0.46),
                  child: cart,
                ),
              ],
            ),
          );
        },
      ),
    );
  }

  Widget _productPanel() {
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(12),
            child: TextField(
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
                border: const OutlineInputBorder(),
              ),
            ),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
            child: Row(
              children: [
                Text(
                  'Products',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const Spacer(),
                Text('${_products.length} found'),
              ],
            ),
          ),
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
                    itemBuilder: (context, index) {
                      final product = _products[index];
                      return ListTile(
                        title: Text(
                          product.name,
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                        ),
                        subtitle: Text('SKU: ${product.sku}'),
                        leading: const CircleAvatar(
                          child: Icon(Icons.inventory_2_outlined),
                        ),
                        trailing: Text(
                          'Rs ${_money(product.salePriceE4)}',
                          style: const TextStyle(fontWeight: FontWeight.bold),
                        ),
                        onTap: () => _add(product),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _cartPanel() {
    final total = _totalE4;
    return Card(
      margin: EdgeInsets.zero,
      clipBehavior: Clip.antiAlias,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 6, 8, 6),
            child: Row(
              children: [
                const Icon(Icons.shopping_cart_outlined),
                const SizedBox(width: 8),
                Text(
                  'Current sale',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const Spacer(),
                TextButton(
                  onPressed: _cart.isEmpty
                      ? null
                      : () => setState(() {
                          _cart.clear();
                          if (!_paymentEdited) _paid.text = '0';
                        }),
                  child: const Text('Clear'),
                ),
              ],
            ),
          ),
          const Divider(height: 1),
          Expanded(
            flex: 2,
            child: _cart.isEmpty
                ? const Center(child: Text('Your cart is empty'))
                : ListView(
                    children: [
                      for (final line in _cart.values)
                        ListTile(
                          dense: true,
                          title: Text(
                            line.product.name,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          subtitle: Text(
                            '${_money(line.quantityE4)} × Rs ${_money(line.product.salePriceE4)}',
                          ),
                          trailing: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              IconButton(
                                tooltip: 'Decrease quantity',
                                onPressed: () =>
                                    _changeQuantity(line.product.id, -10000),
                                icon: const Icon(Icons.remove_circle_outline),
                              ),
                              Text(_money(line.quantityE4)),
                              IconButton(
                                tooltip: 'Increase quantity',
                                onPressed: () =>
                                    _changeQuantity(line.product.id, 10000),
                                icon: const Icon(Icons.add_circle_outline),
                              ),
                            ],
                          ),
                        ),
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
                  _totalRow('Subtotal', total),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      const Expanded(child: Text('Amount received')),
                      SizedBox(
                        width: 140,
                        child: TextField(
                          controller: _paid,
                          keyboardType: const TextInputType.numberWithOptions(
                            decimal: true,
                          ),
                          textAlign: TextAlign.end,
                          decoration: const InputDecoration(
                            prefixText: 'Rs ',
                            isDense: true,
                            border: OutlineInputBorder(),
                          ),
                          onChanged: (_) => _paymentEdited = true,
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      const Expanded(child: Text('Payment method')),
                      DropdownButton<String>(
                        value: _paymentMethod,
                        items: const [
                          DropdownMenuItem(value: 'Cash', child: Text('Cash')),
                          DropdownMenuItem(value: 'Card', child: Text('Card')),
                          DropdownMenuItem(value: 'Bank', child: Text('Bank')),
                          DropdownMenuItem(
                            value: 'Other',
                            child: Text('Other'),
                          ),
                        ],
                        onChanged: (value) {
                          if (value != null) {
                            setState(() => _paymentMethod = value);
                          }
                        },
                      ),
                    ],
                  ),
                  TextField(
                    controller: _cashier,
                    decoration: const InputDecoration(
                      labelText: 'Cashier',
                      isDense: true,
                      border: OutlineInputBorder(),
                    ),
                  ),
                  const SizedBox(height: 8),
                  _totalRow('TOTAL', total, bold: true),
                  const SizedBox(height: 10),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton.icon(
                      onPressed: _saving || _cart.isEmpty ? null : _checkout,
                      icon: _saving
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Icon(Icons.point_of_sale),
                      label: Text(_saving ? 'Saving sale…' : 'Complete sale'),
                    ),
                  ),
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

  Widget _totalRow(String label, int value, {bool bold = false}) {
    final style = bold
        ? Theme.of(context).textTheme.titleMedium
              ?.copyWith(fontWeight: FontWeight.w800)
        : null;
    return Row(
      children: [
        Expanded(child: Text(label, style: style)),
        Text('Rs ${_money(value)}', style: style),
      ],
    );
  }
}

class _ReceiptDialog extends StatelessWidget {
  const _ReceiptDialog({
    required this.receipt,
    required this.businessName,
    required this.money,
  });

  final PosReceipt receipt;
  final String? businessName;
  final String Function(int) money;

  @override
  Widget build(BuildContext context) {
    final due = receipt.totalE4 - receipt.amountPaidE4;
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
              if (businessName?.trim().isNotEmpty ?? false)
                Text(
                  businessName!,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
              const Text('OFFLINE SALES RECEIPT'),
              const SizedBox(height: 6),
              SelectableText('Receipt: ${receipt.receiptNumber}'),
              SelectableText('Date: ${receipt.createdAt.toLocal()}'),
              const Divider(height: 20),
              for (final line in receipt.lines)
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
                              '${money(line.quantityE4)} × Rs ${money(line.unitPriceE4)}',
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 12),
                      Text('Rs ${money(line.lineTotalE4)}'),
                    ],
                  ),
                ),
              const Divider(height: 20),
              _row(context, 'Subtotal', receipt.subtotalE4),
              _row(context, 'Total', receipt.totalE4, bold: true),
              _row(context, 'Received', receipt.amountPaidE4),
              _row(context, 'Remaining due', due),
              const SizedBox(height: 8),
              Text('Payment: ${receipt.paymentMethod}'),
              const SizedBox(height: 12),
              const Text('This receipt reflects the sale saved locally.'),
            ],
          ),
        ),
      ),
      actions: [
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
  }) {
    final style = bold
        ? Theme.of(context).textTheme.titleMedium
              ?.copyWith(fontWeight: FontWeight.w800)
        : null;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: [
          Expanded(child: Text(label, style: style)),
          Text('Rs ${money(value)}', style: style),
        ],
      ),
    );
  }
}
