import 'dart:io';

import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'app_database.dart';

/// Raised when the local database cannot be opened or migrated.
///
/// The UI should show [message] and stop, rather than continue with an
/// empty or partially migrated database. The file on disk is never deleted.
class LocalDatabaseException implements Exception {
  LocalDatabaseException(this.message, {this.path, this.cause});

  final String message;
  final String? path;
  final Object? cause;

  @override
  String toString() =>
      'LocalDatabaseException: $message'
      '${path == null ? '' : ' ($path)'}'
      '${cause == null ? '' : ' - $cause'}';
}

/// Opens (creating and migrating if needed) the persistent business database.
///
/// Data lives in the platform's application-support directory, never in a
/// temp or cache directory. [directory] exists so tests can use a temp folder.
Future<AppDatabase> openAppDatabase({
  Directory? directory,
  String fileName = 'flexicore_local.sqlite',
}) async {
  final Directory dir;
  try {
    dir = directory ?? await getApplicationSupportDirectory();
    await dir.create(recursive: true);
  } catch (e) {
    throw LocalDatabaseException(
      'The application data folder is not available.',
      cause: e,
    );
  }

  final file = File(p.join(dir.path, fileName));
  final db = AppDatabase(
    NativeDatabase.createInBackground(
      file,
      setup: (raw) {
        // WAL: readers do not block the writer; safer for a long-lived app.
        raw.execute('PRAGMA journal_mode = WAL;');
        raw.execute('PRAGMA busy_timeout = 5000;');
      },
    ),
  );

  try {
    // Forces the connection to open and any migration to run now, so a
    // failure surfaces here instead of on the first screen that reads data.
    await db.customSelect('SELECT 1').get();
  } catch (e) {
    await db.close();
    throw LocalDatabaseException(
      'The local database could not be opened or upgraded. '
      'Your data file has not been modified or deleted.',
      path: file.path,
      cause: e,
    );
  }
  return db;
}
