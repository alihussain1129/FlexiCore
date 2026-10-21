import 'package:flutter/material.dart';

import '../../../core/money.dart';
import '../domain/catalog_domain.dart';
import 'catalog_controller.dart';

/// Local catalog: units and products, read from and written to the local
/// database. Never calls the API.
class CatalogScreen extends StatelessWidget {
  /// Extra app-bar buttons supplied by the host app (for example a menu).
  final List<Widget> actions;

  const CatalogScreen({
    super.key,
    required this.controller,
    this.actions = const [],
  });

  final CatalogController controller;

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('Catalog (local)'),
          actions: actions,
          bottom: const TabBar(
            tabs: [
              Tab(text: 'Products'),
              Tab(text: 'Units'),
            ],
          ),
        ),
        body: ListenableBuilder(
          listenable: controller,
          builder: (context, _) {
            if (controller.loadError != null) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Text(controller.loadError!),
                ),
              );
            }
            return TabBarView(
              children: [
                _ProductsTab(controller: controller),
                _UnitsTab(controller: controller),
              ],
            );
          },
        ),
      ),
    );
  }
}

Future<bool> _confirm(BuildContext context, String message) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: const Text('Delete?'),
      content: Text(message),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(ctx, false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () => Navigator.pop(ctx, true),
          child: const Text('Delete'),
        ),
      ],
    ),
  );
  return result ?? false;
}

void _toast(BuildContext context, String message) {
  ScaffoldMessenger.of(context)
    ..hideCurrentSnackBar()
    ..showSnackBar(SnackBar(content: Text(message)));
}

// ------------------------------------------------------------------ units

class _UnitsTab extends StatelessWidget {
  const _UnitsTab({required this.controller});

  final CatalogController controller;

  @override
  Widget build(BuildContext context) {
    final units = controller.units;
    return Column(
      children: [
        Align(
          alignment: Alignment.centerRight,
          child: Padding(
            padding: const EdgeInsets.all(12),
            child: FilledButton.icon(
              onPressed: () => showDialog<void>(
                context: context,
                builder: (_) => _UnitDialog(controller: controller),
              ),
              icon: const Icon(Icons.add),
              label: const Text('Add unit'),
            ),
          ),
        ),
        Expanded(
          child: units.isEmpty
              ? const Center(child: Text('No units yet.'))
              : ListView.separated(
                  itemCount: units.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, i) {
                    final unit = units[i];
                    return ListTile(
                      title: Text(unit.code),
                      subtitle: Text(unit.name),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          IconButton(
                            tooltip: 'Edit',
                            icon: const Icon(Icons.edit_outlined),
                            onPressed: () => showDialog<void>(
                              context: context,
                              builder: (_) => _UnitDialog(
                                controller: controller,
                                unit: unit,
                              ),
                            ),
                          ),
                          IconButton(
                            tooltip: 'Delete',
                            icon: const Icon(Icons.delete_outline),
                            onPressed: () async {
                              final ok = await _confirm(
                                context,
                                'Delete unit ${unit.code}?',
                              );
                              if (!ok) return;
                              final error = await controller.removeUnit(unit);
                              if (error != null && context.mounted) {
                                _toast(context, error);
                              }
                            },
                          ),
                        ],
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}

class _UnitDialog extends StatefulWidget {
  const _UnitDialog({required this.controller, this.unit});

  final CatalogController controller;
  final CatalogUnit? unit;

  @override
  State<_UnitDialog> createState() => _UnitDialogState();
}

class _UnitDialogState extends State<_UnitDialog> {
  late final TextEditingController _code = TextEditingController(
    text: widget.unit?.code ?? '',
  );
  late final TextEditingController _name = TextEditingController(
    text: widget.unit?.name ?? '',
  );
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _code.dispose();
    _name.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    final unit = widget.unit;
    final error = unit == null
        ? await widget.controller.addUnit(_code.text, _name.text)
        : await widget.controller.saveUnit(unit, _code.text, _name.text);
    if (!mounted) return;
    if (error == null) {
      Navigator.pop(context);
    } else {
      setState(() {
        _error = error;
        _saving = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      title: Text(widget.unit == null ? 'New unit' : 'Edit unit'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          TextField(
            controller: _code,
            decoration: const InputDecoration(labelText: 'Code (e.g. KG)'),
          ),
          TextField(
            controller: _name,
            decoration: const InputDecoration(labelText: 'Name'),
          ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Text(
                _error!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.pop(context),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: const Text('Save'),
        ),
      ],
    );
  }
}

// --------------------------------------------------------------- products

class _ProductsTab extends StatelessWidget {
  const _ProductsTab({required this.controller});

  final CatalogController controller;

  @override
  Widget build(BuildContext context) {
    final products = controller.products;
    final unitCodes = {for (final u in controller.units) u.id: u.code};
    return Column(
      children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: Row(
            children: [
              Expanded(
                child: TextField(
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search),
                    hintText: 'Search name, SKU or barcode',
                  ),
                  onChanged: controller.search,
                ),
              ),
              const SizedBox(width: 12),
              FilledButton.icon(
                onPressed: () => showDialog<void>(
                  context: context,
                  builder: (_) => _ProductDialog(controller: controller),
                ),
                icon: const Icon(Icons.add),
                label: const Text('Add product'),
              ),
            ],
          ),
        ),
        Expanded(
          child: products.isEmpty
              ? Center(
                  child: Text(
                    controller.query.isEmpty
                        ? 'No products yet.'
                        : 'No products match "${controller.query}".',
                  ),
                )
              : ListView.separated(
                  itemCount: products.length,
                  separatorBuilder: (_, _) => const Divider(height: 1),
                  itemBuilder: (context, i) {
                    final p = products[i];
                    final unit = unitCodes[p.unitId];
                    return ListTile(
                      title: Text(p.name),
                      subtitle: Text(
                        [
                          'SKU ${p.sku}',
                          ?unit,
                          'Sale ${Money.formatE4(p.salePriceE4)}',
                          'Cost ${Money.formatE4(p.costPriceE4)}',
                        ].join('  ·  '),
                      ),
                      trailing: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          IconButton(
                            tooltip: 'Edit',
                            icon: const Icon(Icons.edit_outlined),
                            onPressed: () => showDialog<void>(
                              context: context,
                              builder: (_) => _ProductDialog(
                                controller: controller,
                                product: p,
                              ),
                            ),
                          ),
                          IconButton(
                            tooltip: 'Delete',
                            icon: const Icon(Icons.delete_outline),
                            onPressed: () async {
                              final ok = await _confirm(
                                context,
                                'Delete product ${p.name}?',
                              );
                              if (!ok) return;
                              final error = await controller.removeProduct(p);
                              if (error != null && context.mounted) {
                                _toast(context, error);
                              }
                            },
                          ),
                        ],
                      ),
                    );
                  },
                ),
        ),
      ],
    );
  }
}

class _ProductDialog extends StatefulWidget {
  const _ProductDialog({required this.controller, this.product});

