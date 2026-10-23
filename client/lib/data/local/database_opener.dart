import 'dart:io';

import 'package:drift/drift.dart'
    show ApplyInterceptor, BatchedStatements, QueryExecutor, QueryInterceptor;
import 'package:drift/native.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';
import 'package:sqlite3/sqlite3.dart' as sql;

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

/// Thrown when a write is attempted while the license has expired.
/// Reads, backup and restore are not affected.
class ReadOnlyLicenseException implements Exception {
  const ReadOnlyLicenseException();

  @override
  String toString() =>
      'Your license has expired, so FlexiCore is read-only. '
      'Renew the license to make changes.';
}

/// Blocks every write statement while [_isReadOnly] returns true.
/// It does nothing until [armed] is set, so migrations are never blocked.
class _WriteGuard extends QueryInterceptor {
  _WriteGuard(this._isReadOnly);

  final bool Function() _isReadOnly;
  bool armed = false;

  static final RegExp _writeSql = RegExp(
    r'^\s*(INSERT|UPDATE|DELETE|REPLACE)\b',
    caseSensitive: false,
  );

  void _block() {
    if (armed && _isReadOnly()) throw const ReadOnlyLicenseException();
  }

  void _blockIfWrite(String sql) {
    if (_writeSql.hasMatch(sql)) _block();
  }

  @override
  Future<int> runInsert(
    QueryExecutor executor,
    String statement,
    List<Object?> args,
  ) async {
    _block();
    return super.runInsert(executor, statement, args);
  }

  @override
  Future<int> runUpdate(
    QueryExecutor executor,
    String statement,
    List<Object?> args,
  ) async {
    _block();
    return super.runUpdate(executor, statement, args);
  }

  @override
  Future<int> runDelete(
    QueryExecutor executor,
    String statement,
    List<Object?> args,
  ) async {
    _block();
    return super.runDelete(executor, statement, args);
  }

  @override
  Future<void> runBatched(
    QueryExecutor executor,
    BatchedStatements statements,
  ) async {
    _block();
    return super.runBatched(executor, statements);
  }

  @override
  Future<void> runCustom(
    QueryExecutor executor,
    String statement,
    List<Object?> args,
  ) async {
    _blockIfWrite(statement);
    return super.runCustom(executor, statement, args);
  }

  // Drift sends INSERT/UPDATE ... RETURNING through runSelect.
  @override
  Future<List<Map<String, Object?>>> runSelect(
    QueryExecutor executor,
    String statement,
    List<Object?> args,
  ) async {
    _blockIfWrite(statement);
    return super.runSelect(executor, statement, args);
  }
}

/// Opens (creating and migrating if needed) the persistent business database.
///
/// Data lives in the platform's application-support directory, never in a
/// temp or cache directory. [directory] exists so tests can use a temp folder.
Future<AppDatabase> openAppDatabase({
  Directory? directory,
  String fileName = 'flexicore_local.sqlite',
  bool Function()? isReadOnly,
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
  // An older database gets a verified copy before any upgrade runs.
  await copyBeforeUpgrade(file);

  QueryExecutor executor = NativeDatabase.createInBackground(
    file,
    setup: (raw) {
      // WAL: readers do not block the writer; safer for a long-lived app.
      raw.execute('PRAGMA journal_mode = WAL;');
      raw.execute('PRAGMA busy_timeout = 5000;');
    },
  );
  // The write guard is only installed when the caller supplies a license
  // check ([isReadOnly]); every other caller behaves exactly as before.
  _WriteGuard? guard;
  if (isReadOnly != null) {
    guard = _WriteGuard(isReadOnly);
    executor = executor.interceptWith(guard);
  }
  final db = AppDatabase(executor);

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
  // Armed only now: the database is open and migrated, so no upgrade can be
  // blocked. From here on, writes follow the live license state.
  guard?.armed = true;
  return db;
}

String _two(int n) => n.toString().padLeft(2, '0');

/// Before an older database is upgraded, writes a verified copy next to it
/// (`<file>.pre-v3-<time>`). If the copy cannot be made the upgrade is not
/// started and the data file is left exactly as it was.
/// Returns the copy, or null when no upgrade is pending.
Future<File?> copyBeforeUpgrade(File file, {DateTime Function()? clock}) async {
  if (!file.existsSync()) return null;

  sql.Database? raw;
  sql.Database? check;
  File? target;
  var createdByThisAttempt = false;

  try {
    raw = sql.sqlite3.open(file.path);
    final version = raw.select('PRAGMA user_version').first.values.first as int;

    if (version < 1 || version >= AppDatabase.currentSchemaVersion) {
      return null;
    }

    final t = (clock ?? DateTime.now)();
    final stamp =
        '${t.year}${_two(t.month)}${_two(t.day)}-'
        '${_two(t.hour)}${_two(t.minute)}${_two(t.second)}';

    final basePath =
        '${file.path}.pre-v${AppDatabase.currentSchemaVersion}-$stamp';

    // Never reuse or overwrite a pre-existing safety copy.
    var candidate = File(basePath);
    var suffix = 1;

    while (candidate.existsSync()) {
      candidate = File('$basePath-$suffix');
      suffix++;
    }

    target = candidate;
    final quoted = candidate.path.replaceAll("'", "''");
    raw.execute("VACUUM INTO '$quoted'");
    createdByThisAttempt = true;

    check = sql.sqlite3.open(candidate.path, mode: sql.OpenMode.readOnly);

    final copied = check.select('PRAGMA user_version').first.values.first;
    final integrity = check
        .select('PRAGMA integrity_check')
        .first
        .values
        .first
        .toString();

    if (copied != version || integrity != 'ok') {
      throw StateError('the safety copy did not verify');
    }

    return target;
  } catch (e) {
    // Delete only a copy this attempt successfully created.
    if (createdByThisAttempt && target != null) {
      try {
        if (target.existsSync()) target.deleteSync();
      } catch (_) {}
    }

    throw LocalDatabaseException(
      'A safety copy could not be made before upgrading, so the upgrade was '
      'not started. Your data file has not been modified or deleted.',
      path: file.path,
      cause: e,
    );
  } finally {
    check?.close();
    raw?.close();
  }
}
