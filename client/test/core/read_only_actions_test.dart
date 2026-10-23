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

int _taps = 0;

class _Probe extends StatelessWidget {
  const _Probe();

  @override
  Widget build(BuildContext context) {
    return FilledButton(
      onPressed: () {
        if (isReadOnlyNow(context)) {
          showReadOnlyNotice(context);
          return;
        }
        _taps++;
      },
      child: const Text('Go'),
    );
  }
}

Widget _app(LicenseController? license) {
  const app = MaterialApp(home: Scaffold(body: _Probe()));
  if (license == null) return app;
  return ChangeNotifierProvider<LicenseController>.value(
    value: license,
    child: app,
  );
}

void main() {
  setUp(() => _taps = 0);

  testWidgets('action runs when there is no license provider', (tester) async {
    await tester.pumpWidget(_app(null));
    await tester.tap(find.text('Go'));
    await tester.pump();
    expect(_taps, 1);
  });

  testWidgets('action runs when the license is active', (tester) async {
    final license = _FakeLicense(false);
    addTearDown(license.dispose);
    await tester.pumpWidget(_app(license));
    await tester.tap(find.text('Go'));
    await tester.pump();
    expect(_taps, 1);
  });

  testWidgets('action is blocked and a notice shows when expired', (tester) async {
    final license = _FakeLicense(true);
    addTearDown(license.dispose);
    await tester.pumpWidget(_app(license));
    await tester.tap(find.text('Go'));
    await tester.pump();
    expect(_taps, 0);
    expect(find.textContaining('read-only'), findsOneWidget);
  });
}