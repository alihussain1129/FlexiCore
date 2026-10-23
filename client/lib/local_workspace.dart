import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:provider/provider.dart';

import 'core/app_theme.dart';
import 'core/widgets/app_widgets.dart' show MinSizeGuard;
import 'data/local/app_database.dart';
import 'data/local/database_backup.dart';
import 'data/local/database_opener.dart';
import 'features/catalog/data/local_catalog_repository.dart';
import 'features/catalog/presentation/catalog_controller.dart';
import 'features/catalog/presentation/catalog_screen.dart';
import 'features/inventory/data/local_inventory_repository.dart';
import 'features/inventory/data/opening_stock_recorder.dart';
import 'features/inventory/presentation/stock_in_screen.dart';
import 'features/license/license_controller.dart';
import 'features/local_data/local_data_menu.dart';
import 'features/overview/overview_screen.dart';
import 'features/profile/data/local_profile_repository.dart';
import 'features/profile/presentation/profile_controller.dart';
import 'features/profile/presentation/profile_setup_screen.dart';
import 'features/pos/presentation/pos_screen.dart';
import 'features/pos/presentation/sales_history_screen.dart';
import 'features/shell/app_shell.dart';

/// Local entry point: runs entirely on the local database and never creates
/// an API client, so it works with the network off.
const String kLocalCompanyId = 'local-company';

class _Boot {
  _Boot(this.db, this.profile, this.catalog);
  final AppDatabase db;
  final ProfileController profile;
  final CatalogController catalog;

  /// Bumped after any stock change so open screens (the POS) reload.
  final ValueNotifier<int> stockRevision = ValueNotifier<int>(0);
}

class LocalWorkspaceApp extends StatefulWidget {
  const LocalWorkspaceApp({super.key});

  @override
  State<LocalWorkspaceApp> createState() => _LocalAppState();
}

class _LocalAppState extends State<LocalWorkspaceApp> {
  AppDatabase? _db;
  late Future<_Boot> _boot = _start();

  Future<_Boot> _start() async {
    // Read before the first await. Fails loudly if there is no Provider, so the
    // app can never run silently without the write guard.
    final license = context.read<LicenseController>();
    final db = await openAppDatabase(isReadOnly: () => license.isReadOnly);
    _db = db;
    final profile = ProfileController(
      LocalProfileRepository(db, companyId: kLocalCompanyId),
    );
    await profile.load();
    late final _Boot boot;
    final catalog = CatalogController(
      LocalCatalogRepository(db, companyId: kLocalCompanyId),
      openingStock: localOpeningStockRecorder(
        LocalInventoryRepository(db, companyId: kLocalCompanyId),
        onRecorded: () => boot.stockRevision.value++,
      ),
    );
    await catalog.load();
    boot = _Boot(db, profile, catalog);
    return boot;
  }

  /// Closes the database, restores [backup] over it, then reopens the app on
  /// whatever database is in place afterwards (the restored one, or the
  /// previous one if the restore was rolled back).
  Future<({bool ok, String message})> _restore(File backup) async {
    try {
      final dir = await getApplicationSupportDirectory();
      await _db?.close();
      _db = null;
      try {
        final r = await restoreBackup(backup: backup, directory: dir);
        final kept = r.safetyCopy == null
            ? ''
            : ' Your previous data was kept as '
                  '${p.basename(r.safetyCopy!.path)} in the app data folder.';
        return (ok: true, message: 'Backup restored.$kept');
      } on BackupException catch (e) {
        return (ok: false, message: e.message);
      }
    } catch (e) {
      return (ok: false, message: 'Restore failed: $e');
    } finally {
      if (mounted) {
        setState(() {
          _boot = _start();
        });
      }
    }
  }

  @override
  void dispose() {
    _db?.close();
    super.dispose();
  }

