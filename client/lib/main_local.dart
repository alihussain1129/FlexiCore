import 'package:flutter/material.dart';

import 'data/local/app_database.dart';
import 'data/local/database_opener.dart';
import 'features/catalog/data/local_catalog_repository.dart';
import 'features/catalog/presentation/catalog_controller.dart';
import 'features/catalog/presentation/catalog_screen.dart';
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
  _Boot(this.profile, this.catalog);
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
  late final Future<_Boot> _boot = _start();

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
    return _Boot(profile, catalog);
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
          if (!snapshot.hasData) {
            return const Scaffold(
              body: Center(child: CircularProgressIndicator()),
            );
          }
          final boot = snapshot.data!;
          return ListenableBuilder(
            listenable: boot.profile,
            builder: (context, _) => boot.profile.profile == null
                ? ProfileSetupScreen(controller: boot.profile)
                : CatalogScreen(controller: boot.catalog),
          );
        },
      ),
    );
  }
}