  final CatalogController controller;
  final CatalogProduct? product;

  @override
  State<_ProductDialog> createState() => _ProductDialogState();
}

class _ProductDialogState extends State<_ProductDialog> {
  late final TextEditingController _sku = TextEditingController(
    text: widget.product?.sku ?? '',
  );
  late final TextEditingController _name = TextEditingController(
    text: widget.product?.name ?? '',
  );
  late final TextEditingController _barcode = TextEditingController(
    text: widget.product?.barcode ?? '',
  );
  late final TextEditingController _sale = TextEditingController(
    text: widget.product == null
        ? ''
        : Money.formatE4(widget.product!.salePriceE4),
  );
  late final TextEditingController _cost = TextEditingController(
    text: widget.product == null
        ? ''
        : Money.formatE4(widget.product!.costPriceE4),
  );
  late String? _unitId = widget.product?.unitId;
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _sku.dispose();
    _name.dispose();
    _barcode.dispose();
    _sale.dispose();
    _cost.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _saving = true);
    final product = widget.product;
    final error = product == null
        ? await widget.controller.addProduct(
            sku: _sku.text,
            name: _name.text,
            barcode: _barcode.text,
            unitId: _unitId,
            salePrice: _sale.text,
            costPrice: _cost.text,
          )
        : await widget.controller.saveProduct(
            product,
            sku: _sku.text,
            name: _name.text,
            barcode: _barcode.text,
            unitId: _unitId,
            salePrice: _sale.text,
            costPrice: _cost.text,
          );
    if (!mounted) return;
    if (error == null) {
      Navigator.pop(context);
    } else {
      setState(() {
        _error = error;
        _saving = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final units = widget.controller.units;
    return AlertDialog(
      title: Text(widget.product == null ? 'New product' : 'Edit product'),
      content: SizedBox(
        width: 420,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: _sku,
                decoration: const InputDecoration(labelText: 'SKU'),
              ),
              TextField(
                controller: _name,
                decoration: const InputDecoration(labelText: 'Name'),
              ),
              TextField(
                controller: _barcode,
                decoration: const InputDecoration(
                  labelText: 'Barcode (optional)',
                ),
              ),
              DropdownButtonFormField<String?>(
                initialValue: _unitId,
                decoration: const InputDecoration(labelText: 'Unit'),
                items: [
                  const DropdownMenuItem<String?>(
                    value: null,
                    child: Text('No unit'),
                  ),
                  for (final u in units)
                    DropdownMenuItem<String?>(
                      value: u.id,
                      child: Text('${u.code} - ${u.name}'),
                    ),
                ],
                onChanged: (v) => setState(() => _unitId = v),
              ),
              TextField(
                controller: _sale,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(labelText: 'Sale price'),
              ),
              TextField(
                controller: _cost,
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                decoration: const InputDecoration(labelText: 'Cost price'),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(
                    _error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.pop(context),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: const Text('Save'),
        ),
      ],
    );
  }
}
