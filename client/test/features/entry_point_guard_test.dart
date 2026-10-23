import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

const _entryPoints = {
  'lib/main.dart',
  'lib/main_local.dart',
  'lib/main_device.dart',
};
const _runAppAllowed = {'lib/main.dart', 'lib/main_device.dart'};

final _mainDecl = RegExp(
  r'^\s*(?:[\w<>?]+\s+)?main\s*\([^)]*\)\s*(?:async\s*)?(?:\{|=>)',
  multiLine: true,
);
final _runApp = RegExp(r'\brunApp\s*\(');
final _import = RegExp(
  r'''^\s*(?:import|export)\s+['"]([^'"]+)['"]''',
  multiLine: true,
);

String _rel(File f) => f.path.replaceAll('\\', '/');

Iterable<File> _libFiles() => Directory('lib')
    .listSync(recursive: true)
    .whereType<File>()
    .where((f) => f.path.endsWith('.dart'));

String? _resolve(String from, String uri) {
  if (uri.startsWith('dart:')) return null;
  if (uri.startsWith('package:flexicore_client/')) {
    return 'lib/${uri.substring('package:flexicore_client/'.length)}';
  }
  if (uri.startsWith('package:')) return null;
  final base = from.substring(0, from.lastIndexOf('/'));
  final parts = <String>[];
  for (final p in '$base/$uri'.split('/')) {
    if (p == '..') {
      if (parts.isNotEmpty) parts.removeLast();
    } else if (p != '.' && p.isNotEmpty) {
      parts.add(p);
    }
  }
  return parts.join('/');
}

void main() {
  test('only the three known files declare main()', () {
    final offenders = [
      for (final f in _libFiles())
        if (!_entryPoints.contains(_rel(f)) &&
            _mainDecl.hasMatch(f.readAsStringSync()))
          _rel(f),
    ];
    expect(offenders, isEmpty);
  });

  test('runApp is only called from main.dart and main_device.dart', () {
    final offenders = [
      for (final f in _libFiles())
        if (!_runAppAllowed.contains(_rel(f)) &&
            _runApp.hasMatch(f.readAsStringSync()))
          _rel(f),
    ];
    expect(offenders, isEmpty);
  });

  test('main_local.dart is only a stub that calls the official main', () {
    final text = File('lib/main_local.dart').readAsStringSync();
    expect(text, contains("import 'main.dart'"));
    expect(text, contains('official.main()'));
    for (final bad in [
      'runApp',
      'LocalWorkspaceApp',
      'ApiClient',
      'AppDatabase',
    ]) {
      expect(text.contains(bad), isFalse, reason: bad);
    }
  });

  test('main_device.dart cannot reach the local ERP through any import', () {
    final seen = <String>{};
    final queue = ['lib/main_device.dart'];
    while (queue.isNotEmpty) {
      final cur = queue.removeLast();
      if (!seen.add(cur)) continue;
      final f = File(cur);
      if (!f.existsSync()) continue;
      for (final m in _import.allMatches(f.readAsStringSync())) {
        final next = _resolve(cur, m.group(1)!);
        if (next != null) queue.add(next);
      }
    }
    final forbidden = seen.where(
      (p) =>
          p == 'lib/local_workspace.dart' ||
          p.startsWith('lib/data/local/') ||
          p.contains('/catalog/') ||
          p.contains('/inventory/') ||
          p.contains('/pos/'),
    );
    expect(forbidden, isEmpty, reason: 'reached via main_device.dart');
  });

  test('LocalWorkspaceApp is only constructed from main.dart', () {
    final ctor = RegExp(r'\bLocalWorkspaceApp\s*\(');
    final offenders = [
      for (final f in _libFiles())
        if (ctor.hasMatch(f.readAsStringSync()) &&
            _rel(f) != 'lib/main.dart' &&
            _rel(f) != 'lib/local_workspace.dart')
          _rel(f),
    ];
    expect(offenders, isEmpty);
  });
}