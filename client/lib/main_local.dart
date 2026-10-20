import 'package:flutter/material.dart';

import 'data/local/app_database.dart';
import 'data/local/database_opener.dart';
import 'features/catalog/data/local_catalog_repository.dart';
import 'features/catalog/presentation/catalog_controller.dart';
import 'features/catalog/presentation/catalog_screen.dart';

/// Phase C entry point: runs the catalog entirely on the local database.
/// It never creates an API client, so it works with the network off.
///
/// Temporary: one fixed local company until Phase D adds real local owner
/// setup. Run with `flutter run -t lib/main_local.dart`.
const String kLocalCompanyId = 'local-company';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const _LocalApp());
}

class _LocalApp extends StatefulWidget {
  const _LocalApp();

  @override
  State<_LocalApp> createState() => _LocalAppState();
}

class _LocalAppState extends State<_LocalApp> {
  AppDatabase? _db;
  late final Future<CatalogController> _controller = _start();

  Future<CatalogController> _start() async {
    final db = await openAppDatabase();
    _db = db;
    final controller = CatalogController(
      LocalCatalogRepository(db, companyId: kLocalCompanyId),
    );
    await controller.load();
    return controller;
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
      home: FutureBuilder<CatalogController>(
        future: _controller,
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
          return CatalogScreen(controller: snapshot.data!);
        },
      ),
    );
  }
}
