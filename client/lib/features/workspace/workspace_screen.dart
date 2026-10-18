import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';
import '../../core/appearance_controller.dart';
import '../auth/session_controller.dart';
import 'import_mapping_dialog.dart';
import 'spreadsheet_exchange.dart';
import 'tenancy_service.dart';

class WorkspaceScreen extends StatefulWidget {
  const WorkspaceScreen({super.key});

  @override
  State<WorkspaceScreen> createState() => _WorkspaceScreenState();
}

class _WorkspaceScreenState extends State<WorkspaceScreen> {
  late final TenancyService _service;
  List<Organization> _organizations = [];
  List<Company> _allCompanies = [];
  String? _selectedOrganizationId;
  String? _selectedCompanyId;
  String? _error;
  bool _loading = true;
  bool _working = false;
  bool _hasMoreOrganizations = false;
  bool _hasMoreCompanies = false;
  String _search = '';
  String _sortColumn = 'name';
  bool _sortAscending = true;
  int _page = 0;
  final Set<String> _selectedRows = {};
  static const _pageSize = 10;

  static const _modules = <_WorkspaceModule>[
    _WorkspaceModule('Overview', Icons.dashboard_outlined),
  ];

  @override
  void initState() {
    super.initState();
    _service = TenancyService(context.read<ApiClient>());
    _load();
  }

  List<Company> get _companies {
    final organizationId = _selectedOrganizationId;
    if (organizationId == null) return [];
    return _allCompanies
        .where((company) => company.organizationId == organizationId)
        .toList();
  }

  Organization? get _selectedOrganization {
    for (final item in _organizations) {
      if (item.id == _selectedOrganizationId) return item;
    }
    return null;
  }

  Company? get _selectedCompany {
    for (final item in _companies) {
      if (item.id == _selectedCompanyId) return item;
    }
    return null;
  }

  Future<void> _load() async {
    if (mounted) {
      setState(() {
        _loading = true;
        _error = null;
      });
    }
    try {
      final results = await Future.wait([
        _service.listOrganizations(),
        _service.listCompanies(),
      ]);
      if (!mounted) return;
      final organizations = results[0] as List<Organization>;
      final companies = results[1] as List<Company>;
      final previousOrg = _selectedOrganizationId;
      final nextOrg = organizations.any((o) => o.id == previousOrg)
          ? previousOrg
          : (organizations.isEmpty ? null : organizations.first.id);
      final validCompanies = companies
          .where((c) => c.organizationId == nextOrg)
          .toList();
      final previousCompany = _selectedCompanyId;
      final nextCompany = validCompanies.any((c) => c.id == previousCompany)
          ? previousCompany
          : (validCompanies.isEmpty ? null : validCompanies.first.id);
      setState(() {
        _organizations = organizations;
        _allCompanies = companies;
        _selectedOrganizationId = nextOrg;
        _selectedCompanyId = nextCompany;
        _hasMoreOrganizations = organizations.length == 100;
        _hasMoreCompanies = companies.length == 100;
        _selectedRows.clear();
        _page = 0;
        _loading = false;
      });
    } on ApiException catch (e) {
      if (mounted) {
        setState(() {
          _error = e.message;
          _loading = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _error = 'Could not load your workspace. Please try again.';
          _loading = false;
        });
      }
    }
  }