  Widget _workspace(_Boot boot) {
    Widget menu() =>
        LocalDataMenu(db: boot.db, profile: boot.profile, onRestore: _restore);
    return AppShell(
      destinations: [
        ShellDestination(
          label: 'Overview',
          icon: Icons.dashboard_outlined,
          selectedIcon: Icons.dashboard,
          builder: (context) => OverviewScreen(
            profile: boot.profile,
            catalog: boot.catalog,
            stockIndex: 2,
            actions: [menu()],
          ),
        ),
        ShellDestination(
          label: 'Products',
          icon: Icons.sell_outlined,
          selectedIcon: Icons.sell,
          builder: (context) => CatalogScreen(
            controller: boot.catalog,
            businessName: boot.profile.profile?.businessName,
            actions: [menu()],
          ),
        ),
        ShellDestination(
          label: 'Stock',
          icon: Icons.inventory_2_outlined,
          selectedIcon: Icons.inventory_2,
          builder: (context) => StockInScreen(
            db: boot.db,
            companyId: kLocalCompanyId,
            onChanged: () => boot.stockRevision.value++,
          ),
        ),
        ShellDestination(
          label: 'POS',
          icon: Icons.point_of_sale_outlined,
          selectedIcon: Icons.point_of_sale,
          builder: (context) => PosScreen(
            db: boot.db,
            refresh: boot.stockRevision,
            companyId: kLocalCompanyId,
            businessName: boot.profile.profile?.businessName,
          ),
        ),
        ShellDestination(
          label: 'Sales',
          icon: Icons.receipt_long_outlined,
          selectedIcon: Icons.receipt_long,
          builder: (context) => SalesHistoryScreen(
            db: boot.db,
            companyId: kLocalCompanyId,
            businessName: boot.profile.profile?.businessName,
          ),
        ),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FlexiCore',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      builder: (context, child) =>
          MinSizeGuard(child: child ?? const SizedBox.shrink()),
      home: FutureBuilder<_Boot>(
        future: _boot,
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            final e = snapshot.error;
            final text = e is LocalDatabaseException
                ? '${e.message}\n\n${e.path ?? ''}'
                : 'Could not start: $e';
            return _StartupError(text: text);
          }
          if (snapshot.connectionState != ConnectionState.done ||
              !snapshot.hasData) {
            return const _Splash();
          }
          final boot = snapshot.data!;
          return ListenableBuilder(
            listenable: boot.profile,
            builder: (context, _) => boot.profile.profile == null
                ? ProfileSetupScreen(controller: boot.profile)
                : _workspace(boot),
          );
        },
      ),
    );
  }
}

class _Splash extends StatelessWidget {
  const _Splash();

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 76,
              height: 76,
              decoration: BoxDecoration(
                gradient: const LinearGradient(
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                  colors: [AppColors.tealDark, AppColors.teal],
                ),
                borderRadius: BorderRadius.circular(22),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.ink.withValues(alpha: 0.25),
                    blurRadius: 24,
                    offset: const Offset(0, 10),
                  ),
                ],
              ),
              child: const Icon(
                Icons.storefront_outlined,
                color: Colors.white,
                size: 36,
              ),
            ),
            const SizedBox(height: 18),
            const Text(
              'FlexiCore',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 16),
            const SizedBox(
              width: 26,
              height: 26,
              child: CircularProgressIndicator(strokeWidth: 3),
            ),
          ],
        ),
      ),
    );
  }
}

class _StartupError extends StatelessWidget {
  const _StartupError({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: Container(
              padding: const EdgeInsets.all(24),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(18),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.ink.withValues(alpha: 0.10),
                    blurRadius: 32,
                    offset: const Offset(0, 14),
                  ),
                ],
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Icon(
                    Icons.error_outline,
                    color: AppColors.danger,
                    size: 40,
                  ),
                  const SizedBox(height: 12),
                  const Text(
                    'FlexiCore could not start',
                    style: TextStyle(fontSize: 20, fontWeight: FontWeight.w700),
                  ),
                  const SizedBox(height: 8),
                  SelectableText(text),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
