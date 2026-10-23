import 'package:flutter/material.dart';
import '../../../core/read_only.dart';

import '../data/local_inventory_repository.dart';
import '../domain/inventory_domain.dart';

/// Asks for a warehouse code and name and saves it. Pops the new [Warehouse].
/// Typed values stay in the form if saving fails.
class AddWarehouseDialog extends StatefulWidget {
  const AddWarehouseDialog({super.key, required this.repo});

  final LocalInventoryRepository repo;

  @override
  State<AddWarehouseDialog> createState() => _AddWarehouseDialogState();
}

class _AddWarehouseDialogState extends State<AddWarehouseDialog> {
  final _code = TextEditingController();
  final _name = TextEditingController();
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
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      final w = await widget.repo.createWarehouse(
        code: _code.text,
        name: _name.text,
      );
      if (!mounted) return;
      Navigator.of(context).pop(w);
    } on InventoryException catch (e) {
      if (!mounted) return;
      setState(() {
        _error = e.message;
        _saving = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = friendlyError(e, 'Could not save');
        _saving = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return AlertDialog(
      title: const Text('Add warehouse'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            TextField(
              controller: _code,
              autofocus: true,
              maxLength: 20,
              textCapitalization: TextCapitalization.characters,
              decoration: const InputDecoration(
                labelText: 'Code (for example SHOP2)',
                border: OutlineInputBorder(),
              ),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _name,
              maxLength: 80,
              decoration: const InputDecoration(
                labelText: 'Name',
                border: OutlineInputBorder(),
              ),
            ),
            if (_error != null) ...[
              const SizedBox(height: 8),
              Text(_error!, style: TextStyle(color: scheme.error)),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _saving ? null : () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: _saving ? null : _save,
          child: Text(_saving ? 'Saving...' : 'Add'),
        ),
      ],
    );
  }
}