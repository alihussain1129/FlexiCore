import 'dart:io';

import 'package:path/path.dart' as p;
import 'package:sqlite3/sqlite3.dart' as sql;

import 'app_database.dart';
import 'database_integrity.dart';
import 'database_opener.dart';

/// Raised for any backup or restore problem. The message is safe to show.
class BackupException implements Exception {
  BackupException(this.message, {this.cause});

  final String message;
  final Object? cause;

  @override
  String toString() =>
      'BackupException: $message${cause == null ? '' : ' - $cause'}';
}

class BackupInfo {
  const BackupInfo({required this.schemaVersion, required this.sizeBytes});

  final int schemaVersion;
  final int sizeBytes;
}

class RestoreResult {
  const RestoreResult({required this.safetyCopy, required this.schemaVersion});

  /// The database that was in place before the restore (null if none existed).
  /// It is never deleted automatically.
  final File? safetyCopy;
  final int schemaVersion;
}

String _two(int n) => n.toString().padLeft(2, '0');

String _stamp(DateTime t) =>
    '${t.year}${_two(t.month)}${_two(t.day)}-'
    '${_two(t.hour)}${_two(t.minute)}${_two(t.second)}';

String backupFileName(DateTime t) => 'flexicore-backup-${_stamp(t)}.sqlite';

/// Returns one unused stem for a group of related sibling files.
/// All suffix variants are checked before the stem is selected.
String _availableStem(String baseStem, List<String> suffixes) {
  var candidate = baseStem;
  var index = 1;

  bool isAvailable(String stem) =>
      suffixes.every((suffix) => !File('$stem$suffix').existsSync());

  while (!isAvailable(candidate)) {
    candidate = '$baseStem-$index';
    index++;
  }

  return candidate;
}

void _tryDelete(File f) {
  try {
    if (f.existsSync()) {
      f.deleteSync();
    }
  } catch (_) {}
}

/// Opens [file] read-only and checks it is a usable FlexiCore database.
/// Throws [BackupException] if not. Never modifies the file.
BackupInfo validateBackupFile(File file) {
  if (!file.existsSync()) {
    throw BackupException('The backup file was not found.');
  }
  sql.Database? raw;
  try {
    raw = sql.sqlite3.open(file.path, mode: sql.OpenMode.readOnly);

    final version = raw.select('PRAGMA user_version').first.values.first as int;
    if (version < 1) {
      throw BackupException('That file is not a FlexiCore database.');
    }
    if (version > AppDatabase.currentSchemaVersion) {
      throw BackupException(
        'That backup was made by a newer version of FlexiCore '
        '(schema $version). Update the app before restoring it.',
      );
    }

    final tables = raw
        .select("SELECT name FROM sqlite_master WHERE type = 'table'")
        .map((r) => r['name'] as String)
        .toSet();
    for (final required in const ['units', 'products']) {
      if (!tables.contains(required)) {
        throw BackupException(
          'That file is missing the "$required" table, so it is not a '
          'FlexiCore backup.',
        );
      }
    }

    final problems = <String>[];
    for (final r in raw.select('PRAGMA integrity_check')) {
      final v = r.values.first.toString();
      if (v != 'ok') {
        problems.add(v);
      }
    }
    for (final r in raw.select('PRAGMA foreign_key_check')) {
      problems.add('foreign key problem in ${r['table']}');
    }
    if (problems.isNotEmpty) {
      throw BackupException(
        'The backup failed its integrity check: ${problems.take(3).join('; ')}',
      );
    }

    return BackupInfo(schemaVersion: version, sizeBytes: file.lengthSync());
  } on BackupException {
    rethrow;
  } catch (e) {
    throw BackupException(
      'That file is not a readable FlexiCore backup.',
      cause: e,
    );
  } finally {
    raw?.close();
  }
}

/// Writes a consistent snapshot of the open database to [destination].
/// Never overwrites an existing file. The snapshot is verified before it
/// appears under its final name.
Future<File> createBackup(AppDatabase db, File destination) async {
  if (destination.existsSync()) {
    throw BackupException(
      'A file with that name already exists. Choose a new name.',
    );
  }
  try {
    await destination.parent.create(recursive: true);
  } catch (e) {
    throw BackupException('The backup folder is not available.', cause: e);
  }

  final partial = File('${destination.path}.partial');

  // Never delete an artifact left by an earlier or interrupted attempt.
  if (partial.existsSync()) {
    throw BackupException(
      'A partial backup file already exists. Preserve it and inspect it '
      'before retrying the backup.',
    );
  }

  var partialCreatedByAttempt = false;
  try {
    final quoted = partial.path.replaceAll("'", "''");
    await db.customStatement("VACUUM INTO '$quoted'");
    partialCreatedByAttempt = true;

    validateBackupFile(partial);
    partial.renameSync(destination.path);
    return destination;
  } on BackupException {
    if (partialCreatedByAttempt) {
      _tryDelete(partial);
    }
    rethrow;
  } catch (e) {
    if (partialCreatedByAttempt) {
      _tryDelete(partial);
    }
    throw BackupException(
      'The backup could not be created. Your database was not changed.',
      cause: e,
    );
  }
}

