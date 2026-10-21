import 'dart:io';

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'data/local/app_database.dart';
import 'data/local/database_backup.dart';
import 'data/local/database_opener.dart';
import 'features/catalog/data/local_catalog_repository.dart';
import 'features/catalog/presentation/catalog_controller.dart';
import 'features/catalog/presentation/catalog_screen.dart';
import 'features/local_data/local_data_menu.dart';
import 'features/profile/data/local_profile_repository.dart';
import 'features/profile/presentation/profile_controller.dart';
import 'features/profile/presentation/profile_setup_screen.dart';

/// Local entry point: runs entirely on the local database and never creates
/// an API client, so it works with the network off.
/// Run with `flutter run -t lib/main_local.dart`.
const String kLocalCompanyId = 'local-company';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const _LocalApp());
}

class _Boot {
  _Boot(this.db, this.profile, this.catalog);
  final AppDatabase db;
  final ProfileController profile;
  final CatalogController catalog;
}

class _LocalApp extends StatefulWidget {
  const _LocalApp();

  @override
  State<_LocalApp> createState() => _LocalAppState();
}

class _LocalAppState extends State<_LocalApp> {
  AppDatabase? _db;
  late Future<_Boot> _boot = _start();

  Future<_Boot> _start() async {
    final db = await openAppDatabase();
    _db = db;
    final profile = ProfileController(
      LocalProfileRepository(db, companyId: kLocalCompanyId),
    );
    await profile.load();
    final catalog = CatalogController(
      LocalCatalogRepository(db, companyId: kLocalCompanyId),
    );
    await catalog.load();
    return _Boot(db, profile, catalog);
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
        setState(() => _boot = _start());
      }
    }
  }

  @override
  void dispose() {
    _db?.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FlexiCore (local)',
      theme: ThemeData(colorSchemeSeed: Colors.indigo, useMaterial3: true),
      home: FutureBuilder<_Boot>(
        future: _boot,
        builder: (context, snapshot) {
          if (snapshot.hasError) {
            final e = snapshot.error;
            final text = e is LocalDatabaseException
                ? '${e.message}\n\n${e.path ?? ''}'
                : 'Could not start: $e';
            return Scaffold(
              appBar: AppBar(title: const Text('FlexiCore')),
              body: Center(
                child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: SelectableText(text),
                ),
              ),
            );
          }
          if (snapshot.connectionState != ConnectionState.done ||
              !snapshot.hasData) {
            return const Scaffold(
              body: Center(child: CircularProgressIndicator()),
            );
          }
          final boot = snapshot.data!;
          return ListenableBuilder(
            listenable: boot.profile,
            builder: (context, _) => boot.profile.profile == null
                ? ProfileSetupScreen(controller: boot.profile)
                : CatalogScreen(
                    controller: boot.catalog,
                    actions: [
                      LocalDataMenu(
                        db: boot.db,
                        profile: boot.profile,
                        onRestore: _restore,
                      ),
                    ],
                  ),
          );
        },
      ),
    );
  }
}