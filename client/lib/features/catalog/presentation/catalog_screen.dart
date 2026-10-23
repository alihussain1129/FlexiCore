import 'package:flutter/material.dart';
import '../../../core/read_only.dart';

import '../../../core/app_theme.dart';
import '../../../core/ids.dart';
import '../../../core/money.dart';
import '../../inventory/domain/inventory_domain.dart' show parseQtyE4;
import '../../../core/widgets/app_widgets.dart' show PageHeader;
import '../domain/catalog_domain.dart';
import 'catalog_controller.dart';

double _hPad(BuildContext context) =>
    MediaQuery.sizeOf(context).width < 600 ? 16 : 28;

/// Local catalog: units and products, read from and written to the local
/// database. Never calls the API.
class CatalogScreen extends StatelessWidget {
  const CatalogScreen({
    super.key,
    required this.controller,
    this.actions = const [],
    this.businessName,
  });

  final CatalogController controller;

  /// Extra header buttons supplied by the host app (for example a menu).
  final List<Widget> actions;

  /// Shown as the page title.
  final String? businessName;

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        backgroundColor: AppColors.paper,
        body: ListenableBuilder(
          listenable: controller,
          builder: (context, _) {
            return Column(
              children: [
                _Header(
                  productCount: controller.products.length,
                  unitCount: controller.units.length,
                  businessName: businessName,
                  actions: actions,
                ),
                Expanded(
                  child: controller.loadError != null
                      ? _EmptyState(
                          icon: Icons.error_outline,
                          title: 'Something went wrong',
                          message: controller.loadError!,
                        )
                      : TabBarView(
                          children: [
                            _ProductsTab(controller: controller),
                            _UnitsTab(controller: controller),
                          ],
                        ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

// ----------------------------------------------------------------- header

class _Header extends StatelessWidget {
  const _Header({
    required this.productCount,
    required this.unitCount,
    required this.businessName,
    required this.actions,
  });

  final int productCount;
  final int unitCount;
  final String? businessName;
  final List<Widget> actions;

  Tab _tab(IconData icon, String text, int count) => Tab(
    height: 42,
    child: Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(icon, size: 18),
        const SizedBox(width: 8),
        Flexible(
          child: Text(
            '$text ($count)',
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
          ),
        ),
      ],
    ),
  );

  @override
  Widget build(BuildContext context) {
    final name = (businessName == null || businessName!.trim().isEmpty)
        ? 'My business'
        : businessName!.trim();
    return PageHeader(
      title: name,
      subtitle: 'Your products and units',
      actions: actions,
      bottom: Container(
        padding: const EdgeInsets.all(4),
        decoration: BoxDecoration(
          color: Colors.white.withValues(alpha: 0.12),
          borderRadius: BorderRadius.circular(14),
        ),
        child: TabBar(
          dividerColor: Colors.transparent,
          indicatorSize: TabBarIndicatorSize.tab,
          indicator: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(11),
          ),
          labelColor: AppColors.teal,
          unselectedLabelColor: Colors.white,
          labelStyle: const TextStyle(fontWeight: FontWeight.w700),
          splashBorderRadius: BorderRadius.circular(11),
          tabs: [
            _tab(Icons.inventory_2_outlined, 'Products', productCount),
            _tab(Icons.straighten, 'Units', unitCount),
          ],
        ),
      ),
    );
  }
}

// ---------------------------------------------------------------- shared

class _Page extends StatelessWidget {
  const _Page({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final h = _hPad(context);
    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 1100),
        child: Padding(padding: EdgeInsets.fromLTRB(h, 18, h, 0), child: child),
      ),
    );
  }
}

class _Entrance extends StatelessWidget {
  const _Entrance({required this.index, required this.child});

  final int index;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final step = index > 8 ? 8 : index;
    return TweenAnimationBuilder<double>(
      tween: Tween(begin: 0, end: 1),
      duration: Duration(milliseconds: 280 + step * 40),
      curve: Curves.easeOutCubic,
      builder: (context, t, child) => Opacity(
        opacity: t,
        child: Transform.translate(
          offset: Offset(0, 14 * (1 - t)),
          child: child,
        ),
      ),
      child: child,
    );
  }
}

class _HoverCard extends StatefulWidget {
  const _HoverCard({required this.child});

  final Widget child;

