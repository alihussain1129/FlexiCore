import 'app_database.dart';

class IntegrityReport {
  const IntegrityReport({required this.problems});

  final List<String> problems;

  bool get isOk => problems.isEmpty;
}

/// Runs SQLite's own consistency checks. Read-only; never changes data.
Future<IntegrityReport> checkDatabaseIntegrity(AppDatabase db) async {
  final problems = <String>[];

  final integrity = await db.customSelect('PRAGMA integrity_check').get();
  for (final row in integrity) {
    final value = row.data.values.first?.toString() ?? '';
    if (value != 'ok') {
      problems.add('integrity: $value');
    }
  }

  final foreignKeys = await db.customSelect('PRAGMA foreign_key_check').get();
  for (final row in foreignKeys) {
    problems.add(
      'foreign key: table ${row.data['table']} row ${row.data['rowid']} '
      'points to a missing ${row.data['parent']}',
    );
  }

  return IntegrityReport(problems: problems);
}
