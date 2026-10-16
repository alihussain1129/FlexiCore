import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';
import '../../core/appearance_controller.dart';
import '../auth/session_controller.dart';
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
  int _selectedModule = 0;

  static const _modules = <_WorkspaceModule>[
    _WorkspaceModule('Overview', Icons.dashboard_outlined),
    _WorkspaceModule('Sales', Icons.point_of_sale_outlined),
    _WorkspaceModule('Purchases', Icons.shopping_cart_outlined),
    _WorkspaceModule('Inventory', Icons.inventory_2_outlined),
    _WorkspaceModule('Products', Icons.category_outlined),
    _WorkspaceModule('Customers', Icons.people_outline),
    _WorkspaceModule('Suppliers', Icons.local_shipping_outlined),
    _WorkspaceModule('Accounting', Icons.account_balance_outlined),
    _WorkspaceModule('Ledgers', Icons.menu_book_outlined),
    _WorkspaceModule('Expenses', Icons.receipt_long_outlined),
    _WorkspaceModule('Employees', Icons.badge_outlined),
    _WorkspaceModule('Reports', Icons.bar_chart_outlined),
    _WorkspaceModule('Settings', Icons.settings_outlined),
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
                  child: Text(session.user?.email ?? 'Signed-in account'),
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
                  selected: index == _selectedModule,
                  leading: Icon(module.icon),
                  title: Text(module.label),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(12),
                  ),
                  onTap: () {
                    setState(() => _selectedModule = index);
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
    final module = _modules[_selectedModule];

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
                          _selectedModule == 0
                              ? 'Business overview'
                              : module.label,
                          style: theme.textTheme.headlineMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          _selectedModule == 0
                              ? 'Your business workspace at a glance.'
                              : 'Manage ${module.label.toLowerCase()} in your workspace.',
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
                if (_selectedModule == 0) ...[
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
                  const SizedBox(height: 28),
                  Text('Get started', style: theme.textTheme.titleLarge),
                  const SizedBox(height: 12),
                  _gettingStarted(),
                  const SizedBox(height: 28),
                  Text('Business modules', style: theme.textTheme.titleLarge),
                  const SizedBox(height: 12),
                  _moduleGrid(),
                ] else
                  _moduleUnavailable(module),
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

  Widget _moduleGrid() {
    final modules = _modules.skip(1).toList();
    return LayoutBuilder(
      builder: (context, constraints) {
        final count = constraints.maxWidth >= 1000
            ? 4
            : constraints.maxWidth >= 650
            ? 3
            : constraints.maxWidth >= 400
            ? 2
            : 1;
        final width = (constraints.maxWidth - (count - 1) * 12) / count;
        return Wrap(
          spacing: 12,
          runSpacing: 12,
          children: modules.map((module) {
            return SizedBox(
              width: width,
              child: Card(
                clipBehavior: Clip.antiAlias,
                child: InkWell(
                  onTap: () => setState(
                    () => _selectedModule = _modules.indexOf(module),
                  ),
                  child: Padding(
                    padding: const EdgeInsets.all(16),
                    child: Row(
                      children: [
                        Icon(
                          module.icon,
                          color: Theme.of(context).colorScheme.primary,
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Text(
                            module.label,
                            style: Theme.of(context).textTheme.titleSmall,
                          ),
                        ),
                        const Icon(Icons.arrow_forward_ios, size: 14),
                      ],
                    ),
                  ),
                ),
              ),
            );
          }).toList(),
        );
      },
    );
  }

  Widget _moduleUnavailable(_WorkspaceModule module) {
    final theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(28),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 600),
            child: Column(
              children: [
                Icon(module.icon, size: 48, color: theme.colorScheme.primary),
                const SizedBox(height: 16),
                Text(
                  '${module.label} workspace',
                  style: theme.textTheme.headlineSmall,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 12),
                Text(
                  'This section is reserved for the ${module.label.toLowerCase()} '
                  'workflow. The backend currently exposes authentication and '
                  'organization/company management, but not the API operations '
                  'required to save and retrieve ${module.label.toLowerCase()} data.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                const Text(
                  'No sample transactions or invented totals are being shown.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 20),
                OutlinedButton.icon(
                  onPressed: () => setState(() => _selectedModule = 0),
                  icon: const Icon(Icons.arrow_back),
                  label: const Text('Return to overview'),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _WorkspaceModule {
  const _WorkspaceModule(this.label, this.icon);

  final String label;
  final IconData icon;
}