  @override
  State<_HoverCard> createState() => _HoverCardState();
}

class _HoverCardState extends State<_HoverCard> {
  bool _hover = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      onEnter: (_) => setState(() => _hover = true),
      onExit: (_) => setState(() => _hover = false),
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 180),
        curve: Curves.easeOut,
        transform: Matrix4.translationValues(0, _hover ? -2 : 0, 0),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(
            color: _hover
                ? AppColors.teal.withValues(alpha: 0.35)
                : AppColors.line,
          ),
          boxShadow: [
            BoxShadow(
              color: AppColors.ink.withValues(alpha: _hover ? 0.12 : 0.05),
              blurRadius: _hover ? 22 : 10,
              offset: Offset(0, _hover ? 10 : 4),
            ),
          ],
        ),
        child: widget.child,
      ),
    );
  }
}

class _EmptyState extends StatelessWidget {
  const _EmptyState({
    required this.icon,
    required this.title,
    required this.message,
    this.actionLabel,
    this.onAction,
  });

  final IconData icon;
  final String title;
  final String message;
  final String? actionLabel;
  final VoidCallback? onAction;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: _Entrance(
          index: 0,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                width: 76,
                height: 76,
                decoration: const BoxDecoration(
                  color: AppColors.tealSoft,
                  shape: BoxShape.circle,
                ),
                child: Icon(icon, size: 34, color: AppColors.teal),
              ),
              const SizedBox(height: 18),
              Text(
                title,
                textAlign: TextAlign.center,
                style: const TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.w700,
                ),
              ),
              const SizedBox(height: 6),
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 340),
                child: Text(
                  message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: AppColors.muted, height: 1.4),
                ),
              ),
              if (actionLabel != null && onAction != null) ...[
                const SizedBox(height: 18),
                FilledButton.icon(
                  onPressed: isReadOnlyOf(context) ? null : onAction,
                  icon: const Icon(Icons.add),
                  label: Text(actionLabel!),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _RowActions extends StatelessWidget {
  const _RowActions({required this.onEdit, required this.onDelete});

  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        IconButton(
          tooltip: 'Edit',
          onPressed: isReadOnlyOf(context) ? null : onEdit,
          icon: const Icon(Icons.edit_outlined),
          color: AppColors.teal,
        ),
        IconButton(
          tooltip: 'Delete',
          onPressed: isReadOnlyOf(context) ? null : onDelete,
          icon: const Icon(Icons.delete_outline),
          color: AppColors.danger,
        ),
      ],
    );
  }
}

class _Avatar extends StatelessWidget {
  const _Avatar({required this.text, required this.bg, required this.fg});

  final String text;
  final Color bg;
  final Color fg;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 46,
      height: 46,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(14),
      ),
      child: Padding(
        padding: const EdgeInsets.all(6),
        child: FittedBox(
          child: Text(
            text,
            style: TextStyle(
              color: fg,
              fontSize: 18,
              fontWeight: FontWeight.w700,
            ),
          ),
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
          style: FilledButton.styleFrom(backgroundColor: AppColors.danger),
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

class _DialogTitle extends StatelessWidget {
  const _DialogTitle({required this.icon, required this.text});

  final IconData icon;
  final String text;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.tealSoft,
            borderRadius: BorderRadius.circular(12),
          ),
          child: Icon(icon, color: AppColors.teal, size: 22),
        ),
        const SizedBox(width: 12),
        Flexible(child: Text(text)),
      ],
    );
  }
}

class _ErrorText extends StatelessWidget {
  const _ErrorText(this.error);

  final String? error;

  @override
  Widget build(BuildContext context) {
    return AnimatedSize(
      duration: const Duration(milliseconds: 200),
      curve: Curves.easeOut,
      child: error == null
          ? const SizedBox(width: double.infinity)
          : Padding(
              padding: const EdgeInsets.only(top: 14),
              child: Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: AppColors.danger.withValues(alpha: 0.08),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Icon(
                      Icons.error_outline,
                      size: 18,
                      color: AppColors.danger,
                    ),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        error!,
                        style: const TextStyle(color: AppColors.danger),
                      ),
                    ),
                  ],
                ),
              ),
            ),
    );
  }
}

Widget _saveLabel(bool saving) => saving
    ? const SizedBox(
        width: 18,
        height: 18,
        child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white),
      )
    : const Text('Save');

// ------------------------------------------------------------------ units

class _UnitsTab extends StatelessWidget {
  const _UnitsTab({required this.controller});

  final CatalogController controller;

  void _add(BuildContext context) => showDialog<void>(
    context: context,
    builder: (_) => _UnitDialog(controller: controller),
  );

