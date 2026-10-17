import 'package:flutter/material.dart';

import 'spreadsheet_exchange.dart';

class ImportMappingResult {
  const ImportMappingResult({
    required this.validRows,
    required this.invalidRows,
  });

  final List<ImportRowPreview> validRows;
  final List<ImportRowPreview> invalidRows;
}

class ImportMappingDialog extends StatefulWidget {
  const ImportMappingDialog({
    required this.grid,
    required this.entity,
    required this.existingSlugs,
    super.key,
  });

  final SpreadsheetGrid grid;
  final ImportEntity entity;
  final Set<String> existingSlugs;

  @override
  State<ImportMappingDialog> createState() => _ImportMappingDialogState();
}

class _ImportMappingDialogState extends State<ImportMappingDialog> {
  late final Map<String, int?> _mapping;
  late List<ImportRowPreview> _preview;
  String? _mappingError;

  List<String> get _fields => widget.entity == ImportEntity.organizations
      ? const ['name', 'slug']
      : const ['name', 'slug', 'industryKey'];

  String _fieldLabel(String field) => switch (field) {
    'name' => 'Name',
    'slug' => 'Slug',
    _ => 'Industry key',
  };

  @override
  void initState() {
    super.initState();
    _mapping = {for (final field in _fields) field: _findHeader(field)};
    _updatePreview();
  }

  int? _findHeader(String field) {
    final aliases = switch (field) {
      'name' => {'name', 'organizationname', 'companyname'},
      'slug' => {'slug', 'code'},
      _ => {'industry', 'industrykey'},
    };
    for (var index = 0; index < widget.grid.headers.length; index++) {
      final normalized = widget.grid.headers[index].toLowerCase().replaceAll(
        RegExp(r'[^a-z0-9]'),
        '',
      );
      if (aliases.contains(normalized)) return index;
    }
    return null;
  }

  void _updatePreview() {
    try {
      _preview = SpreadsheetExchange.previewImport(
        grid: widget.grid,
        entity: widget.entity,
        columnMap: _mapping,
        existingSlugs: widget.existingSlugs,
      );
      _mappingError = null;
    } on FormatException catch (error) {
      _preview = [];
      _mappingError = error.message;
    }
  }

  @override
  Widget build(BuildContext context) {
    final validRows = _preview.where((row) => row.isValid).toList();
    final invalidRows = _preview.where((row) => !row.isValid).toList();
    final entityLabel = widget.entity == ImportEntity.organizations
        ? 'organizations'
        : 'companies';

    return AlertDialog(
      title: Text('Preview $entityLabel import'),
      content: SizedBox(
        width: 820,
        height: MediaQuery.sizeOf(context).height * 0.75,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'Import is create-only. Existing records are never overwritten. '
                'Company rows will be created in the currently selected organization.',
              ),
              const SizedBox(height: 16),
              for (final field in _fields)
                Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: DropdownButtonFormField<int>(
                    initialValue: _mapping[field],
                    decoration: InputDecoration(labelText: _fieldLabel(field)),
                    items: [
                      for (
                        var index = 0;
                        index < widget.grid.headers.length;
                        index++
                      )
                        DropdownMenuItem(
                          value: index,
                          child: Text(widget.grid.headers[index]),
                        ),
                    ],
                    onChanged: (value) => setState(() {
                      _mapping[field] = value;
                      _updatePreview();
                    }),
                  ),
                ),
              if (_mappingError != null)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 8),
                  child: Text(
                    _mappingError!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                )
              else ...[
                Text(
                  '${_preview.length} rows · ${validRows.length} ready · '
                  '${invalidRows.length} with errors',
                ),
                if (validRows.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(
                    'Sample of ${validRows.length < 5 ? validRows.length : 5} '
                    'valid rows',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  SizedBox(
                    height: 170,
                    child: ListView.builder(
                      itemCount: validRows.length < 5 ? validRows.length : 5,
                      itemBuilder: (context, index) {
                        final row = validRows[index];
                        final details = [
                          'Slug: ${row.values['slug']}',
                          if (row.values.containsKey('industryKey'))
                            'Industry: ${row.values['industryKey']}',
                        ].join(' · ');
                        return ListTile(
                          dense: true,
                          leading: Text('Row ${row.sourceRow}'),
                          title: Text(row.values['name'] ?? ''),
                          subtitle: Text(details),
                        );
                      },
                    ),
                  ),
                ],
                if (invalidRows.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text(
                    'Rows with validation errors',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                  SizedBox(
                    height: 190,
                    child: ListView.builder(
                      itemCount: invalidRows.length,
                      itemBuilder: (context, index) {
                        final row = invalidRows[index];
                        return ListTile(
                          dense: true,
                          leading: Text('Row ${row.sourceRow}'),
                          title: Text(row.values['name'] ?? ''),
                          subtitle: Text(row.errors.join(' ')),
                          textColor: Theme.of(context).colorScheme.error,
                        );
                      },
                    ),
                  ),
                ] else
                  const Text('No row validation errors found.'),
              ],
            ],
          ),
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: validRows.isEmpty
              ? null
              : () => Navigator.pop(
                  context,
                  ImportMappingResult(
                    validRows: validRows,
                    invalidRows: invalidRows,
                  ),
                ),
          child: Text(
            validRows.isEmpty
                ? 'No valid rows'
                : 'Create ${validRows.length} valid rows',
          ),
        ),
      ],
    );
  }
}