List<String> _rollback(Map<File, File> moved) {
  final failures = <String>[];

  for (final entry in moved.entries) {
    final from = entry.key;
    final to = entry.value;

    try {
      if (to.existsSync() && !from.existsSync()) {
        to.renameSync(from.path);
      }

      if (!from.existsSync() || to.existsSync()) {
        failures.add(from.path);
      }
    } catch (e) {
      failures.add('${from.path}: $e');
    }
  }

  return failures;
}

String _recoveryFailureMessage(List<String> failures) {
  if (failures.isEmpty) {
    return 'Your previous data was put back.';
  }

  return 'Recovery is incomplete. Keep FlexiCore closed and do not use '
      'the database until its files have been inspected. Contact support '
      'for help recovering the previous data.';
}

/// Replaces the local database with [backup].
///
/// The caller must have closed its [AppDatabase] first. The current database
/// is moved aside (never deleted), the backup is opened and checked, and if
/// anything fails the previous database is put back.
Future<RestoreResult> restoreBackup({
  required File backup,
  required Directory directory,
  String fileName = 'flexicore_local.sqlite',
  DateTime Function()? clock,
}) async {
  validateBackupFile(backup);

  final stamp = _stamp((clock ?? DateTime.now)());
  final live = File(p.join(directory.path, fileName));
  final staged = File('${live.path}.restoring');

  // 1. Stage a verified copy next to the live file. Live is untouched so far.
  // Never delete a pre-existing staging file automatically.
  var stagedCreatedByAttempt = false;
  try {
    directory.createSync(recursive: true);
    if (staged.existsSync()) {
      throw BackupException(
        'A restore staging file already exists. Preserve it and inspect it '
        'before retrying the restore.',
      );
    }
    stagedCreatedByAttempt = true;
    backup.copySync(staged.path);
    validateBackupFile(staged);
  } catch (e) {
    if (stagedCreatedByAttempt) {
      _tryDelete(staged);
    }
    if (e is BackupException) {
      rethrow;
    }
    throw BackupException(
      'The backup could not be prepared. Your data was not changed.',
      cause: e,
    );
  }

  // 2. Move the current database (and any WAL files) aside, then swap.
  final moved = <File, File>{};
  File? safety;
  final safetyStem = _availableStem('${live.path}.pre-restore-$stamp', const [
    '',
    '-wal',
    '-shm',
  ]);
  try {
    for (final suffix in const ['', '-wal', '-shm']) {
      final from = File('${live.path}$suffix');
      if (from.existsSync()) {
        final to = File('$safetyStem$suffix');
        from.renameSync(to.path);
        moved[from] = to;
        if (suffix == '') {
          safety = to;
        }
      }
    }
    staged.renameSync(live.path);
  } catch (e) {
    final recoveryFailures = _rollback(moved);
    throw BackupException(
      'The restore could not be completed. '
      '${_recoveryFailureMessage(recoveryFailures)}',
      cause: e,
    );
  }

  // 3. Open the restored file (runs any needed migration) and check it.
  try {
    final db = await openAppDatabase(directory: directory, fileName: fileName);
    try {
      final report = await checkDatabaseIntegrity(db);
      if (!report.isOk) {
        throw BackupException(report.problems.take(3).join('; '));
      }
    } finally {
      await db.close();
    }
  } catch (e) {
    // Keep the bad file for inspection, put the old database back.
    final failedStem = _availableStem(
      '${live.path}.failed-restore-$stamp',
      const ['', '-wal', '-shm'],
    );
    final artifactMoveFailures = <String>[];
    for (final suffix in const ['', '-wal', '-shm']) {
      final bad = File('${live.path}$suffix');
      try {
        if (bad.existsSync()) {
          bad.renameSync('$failedStem$suffix');
        }
      } catch (_) {
        artifactMoveFailures.add(
          'A failed-restore artifact could not be preserved.',
        );
      }
    }

    final recoveryFailures = <String>[
      ...artifactMoveFailures,
      ..._rollback(moved),
    ];
    throw BackupException(
      'The restored file could not be opened or failed its integrity check. '
      '${_recoveryFailureMessage(recoveryFailures)}',
      cause: e,
    );
  }

  final version = validateBackupFile(live).schemaVersion;
  return RestoreResult(safetyCopy: safety, schemaVersion: version);
}