  @override
  Widget build(BuildContext context) {
    final units = controller.units;
    return _Page(
      child: Column(
        children: [
          Align(
            alignment: Alignment.centerRight,
            child: FilledButton.icon(
              onPressed: isReadOnlyOf(context) ? null : () => _add(context),
              icon: const Icon(Icons.add),
              label: const Text('Add unit'),
            ),
          ),
          const SizedBox(height: 14),
          Expanded(
            child: units.isEmpty
                ? _EmptyState(
                    icon: Icons.straighten,
                    title: 'No units yet',
                    message:
                        'Units describe how you sell things, like pieces, '
                        'boxes or kilograms. Add one to use it on products.',
                    actionLabel: 'Add unit',
                    onAction: () => _add(context),
                  )
                : ListView.builder(
                    padding: const EdgeInsets.only(bottom: 32),
                    itemCount: units.length,
                    itemBuilder: (context, i) {
                      final unit = units[i];
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: _Entrance(
                          index: i,
                          child: _HoverCard(
                            child: Padding(
                              padding: const EdgeInsets.all(14),
                              child: Row(
                                children: [
                                  _Avatar(
                                    text: unit.code,
                                    bg: AppColors.claySoft,
                                    fg: AppColors.clay,
                                  ),
                                  const SizedBox(width: 14),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          unit.name,
                                          maxLines: 2,
                                          overflow: TextOverflow.ellipsis,
                                          style: const TextStyle(
                                            fontSize: 16,
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                        const SizedBox(height: 2),
                                        Text(
                                          'Code: ${unit.code}',
                                          style: const TextStyle(
                                            color: AppColors.muted,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                  _RowActions(
                                    onEdit: () => showDialog<void>(
                                      context: context,
                                      builder: (_) => _UnitDialog(
                                        controller: controller,
                                        unit: unit,
                                      ),
                                    ),
                                    onDelete: () async {
                                      final ok = await _confirm(
                                        context,
                                        'Delete unit ${unit.code}?',
                                      );
                                      if (!ok) return;
                                      final error = await controller.removeUnit(
                                        unit,
                                      );
                                      if (error != null && context.mounted) {
                                        _toast(context, error);
                                      }
                                    },
                                  ),
                                ],
                              ),
                            ),
                          ),
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
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
    if (_saving) return;
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
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      title: _DialogTitle(
        icon: Icons.straighten,
        text: widget.unit == null ? 'New unit' : 'Edit unit',
      ),
      content: SizedBox(
        width: 400,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const SizedBox(height: 4),
              TextField(
                controller: _code,
                autofocus: true,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(
                  labelText: 'Code',
                  helperText: 'A short label, for example KG or PCS',
                ),
              ),
              const SizedBox(height: 14),
              TextField(
                controller: _name,
                textInputAction: TextInputAction.done,
                onSubmitted: (_) => _save(),
                decoration: const InputDecoration(
                  labelText: 'Name',
                  helperText: 'The full name, for example Kilogram',
                ),
              ),
              _ErrorText(_error),
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
          child: _saveLabel(_saving),
        ),
      ],
    );
  }
}

// --------------------------------------------------------------- products

class _ProductsTab extends StatelessWidget {
  const _ProductsTab({required this.controller});

  final CatalogController controller;

  void _add(BuildContext context) => showDialog<void>(
    context: context,
    builder: (_) => _ProductDialog(controller: controller),
  );

  @override
  Widget build(BuildContext context) {
    final products = controller.products;
    final unitCodes = {for (final u in controller.units) u.id: u.code};

    final search = TextField(
      onChanged: controller.search,
      decoration: const InputDecoration(
        prefixIcon: Icon(Icons.search),
        hintText: 'Search by name, SKU or barcode',
      ),
    );
    final add = FilledButton.icon(
      onPressed: isReadOnlyOf(context) ? null : () => _add(context),
      icon: const Icon(Icons.add),
      label: const Text('Add product'),
    );

    return _Page(
      child: Column(
        children: [
          LayoutBuilder(
            builder: (context, c) {
              if (c.maxWidth < 560) {
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [search, const SizedBox(height: 10), add],
                );
              }
              return Row(
                children: [
                  Expanded(child: search),
                  const SizedBox(width: 12),
                  add,
                ],
              );
            },
          ),
          const SizedBox(height: 14),
          Expanded(
            child: products.isEmpty
                ? (controller.query.isEmpty
                      ? _EmptyState(
                          icon: Icons.inventory_2_outlined,
                          title: 'No products yet',
                          message:
                              'Add your first product to start building '
                              'your catalog.',
                          actionLabel: 'Add product',
                          onAction: () => _add(context),
                        )
                      : _EmptyState(
                          icon: Icons.search_off,
                          title: 'No matches',
                          message:
                              'Nothing matches "${controller.query}". Try a '
                              'different name, SKU or barcode.',
                        ))
                : ListView.builder(
                    padding: const EdgeInsets.only(bottom: 32),
                    itemCount: products.length,
                    itemBuilder: (context, i) {
                      final p = products[i];
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 10),
                        child: _Entrance(
                          index: i,
                          child: _ProductCard(
                            product: p,
                            unitCode: unitCodes[p.unitId],
                            onEdit: () => showDialog<void>(
                              context: context,
                              builder: (_) => _ProductDialog(
                                controller: controller,
                                product: p,
                              ),
                            ),
                            onDelete: () async {
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
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}

class _ProductCard extends StatelessWidget {
  const _ProductCard({
    required this.product,
    required this.unitCode,
    required this.onEdit,
    required this.onDelete,
  });

  final CatalogProduct product;
  final String? unitCode;
  final VoidCallback onEdit;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final name = product.name;
    final letter = name.isEmpty
        ? '?'
        : String.fromCharCode(name.runes.first).toUpperCase();
    final barcode = product.barcode;
    final sub = StringBuffer('SKU ${product.sku}');
    if (barcode != null && barcode.isNotEmpty) {
      sub.write('   Barcode $barcode');
    }
    final sale = Money.formatE4(product.salePriceE4);
    final cost = Money.formatE4(product.costPriceE4);

    final title = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          name,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: 2),
        Text(
          sub.toString(),
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(color: AppColors.muted),
        ),
      ],
    );

    return _HoverCard(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: LayoutBuilder(
          builder: (context, c) {
            final avatar = _Avatar(
              text: letter,
              bg: AppColors.tealSoft,
              fg: AppColors.teal,
            );
            final actions = _RowActions(onEdit: onEdit, onDelete: onDelete);
            if (c.maxWidth >= 640) {
              return Row(
                children: [
                  avatar,
                  const SizedBox(width: 14),
                  Expanded(flex: 5, child: title),
                  if (unitCode != null) ...[
                    const SizedBox(width: 12),
                    _UnitChip(unitCode!),
                  ],
                  const SizedBox(width: 12),
                  _Price(label: 'Sale', value: sale, strong: true),
                  _Price(label: 'Cost', value: cost, strong: false),
                  const SizedBox(width: 4),
                  actions,
                ],
              );
            }
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    avatar,
                    const SizedBox(width: 12),
                    Expanded(child: title),
                    actions,
                  ],
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    if (unitCode != null) _UnitChip(unitCode!),
                    _MiniPrice(label: 'Sale', value: sale, strong: true),
                    _MiniPrice(label: 'Cost', value: cost, strong: false),
                  ],
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class _UnitChip extends StatelessWidget {
  const _UnitChip(this.code);

  final String code;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: AppColors.tealSoft,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        code,
        style: const TextStyle(
          color: AppColors.teal,
          fontWeight: FontWeight.w700,
          fontSize: 12,
        ),
      ),
    );
  }
}

class _Price extends StatelessWidget {
  const _Price({
    required this.label,
    required this.value,
    required this.strong,
  });

  final String label;
  final String value;
  final bool strong;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: 112,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.end,
        children: [
          Text(
            label,
            style: const TextStyle(color: AppColors.muted, fontSize: 12),
          ),
          const SizedBox(height: 2),
          Text(
            value,
            style: TextStyle(
              fontSize: 15,
              fontWeight: strong ? FontWeight.w700 : FontWeight.w500,
            ),
          ),
        ],
      ),
    );
  }
}

class _MiniPrice extends StatelessWidget {
  const _MiniPrice({
    required this.label,
    required this.value,
    required this.strong,
  });

  final String label;
  final String value;
  final bool strong;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: AppColors.paper,
        borderRadius: BorderRadius.circular(10),
      ),
      child: Text.rich(
        TextSpan(
          children: [
            TextSpan(
              text: '$label  ',
              style: const TextStyle(color: AppColors.muted, fontSize: 12),
            ),
            TextSpan(
              text: value,
              style: TextStyle(
                fontWeight: strong ? FontWeight.w700 : FontWeight.w500,
              ),
            ),
          ],
        ),
      ),
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
  final TextEditingController _opening = TextEditingController();

  /// One id per dialog: retrying the opening stock never adds it twice.
  final String _openingRequestId = generateUuidV4();

  /// Set once the product exists, so a retry only repeats the opening stock.
  CatalogProduct? _created;
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _sku.dispose();
    _name.dispose();
    _barcode.dispose();
    _sale.dispose();
    _cost.dispose();
    _opening.dispose();
    super.dispose();
  }

  bool get _wantsOpening =>
      widget.product == null &&
      widget.controller.canRecordOpeningStock &&
      _opening.text.trim().isNotEmpty;

  Future<void> _save() async {
    if (_saving) return;
    final created = _created;
    if (created != null) {
      // The product already exists: only the opening stock is retried.
      await _recordOpening(created);
      return;
    }
    final product = widget.product;
    if (product == null && _wantsOpening) {
      final qty = parseQtyE4(_opening.text);
      if (qty == null || qty <= 0) {
        setState(
          () => _error =
              'Opening stock must be a number above zero (up to 4 decimals), or leave it empty.',
        );
        return;
      }
    }
    setState(() {
      _saving = true;
      _error = null;
    });
    if (product != null) {
      final error = await widget.controller.saveProduct(
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
      return;
    }
    final result = await widget.controller.createProduct(
      sku: _sku.text,
      name: _name.text,
      barcode: _barcode.text,
      unitId: _unitId,
      salePrice: _sale.text,
      costPrice: _cost.text,
    );
    if (!mounted) return;
    final made = result.product;
    if (made == null) {
      setState(() {
        _error = result.error;
        _saving = false;
      });
      return;
    }
    if (!_wantsOpening) {
      Navigator.pop(context);
      return;
    }
    _created = made;
    await _recordOpening(made);
  }

  Future<void> _recordOpening(CatalogProduct created) async {
    setState(() {
      _saving = true;
      _error = null;
    });
    final error = await widget.controller.recordOpeningStock(
      productId: created.id,
      quantityText: _opening.text.trim(),
      requestId: _openingRequestId,
    );
    if (!mounted) return;
    if (error == null) {
      Navigator.pop(context);
      return;
    }
    setState(() {
      _error =
          'The product was saved, but its opening stock was not recorded. '
          '$error You can retry, or add it later from the Stock tab.';
      _saving = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    final units = widget.controller.units;
    const gap = SizedBox(height: 14);

    // Two columns when there is room, stacked on a narrow phone.
    Widget pair(Widget a, Widget b) => LayoutBuilder(
      builder: (context, box) {
        if (box.maxWidth < 340) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [a, gap, b],
          );
        }
        return Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(child: a),
            const SizedBox(width: 12),
            Expanded(child: b),
          ],
        );
      },
    );

    return AlertDialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      title: _DialogTitle(
        icon: Icons.inventory_2_outlined,
        text: widget.product == null ? 'New product' : 'Edit product',
      ),
      content: SizedBox(
        width: 460,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const SizedBox(height: 4),
              TextField(
                controller: _name,
                autofocus: true,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(labelText: 'Name'),
              ),
              gap,
              pair(
                TextField(
                  controller: _sku,
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(
                    labelText: 'SKU',
                    helperText: 'Your own product code',
                  ),
                ),
                TextField(
                  controller: _barcode,
                  textInputAction: TextInputAction.next,
                  decoration: const InputDecoration(
                    labelText: 'Barcode',
                    helperText: 'Optional',
                  ),
                ),
              ),
              gap,
              DropdownButtonFormField<String?>(
                initialValue: _unitId,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'Unit'),
                items: [
                  const DropdownMenuItem<String?>(
                    value: null,
                    child: Text('No unit'),
                  ),
                  for (final u in units)
                    DropdownMenuItem<String?>(
                      value: u.id,
                      child: Text(
                        '${u.code} - ${u.name}',
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                ],
                onChanged: (v) => setState(() => _unitId = v),
              ),
              gap,
              pair(
                TextField(
                  controller: _sale,
                  textInputAction: TextInputAction.next,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Sale price',
                    helperText: 'What customers pay',
                  ),
                ),
                TextField(
                  controller: _cost,
                  textInputAction: TextInputAction.done,
                  onSubmitted: (_) => _save(),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Cost price',
                    helperText: 'What you pay',
                  ),
                ),
              ),
              if (widget.product == null &&
                  widget.controller.canRecordOpeningStock) ...[
                gap,
                TextField(
                  controller: _opening,
                  enabled: _created == null,
                  textInputAction: TextInputAction.done,
                  onSubmitted: (_) => _save(),
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: InputDecoration(
                    labelText: 'Opening stock (optional)',
                    helperText:
                        'How many you already have. Saved once as opening stock.',
                    helperMaxLines: 2,
                    suffixText: widget.controller.units
                        .where((u) => u.id == _unitId)
                        .firstOrNull
                        ?.code,
                  ),
                ),
              ],
              _ErrorText(_error),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.pop(context),
          child: Text(_created == null ? 'Cancel' : 'Close'),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: _created != null && !_saving
              ? const Text('Retry opening stock')
              : _saveLabel(_saving),
        ),
      ],
    );
  }
}