  Future<void> _loadMore() async {
    if (_working || (!_hasMoreOrganizations && !_hasMoreCompanies)) return;
    setState(() => _working = true);
    try {
      final organizationPage = _hasMoreOrganizations
          ? await _service.listOrganizations(offset: _organizations.length)
          : const <Organization>[];
      final companyPage = _hasMoreCompanies
          ? await _service.listCompanies(offset: _allCompanies.length)
          : const <Company>[];
      if (!mounted) return;
      setState(() {
        _organizations.addAll(organizationPage);
        _allCompanies.addAll(companyPage);
        _hasMoreOrganizations = organizationPage.length == 100;
        _hasMoreCompanies = companyPage.length == 100;
        _page = 0;
      });
    } on ApiException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  List<_WorkspaceDataRow> _rowsFor(
    List<Organization> organizations,
    List<Company> companies,
  ) {
    final organizationsById = {
      for (final organization in organizations)
        organization.id: organization.name,
    };
    return [
      for (final organization in organizations)
        _WorkspaceDataRow(
          id: 'organization:${organization.id}',
          type: 'Organization',
          name: organization.name,
          slug: organization.slug,
          parent: '',
          industry: '',
          role: organization.role,
        ),
      for (final company in companies)
        _WorkspaceDataRow(
          id: 'company:${company.id}',
          type: 'Company',
          name: company.name,
          slug: company.slug,
          parent: organizationsById[company.organizationId] ?? '',
          industry: company.industryKey,
          role: company.role,
        ),
    ];
  }

  List<_WorkspaceDataRow> _visibleRows({
    List<Organization>? organizations,
    List<Company>? companies,
    bool applySearch = true,
  }) {
    var rows = _rowsFor(
      organizations ?? _organizations,
      companies ?? _allCompanies,
    );
    final query = applySearch ? _search.trim().toLowerCase() : '';
    if (query.isNotEmpty) {
      rows = rows
          .where((row) => row.values.join(' ').toLowerCase().contains(query))
          .toList();
    }
    rows.sort((left, right) {
      final comparison = left
          .value(_sortColumn)
          .toLowerCase()
          .compareTo(right.value(_sortColumn).toLowerCase());
      return _sortAscending ? comparison : -comparison;
    });
    return rows;
  }

  void _sortRows(String column) {
    setState(() {
      if (_sortColumn == column) {
        _sortAscending = !_sortAscending;
      } else {
        _sortColumn = column;
        _sortAscending = true;
      }
      _page = 0;
    });
  }

  List<_WorkspaceDataRow> get _currentPageRows {
    final rows = _visibleRows();
    final start = _page * _pageSize;
    return rows.skip(start).take(_pageSize).toList();
  }

  Future<void> _copyRows(List<_WorkspaceDataRow> rows) async {
    if (rows.isEmpty) {
      _notice('There are no rows to copy.', error: true);
      return;
    }
    const headers = [
      'Type',
      'Name',
      'Slug',
      'Organization',
      'Industry key',
      'Role',
    ];
    final text = SpreadsheetExchange.encodeTsv([
      headers,
      for (final row in rows) row.values,
    ]);
    await Clipboard.setData(ClipboardData(text: text));
    if (mounted) _notice('${rows.length} row(s) copied as tab-separated text.');
  }

  Future<void> _copySummary() async {
    const headers = ['Summary', 'Value'];
    final text = SpreadsheetExchange.encodeTsv([
      headers,
      ['Organizations', '${_organizations.length}'],
      ['Companies', '${_companies.length}'],
      ['Organization role', _selectedOrganization?.role ?? 'Not set'],
      ['Company role', _selectedCompany?.role ?? 'Not set'],
    ]);
    await Clipboard.setData(ClipboardData(text: text));
    if (mounted) _notice('Workspace summary copied.');
  }

  Future<void> _exportData({
    required bool allRecords,
    required bool csv,
  }) async {
    setState(() => _working = true);
    try {
      final organizations = await _service.listAllOrganizations();
      final companies = await _service.listAllCompanies();
      final rows = _visibleRows(
        organizations: organizations,
        companies: companies,
        applySearch: !allRecords,
      );
      final table = <List<String>>[
        const ['Type', 'Name', 'Slug', 'Organization', 'Industry key', 'Role'],
        for (final row in rows) row.values,
      ];
      final extension = csv ? 'csv' : 'xlsx';
      final location = await getSaveLocation(
        suggestedName:
            'flexicore-workspace${allRecords ? '-all' : ''}.$extension',
        acceptedTypeGroups: [
          XTypeGroup(
            label: csv ? 'CSV' : 'Excel workbook',
            extensions: [extension],
          ),
        ],
      );
      if (location == null) return;

      final file = File(location.path);
      if (csv) {
        await file.writeAsString(
          SpreadsheetExchange.encodeCsv(table),
          flush: true,
        );
      } else {
        await file.writeAsBytes(
          SpreadsheetExchange.encodeXlsx(
            sheetName: 'Workspace records',
            headers: table.first,
            rows: table.skip(1).toList(),
          ),
          flush: true,
        );
      }
      if (mounted) {
        _notice('${rows.length} authorized row(s) exported to $extension.');
      }
    } on ApiException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } on FormatException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } on IOException {
      if (mounted) _notice('The export file could not be saved.', error: true);
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  Future<void> _exportDocx({required bool allRecords}) async {
    setState(() => _working = true);
    try {
      final organizations = await _service.listAllOrganizations();
      final companies = await _service.listAllCompanies();
      final rows = _visibleRows(
        organizations: organizations,
        companies: companies,
        applySearch: !allRecords,
      );
      final table = <List<String>>[
        const ['Type', 'Name', 'Slug', 'Organization', 'Industry key', 'Role'],
        for (final row in rows) row.values,
      ];
      final location = await getSaveLocation(
        suggestedName: 'flexicore-workspace${allRecords ? '-all' : ''}.docx',
        acceptedTypeGroups: const [
          XTypeGroup(label: 'Word document', extensions: ['docx']),
        ],
      );
      if (location == null) return;
      await File(location.path).writeAsBytes(
        SpreadsheetExchange.encodeDocx(
          title: 'FlexiCore workspace directory',
          headers: table.first,
          rows: table.skip(1).toList(),
        ),
        flush: true,
      );
      if (mounted) {
        _notice('${rows.length} authorized row(s) exported to DOCX.');
      }
    } on ApiException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } on IOException {
      if (mounted) {
        _notice('The Word document could not be saved.', error: true);
      }
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  void _onExchangeAction(String action) {
    switch (action) {
      case 'import-organizations':
        _importData(ImportEntity.organizations);
        return;
      case 'import-companies':
        _importData(ImportEntity.companies);
        return;
      case 'template-organizations':
        _saveTemplate(ImportEntity.organizations);
        return;
      case 'template-companies':
        _saveTemplate(ImportEntity.companies);
        return;
      case 'export-filtered-xlsx':
        _exportData(allRecords: false, csv: false);
        return;
      case 'export-all-xlsx':
        _exportData(allRecords: true, csv: false);
        return;
      case 'export-filtered-csv':
        _exportData(allRecords: false, csv: true);
        return;
      case 'export-all-csv':
        _exportData(allRecords: true, csv: true);
        return;
      case 'export-filtered-docx':
        _exportDocx(allRecords: false);
        return;
      case 'export-all-docx':
        _exportDocx(allRecords: true);
        return;
    }
  }

  Future<void> _importData(ImportEntity entity) async {
    if (entity == ImportEntity.companies && _selectedOrganizationId == null) {
      _notice(
        'Select an organization before importing companies.',
        error: true,
      );
      return;
    }
    const group = XTypeGroup(
      label: 'Excel or CSV data',
      extensions: ['xlsx', 'csv'],
    );
    final file = await openFile(acceptedTypeGroups: [group]);
    if (file == null || !mounted) return;
    try {
      if (await file.length() > SpreadsheetExchange.maxFileBytes) {
        throw const FormatException(
          'The selected file exceeds the 20 MB limit.',
        );
      }
      final bytes = await file.readAsBytes();
      final grid = file.name.toLowerCase().endsWith('.csv')
          ? SpreadsheetExchange.decodeCsv(bytes)
          : SpreadsheetExchange.decodeXlsx(bytes);
      if (!mounted) return;
      setState(() => _working = true);
      final existingSlugs = entity == ImportEntity.organizations
          ? (await _service.listAllOrganizations())
                .map((item) => item.slug.toLowerCase())
                .toSet()
          : (await _service.listAllCompanies())
                .where((item) => item.organizationId == _selectedOrganizationId)
                .map((item) => item.slug.toLowerCase())
                .toSet();
      if (!mounted) return;
      final plan = await showDialog<ImportMappingResult>(
        context: context,
        builder: (context) => ImportMappingDialog(
          grid: grid,
          entity: entity,
          existingSlugs: existingSlugs,
        ),
      );
      if (plan == null || !mounted) return;

      final rowErrors = [
        for (final row in plan.invalidRows)
          'Row ${row.sourceRow}: ${row.errors.join(' ')}',
      ];
      var imported = 0;
      for (final row in plan.validRows) {
        try {
          if (entity == ImportEntity.organizations) {
            await _service.createOrganization(
              name: row.values['name']!,
              slug: row.values['slug']!,
            );
          } else {
            await _service.createCompany(
              organizationId: _selectedOrganizationId!,
              name: row.values['name']!,
              slug: row.values['slug']!,
              industryKey: row.values['industryKey']!,
            );
          }
          imported++;
        } on ApiException catch (error) {
          rowErrors.add('Row ${row.sourceRow}: ${error.message}');
        } on Exception {
          rowErrors.add(
            'Row ${row.sourceRow}: The server returned an unexpected response.',
          );
        }
      }
      await _load();
      if (mounted) await _showImportResults(imported, rowErrors);
    } on FormatException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } on ApiException catch (error) {
      if (mounted) _notice(error.message, error: true);
    } on IOException {
      if (mounted) _notice('The import file could not be read.', error: true);
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  Future<void> _showImportResults(int imported, List<String> errors) async {
    await showDialog<void>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Import results'),
        content: SizedBox(
          width: 600,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '$imported record(s) created; ${errors.length} row(s) not imported.',
              ),
              if (errors.isNotEmpty) ...[
                const SizedBox(height: 12),
                SizedBox(
                  height: 240,
                  child: ListView(
                    children: [
                      for (final error in errors)
                        ListTile(dense: true, title: Text(error)),
                    ],
                  ),
                ),
              ],
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  Future<void> _saveTemplate(ImportEntity entity) async {
    final organization = entity == ImportEntity.organizations;
    final headers = organization
        ? SpreadsheetExchange.organizationHeaders
        : SpreadsheetExchange.companyHeaders;
    final bytes = SpreadsheetExchange.encodeXlsx(
      sheetName: organization ? 'Organizations' : 'Companies',
      headers: headers,
      rows: const [],
    );
    final location = await getSaveLocation(
      suggestedName: organization
          ? 'flexicore-organizations-template.xlsx'
          : 'flexicore-companies-template.xlsx',
      acceptedTypeGroups: const [
        XTypeGroup(label: 'Excel workbook', extensions: ['xlsx']),
      ],
    );
    if (location == null) return;
    try {
      await File(location.path).writeAsBytes(bytes, flush: true);
      if (mounted) _notice('Import template saved.');
    } on IOException {
      if (mounted) _notice('The template could not be saved.', error: true);
    }
  }

  String _slugify(String value) {
    final slug = value
        .trim()
        .toLowerCase()
        .replaceAll(RegExp(r'[^a-z0-9]+'), '-')
        .replaceAll(RegExp(r'^-+|-+$'), '');
    return slug.length >= 3 ? slug : '${slug.isEmpty ? 'new' : slug}-app';
  }

  Future<Map<String, String>?> _nameAndSlugDialog({
    required String title,
    required String nameLabel,
    required String slugLabel,
    String initialName = '',
    String? extraLabel,
    String? extraInitial,
  }) async {
    final nameController = TextEditingController(text: initialName);
    final slugController = TextEditingController(text: _slugify(initialName));
    final extraController = TextEditingController(text: extraInitial ?? '');
    final formKey = GlobalKey<FormState>();
    final result = await showDialog<Map<String, String>>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(title),
        content: SizedBox(
          width: 440,
          child: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextFormField(
                    controller: nameController,
                    autofocus: true,
                    textCapitalization: TextCapitalization.words,
                    decoration: InputDecoration(labelText: nameLabel),
                    validator: (value) =>
                        value == null || value.trim().length < 2
                        ? 'Enter at least 2 characters.'
                        : null,
                    onChanged: (value) {
                      if (slugController.text == _slugify(initialName) ||
                          slugController.text.isEmpty) {
                        slugController.text = _slugify(value);
                      }
                    },
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: slugController,
                    decoration: InputDecoration(
                      labelText: slugLabel,
                      helperText: 'Lowercase letters, numbers and hyphens.',
                    ),
                    validator: (value) {
                      if (value == null ||
                          !RegExp(r'^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$')
                              .hasMatch(value.trim())) {
                        return 'Use 3–80 characters; start and end with a letter or number.';
                      }
                      return null;
                    },
                  ),
                  if (extraLabel != null) ...[
                    const SizedBox(height: 12),
                    TextFormField(
                      controller: extraController,
                      decoration: InputDecoration(
                        labelText: extraLabel,
                        helperText: 'Example: general, retail, restaurant.',
                      ),
                      validator: (value) =>
                          value == null ||
                              !RegExp(r'^[a-z][a-z0-9_]{1,63}$')
                                  .hasMatch(value.trim())
                          ? 'Use 2–64 lowercase letters, numbers or underscores.'
                          : null,
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              if (!(formKey.currentState?.validate() ?? false)) return;
              Navigator.pop(dialogContext, {
                'name': nameController.text.trim(),
                'slug': slugController.text.trim(),
                if (extraLabel != null) 'extra': extraController.text.trim(),
              });
            },
            child: const Text('Continue'),
          ),
        ],
      ),
    );
    nameController.dispose();
    slugController.dispose();
    extraController.dispose();
    return result;
  }

  Future<void> _createOrganization() async {
    final values = await _nameAndSlugDialog(
      title: 'Create organization',
      nameLabel: 'Organization name',
      slugLabel: 'Organization slug',
    );
    if (values == null || !mounted) return;
    setState(() => _working = true);
    try {
      final created = await _service.createOrganization(
        name: values['name']!,
        slug: values['slug']!,
      );
      await _load();
      if (mounted) {
        setState(() => _selectedOrganizationId = created.id);
        _notice('Organization created successfully.');
      }
    } on ApiException catch (e) {
      if (mounted) _notice(e.message, error: true);
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  Future<void> _createCompany() async {
    final organization = _selectedOrganization;
    if (organization == null) {
      _notice('Create or select an organization first.', error: true);
      return;
    }
    final values = await _nameAndSlugDialog(
      title: 'Create company',
      nameLabel: 'Company name',
      slugLabel: 'Company slug',
      extraLabel: 'Industry key',
      extraInitial: 'general',
    );
    if (values == null || !mounted) return;
    setState(() => _working = true);
    try {
      final created = await _service.createCompany(
        organizationId: organization.id,
        name: values['name']!,
        slug: values['slug']!,
        industryKey: values['extra']!,
      );
      await _load();
      if (mounted) {
        setState(() {
          _selectedOrganizationId = created.organizationId;
          _selectedCompanyId = created.id;
        });
        _notice('Company created successfully.');
      }
    } on ApiException catch (e) {
      if (mounted) _notice(e.message, error: true);
    } finally {
      if (mounted) setState(() => _working = false);
    }
  }

  void _notice(String message, {bool error = false}) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        behavior: SnackBarBehavior.floating,
        backgroundColor: error ? Theme.of(context).colorScheme.error : null,
      ),
    );
  }

  Future<void> _logout() async {
    try {
      await context.read<SessionController>().logout();
    } on Exception {
      if (mounted) {
        _notice('Could not sign out. Please try again.', error: true);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final session = context.watch<SessionController>();
    final wide = MediaQuery.sizeOf(context).width >= 1050;

    return Scaffold(
      drawer: wide ? null : Drawer(child: SafeArea(child: _navigation())),
      appBar: AppBar(
        title: const Text('FlexiCore'),
        actions: [
          IconButton(
            tooltip: 'Refresh workspace',
            onPressed: _loading || _working ? null : _load,
            icon: const Icon(Icons.refresh),
          ),
          PopupMenuButton<String>(
            tooltip: 'Import and export data',
            icon: const Icon(Icons.import_export),
            onSelected: _onExchangeAction,
            itemBuilder: (context) => const [
              PopupMenuItem(
                value: 'import-organizations',
                child: Text('Import organizations...'),
              ),
              PopupMenuItem(
                value: 'import-companies',
                child: Text('Import companies...'),
              ),
              PopupMenuDivider(),
              PopupMenuItem(
                value: 'template-organizations',
                child: Text('Save organization template...'),
              ),
              PopupMenuItem(
                value: 'template-companies',
                child: Text('Save company template...'),
              ),
              PopupMenuDivider(),
              PopupMenuItem(
                value: 'export-filtered-xlsx',
                child: Text('Export filtered records (.xlsx)'),
              ),
              PopupMenuItem(
                value: 'export-all-xlsx',
                child: Text('Export all authorized records (.xlsx)'),
              ),
              PopupMenuItem(
                value: 'export-filtered-csv',
                child: Text('Export filtered records (.csv)'),
              ),
              PopupMenuItem(
                value: 'export-all-csv',
                child: Text('Export all authorized records (.csv)'),
              ),
              PopupMenuItem(
                value: 'export-filtered-docx',
                child: Text('Export filtered workspace report (.docx)'),
              ),
              PopupMenuItem(
                value: 'export-all-docx',
                child: Text('Export all authorized workspace data (.docx)'),
              ),
            ],
          ),
          IconButton(
            tooltip: context.watch<AppearanceController>().isDark
                ? 'Switch to light theme'
                : 'Switch to dark theme',
            onPressed: () => context.read<AppearanceController>().toggle(),
            icon: Icon(
              context.watch<AppearanceController>().isDark
                  ? Icons.light_mode_outlined
                  : Icons.dark_mode_outlined,
            ),
          ),
          PopupMenuButton<String>(
            tooltip: 'Account',
            onSelected: (value) {
              if (value == 'logout') _logout();
              if (value == 'theme') {
                context.read<AppearanceController>().toggle();
              }
            },
            itemBuilder: (context) => [
              PopupMenuItem(
                enabled: false,
                child: SizedBox(
                  width: 220,
                  child: Text(session.user?.displayName ?? 'Signed-in account'),
                ),
              ),
              const PopupMenuDivider(),
              const PopupMenuItem(
                value: 'theme',
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(Icons.contrast),
                  title: Text('Toggle appearance'),
                ),
              ),
              const PopupMenuItem(
                value: 'logout',
                child: ListTile(
                  contentPadding: EdgeInsets.zero,
                  leading: Icon(Icons.logout),
                  title: Text('Sign out'),
                ),
              ),
            ],
          ),
          const SizedBox(width: 8),
        ],
      ),
      body: Row(
        children: [
          if (wide)
            SizedBox(
              width: 250,
              child: DecoratedBox(
                decoration: BoxDecoration(
                  border: Border(right: BorderSide(color: theme.dividerColor)),
                ),
                child: SafeArea(child: _navigation()),
              ),
            ),
          Expanded(
            child: _loading
                ? const Center(child: CircularProgressIndicator())
                : _error != null
                ? _errorPanel()
                : _content(wide),
          ),
        ],
      ),
    );
  }

  Widget _navigation() {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(20, 20, 16, 12),
          child: Text(
            'WORKSPACE',
            style: theme.textTheme.labelMedium?.copyWith(
              color: theme.colorScheme.primary,
              letterSpacing: 1.3,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        Expanded(
          child: ListView.builder(
            padding: const EdgeInsets.symmetric(horizontal: 10),
            itemCount: _modules.length,
            itemBuilder: (context, index) {
              final module = _modules[index];
              return Padding(
                padding: const EdgeInsets.only(bottom: 3),
                child: ListTile(
                  selected: true,
                  leading: Icon(module.icon),
                  title: Text(module.label),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  onTap: () {
                    if (MediaQuery.sizeOf(context).width < 1050) {
                      Navigator.of(context).pop();
                    }
                  },
                ),
              );
            },
          ),
        ),
        const Divider(),
        ListTile(
          leading: const Icon(Icons.help_outline),
          title: const Text('Workspace help'),
          subtitle: const Text('Setup and available features'),
          onTap: () => _notice(
            'Start by creating an organization and then a company. '
            'Business transaction modules require their API endpoints.',
          ),
        ),
        const SizedBox(height: 8),
      ],
    );
  }

  Widget _errorPanel() {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.cloud_off_outlined, size: 48),
              const SizedBox(height: 16),
              Text(
                'Workspace could not be loaded',
                style: Theme.of(context).textTheme.titleLarge,
              ),
              const SizedBox(height: 8),
              Text(_error!, textAlign: TextAlign.center),
              const SizedBox(height: 20),
              FilledButton.icon(
                onPressed: _load,
                icon: const Icon(Icons.refresh),
                label: const Text('Try again'),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _content(bool wide) {
    final theme = Theme.of(context);
    final organization = _selectedOrganization;
    final company = _selectedCompany;

    return SelectionArea(
      child: SingleChildScrollView(
        padding: EdgeInsets.all(wide ? 28 : 16),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1440),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  runSpacing: 12,
                  spacing: 16,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Business setup',
                          style: theme.textTheme.headlineMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Manage your organizations and companies.',
                          style: theme.textTheme.bodyLarge?.copyWith(
                            color: theme.colorScheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        OutlinedButton.icon(
                          onPressed: _working ? null : _createOrganization,
                          icon: const Icon(Icons.add_business_outlined),
                          label: const Text('New organization'),
                        ),
                        FilledButton.icon(
                          onPressed: _working ? null : _createCompany,
                          icon: const Icon(Icons.add),
                          label: const Text('New company'),
                        ),
                      ],
                    ),
                  ],
                ),
                const SizedBox(height: 28),
                _workspaceSelectors(),
                const SizedBox(height: 24),
                Text('Workspace summary', style: theme.textTheme.titleLarge),
                const SizedBox(height: 12),
                LayoutBuilder(
                  builder: (context, constraints) {
                    final count = constraints.maxWidth >= 900
                        ? 4
                        : constraints.maxWidth >= 560
                        ? 2
                        : 1;
                    final cardWidth =
                        (constraints.maxWidth - (count - 1) * 12) / count;
                    return Wrap(
                      spacing: 12,
                      runSpacing: 12,
                      children: [
                        SizedBox(
                          width: cardWidth,
                          child: _summaryCard(
                            'Organizations',
                            '${_organizations.length}',
                            Icons.apartment_outlined,
                            'Available to your account',
                          ),
                        ),
                        SizedBox(
                          width: cardWidth,
                          child: _summaryCard(
                            'Companies',
                            '${_companies.length}',
                            Icons.business_outlined,
                            'In the selected organization',
                          ),
                        ),
                        SizedBox(
                          width: cardWidth,
                          child: _summaryCard(
                            'Organization role',
                            organization?.role ?? 'Not set',
                            Icons.admin_panel_settings_outlined,
                            'Access granted by membership',
                          ),
                        ),
                        SizedBox(
                          width: cardWidth,
                          child: _summaryCard(
                            'Company role',
                            company?.role ?? 'Not set',
                            Icons.verified_user_outlined,
                            'Access granted by membership',
                          ),
                        ),
                      ],
                    );
                  },
                ),
                const SizedBox(height: 8),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    onPressed: _copySummary,
                    icon: const Icon(Icons.copy_outlined),
                    label: const Text('Copy summary'),
                  ),
                ),
                const SizedBox(height: 20),
                Text(
                  'Organizations and companies',
                  style: theme.textTheme.titleLarge,
                ),
                const SizedBox(height: 12),
                TextField(
                  decoration: const InputDecoration(
                    labelText: 'Search authorized records',
                    prefixIcon: Icon(Icons.search),
                    border: OutlineInputBorder(),
                  ),
                  onChanged: (value) => setState(() {
                    _search = value;
                    _page = 0;
                  }),
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  children: [
                    OutlinedButton.icon(
                      onPressed: _selectedRows.isEmpty
                          ? null
                          : () => _copyRows(
                              _visibleRows()
                                  .where(
                                    (row) => _selectedRows.contains(row.id),
                                  )
                                  .toList(),
                            ),
                      icon: const Icon(Icons.copy),
                      label: Text('Copy selected (${_selectedRows.length})'),
                    ),
                    TextButton.icon(
                      onPressed: () => _copyRows(_visibleRows()),
                      icon: const Icon(Icons.table_view_outlined),
                      label: Text('Copy filtered (${_visibleRows().length})'),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                if (_visibleRows().isEmpty)
                  Padding(
                    padding: const EdgeInsets.all(24),
                    child: Center(
                      child: Text(
                        _organizations.isEmpty && _allCompanies.isEmpty
                            ? 'No authorized organizations or companies yet. Create one to begin.'
                            : 'No authorized records match this search.',
                        textAlign: TextAlign.center,
                      ),
                    ),
                  )
                else
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      SingleChildScrollView(
                        scrollDirection: Axis.horizontal,
                        child: DataTable(
                          sortColumnIndex: const {
                            'type': 1,
                            'name': 2,
                            'slug': 3,
                            'parent': 4,
                            'industry': 5,
                            'role': 6,
                          }[_sortColumn],
                          sortAscending: _sortAscending,
                          onSelectAll: (selected) => setState(() {
                            if (selected == true) {
                              _selectedRows.addAll(
                                _currentPageRows.map((row) => row.id),
                              );
                            } else {
                              _selectedRows.removeAll(
                                _currentPageRows.map((row) => row.id),
                              );
                            }
                          }),
                          columns: [
                            DataColumn(
                              label: const Text('Type'),
                              onSort: (_, _) => _sortRows('type'),
                            ),
                            DataColumn(
                              label: const Text('Name'),
                              onSort: (_, _) => _sortRows('name'),
                            ),
                            DataColumn(
                              label: const Text('Slug'),
                              onSort: (_, _) => _sortRows('slug'),
                            ),
                            DataColumn(
                              label: const Text('Organization'),
                              onSort: (_, _) => _sortRows('parent'),
                            ),
                            DataColumn(
                              label: const Text('Industry key'),
                              onSort: (_, _) => _sortRows('industry'),
                            ),
                            DataColumn(
                              label: const Text('Role'),
                              onSort: (_, _) => _sortRows('role'),
                            ),
                            const DataColumn(label: Text('')),
                          ],
                          rows: [
                            for (final row in _currentPageRows)
                              DataRow(
                                selected: _selectedRows.contains(row.id),
                                onSelectChanged: (selected) => setState(() {
                                  if (selected == true) {
                                    _selectedRows.add(row.id);
                                  } else {
                                    _selectedRows.remove(row.id);
                                  }
                                }),
                                cells: [
                                  DataCell(Text(row.type)),
                                  DataCell(Text(row.name)),
                                  DataCell(SelectableText(row.slug)),
                                  DataCell(Text(row.parent)),
                                  DataCell(Text(row.industry)),
                                  DataCell(Text(row.role)),
                                  DataCell(
                                    IconButton(
                                      tooltip: 'Copy row',
                                      onPressed: () => _copyRows([row]),
                                      icon: const Icon(Icons.copy_outlined),
                                    ),
                                  ),
                                ],
                              ),
                          ],
                        ),
                      ),
                      Row(
                        mainAxisAlignment: MainAxisAlignment.end,
                        children: [
                          Text(
                            'Page ${_page + 1} of ${(_visibleRows().length / _pageSize).ceil().clamp(1, 100000)} · '
                            '${_visibleRows().length} loaded record(s)',
                          ),
                          IconButton(
                            tooltip: 'Previous page',
                            onPressed: _page == 0
                                ? null
                                : () => setState(() => _page--),
                            icon: const Icon(Icons.chevron_left),
                          ),
                          IconButton(
                            tooltip: 'Next page',
                            onPressed:
                                (_page + 1) * _pageSize >= _visibleRows().length
                                ? null
                                : () => setState(() => _page++),
                            icon: const Icon(Icons.chevron_right),
                          ),
                          if (_hasMoreOrganizations || _hasMoreCompanies)
                            TextButton(
                              onPressed: _working ? null : _loadMore,
                              child: Text(
                                _working ? 'Loading...' : 'Load more',
                              ),
                            ),
                        ],
                      ),
                    ],
                  ),
                const SizedBox(height: 28),
                Text('Get started', style: theme.textTheme.titleLarge),
                const SizedBox(height: 12),
                _gettingStarted(),
                const SizedBox(height: 36),
                Text(
                  'FlexiCore • Business workspace',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _workspaceSelectors() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: LayoutBuilder(
          builder: (context, constraints) {
            final compact = constraints.maxWidth < 650;
            final organizationField = DropdownButtonFormField<String>(
              key: ValueKey('org-$_selectedOrganizationId'),
              initialValue: _selectedOrganizationId,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Organization',
                prefixIcon: Icon(Icons.apartment_outlined),
                border: OutlineInputBorder(),
              ),
              hint: const Text('Select an organization'),
              items: _organizations
                  .map(
                    (o) => DropdownMenuItem(
                      value: o.id,
                      child: Text(o.name, overflow: TextOverflow.ellipsis),
                    ),
                  )
                  .toList(),
              onChanged: (value) {
                setState(() {
                  _selectedOrganizationId = value;
                  final matches = _companies;
                  _selectedCompanyId = matches.isEmpty
                      ? null
                      : matches.first.id;
                });
              },
            );
            final companyField = DropdownButtonFormField<String>(
              key: ValueKey('co-$_selectedOrganizationId-$_selectedCompanyId'),
              initialValue: _companies.any((c) => c.id == _selectedCompanyId)
                  ? _selectedCompanyId
                  : null,
              isExpanded: true,
              decoration: const InputDecoration(
                labelText: 'Active company',
                prefixIcon: Icon(Icons.business_outlined),
                border: OutlineInputBorder(),
              ),
              hint: const Text('Select a company'),
              items: _companies
                  .map(
                    (c) => DropdownMenuItem(
                      value: c.id,
                      child: Text(c.name, overflow: TextOverflow.ellipsis),
                    ),
                  )
                  .toList(),
              onChanged: (value) => setState(() => _selectedCompanyId = value),
            );
            if (compact) {
              return Column(
                children: [
                  organizationField,
                  const SizedBox(height: 12),
                  companyField,
                ],
              );
            }
            return Row(
              children: [
                Expanded(child: organizationField),
                const SizedBox(width: 16),
                Expanded(child: companyField),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _summaryCard(
    String title,
    String value,
    IconData icon,
    String description,
  ) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Expanded(child: Text(title, style: theme.textTheme.titleSmall)),
              ],
            ),
            const SizedBox(height: 18),
            Text(
              value,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: theme.textTheme.headlineSmall?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              description,
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _gettingStarted() {
    final theme = Theme.of(context);
    final hasOrganization = _organizations.isNotEmpty;
    final hasCompany = _companies.isNotEmpty;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          children: [
            _step(
              theme,
              '1',
              'Create an organization',
              'Your organization is the top-level workspace for your business.',
              hasOrganization,
              hasOrganization ? 'Available' : 'Create',
              hasOrganization ? null : _createOrganization,
            ),
            const Divider(height: 28),
            _step(
              theme,
              '2',
              'Create a company',
              'Companies belong to an organization and can represent separate businesses.',
              hasCompany,
              hasCompany ? 'Available' : 'Create',
              hasCompany ? null : _createCompany,
            ),
            const Divider(height: 28),
            _step(
              theme,
              '3',
              'Set up business operations',
              'Inventory, sales, purchasing and accounting require their own API-backed workflows.',
              false,
              'Planned',
              null,
            ),
          ],
        ),
      ),
    );
  }

  Widget _step(
    ThemeData theme,
    String number,
    String title,
    String description,
    bool complete,
    String actionLabel,
    VoidCallback? action,
  ) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        CircleAvatar(
          radius: 17,
          backgroundColor: complete
              ? theme.colorScheme.primary
              : theme.colorScheme.surfaceContainerHighest,
          foregroundColor: complete
              ? theme.colorScheme.onPrimary
              : theme.colorScheme.onSurface,
          child: complete ? const Icon(Icons.check, size: 18) : Text(number),
        ),
        const SizedBox(width: 14),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(description, style: theme.textTheme.bodyMedium),
            ],
          ),
        ),
        const SizedBox(width: 8),
        if (action != null)
          TextButton(
            onPressed: _working ? null : action,
            child: Text(actionLabel),
          )
        else
          Padding(
            padding: const EdgeInsets.only(top: 8),
            child: Text(
              actionLabel,
              style: theme.textTheme.labelMedium?.copyWith(
                color: complete
                    ? theme.colorScheme.primary
                    : theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
      ],
    );
  }
}

class _WorkspaceModule {
  const _WorkspaceModule(this.label, this.icon);

  final String label;
  final IconData icon;
}

class _WorkspaceDataRow {
  const _WorkspaceDataRow({
    required this.id,
    required this.type,
    required this.name,
    required this.slug,
    required this.parent,
    required this.industry,
    required this.role,
  });

  final String id;
  final String type;
  final String name;
  final String slug;
  final String parent;
  final String industry;
  final String role;

  List<String> get values => [type, name, slug, parent, industry, role];

  String value(String column) => switch (column) {
    'type' => type,
    'name' => name,
    'slug' => slug,
    'parent' => parent,
    'industry' => industry,
    'role' => role,
    _ => name,
  };
}
