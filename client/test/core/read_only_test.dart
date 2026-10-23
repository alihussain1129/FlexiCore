import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:flexicore_client/core/read_only.dart';
import 'package:flexicore_client/features/license/license_controller.dart';

class _FakeLicense extends LicenseController {
  _FakeLicense(this._readOnly)
    : super(
        fetchStatus: () async => null,
        importLicense: (_) async => throw UnimplementedError(),
      );

  final bool _readOnly;

  @override
  bool get isReadOnly => _readOnly;
}

class _Probe extends StatelessWidget {
  const _Probe();

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: isReadOnlyOf(context) ? null : () {},
      child: const Text('Save'),
    );
  }
}

Widget _app({LicenseController? license}) {
  final app = const MaterialApp(home: Scaffold(body: _Probe()));
  if (license == null) return app;
  return ChangeNotifierProvider<LicenseController>.value(
    value: license,
    child: app,
  );
}

bool _enabled(WidgetTester tester) =>
    tester.widget<FilledButton>(find.byType(FilledButton)).onPressed != null;

void main() {
  testWidgets('button stays enabled when there is no license provider', (
    tester,
  ) async {
    await tester.pumpWidget(_app());
    expect(_enabled(tester), isTrue);
  });

  testWidgets('button stays enabled when the license is active', (
    tester,
  ) async {
    final license = _FakeLicense(false);
    addTearDown(license.dispose);
    await tester.pumpWidget(_app(license: license));
    expect(_enabled(tester), isTrue);
  });

  testWidgets('button is disabled when the license has expired', (
    tester,
  ) async {
    final license = _FakeLicense(true);
    addTearDown(license.dispose);
    await tester.pumpWidget(_app(license: license));
    expect(_enabled(tester), isFalse);
  });

  test('friendlyError shows the clean message for a blocked write', () {
    final text = friendlyError(const ReadOnlyLicenseException(), 'Could not save');
    expect(text, const ReadOnlyLicenseException().toString());
    expect(text.startsWith('Could not save'), isFalse);
  });

  test('friendlyError keeps the prefix for other errors', () {
    expect(friendlyError(StateError('boom'), 'Could not save'), startsWith('Could not save: '));
  });
}