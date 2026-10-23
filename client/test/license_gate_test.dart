import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flexicore_client/features/license/license_screen.dart';
import 'package:flexicore_client/main.dart';

/// A controller whose state is fixed, so the gate can be tested without a
/// backend. It never calls the network.
class _FakeLicense extends LicenseController {
  _FakeLicense(this._fixed)
    : super(
        fetchStatus: () async => null,
        importLicense: (_) async => throw UnimplementedError(),
      );

  final LicenseUiState _fixed;

  @override
  LicenseUiState get state => _fixed;
}

Future<void> _pumpGate(WidgetTester tester, LicenseUiState state) async {
  await tester.pumpWidget(
    ChangeNotifierProvider<LicenseController>.value(
      value: _FakeLicense(state),
      child: MaterialApp(
        home: LicenseGate(workspaceBuilder: (_) => const Text('WORKSPACE_MARKER')),
      ),
    ),
  );
  await tester.pump();
}

void main() {
  for (final state in [
    LicenseUiState.checking,
    LicenseUiState.needsLicense,
    LicenseUiState.unavailable,
    LicenseUiState.unreachable,
  ]) {
    testWidgets('gate blocks the workspace when state is $state', (tester) async {
      await _pumpGate(tester, state);
      expect(find.text('WORKSPACE_MARKER'), findsNothing);
      expect(find.byType(LicenseScreen), findsOneWidget);
    });
  }

  testWidgets('gate opens the workspace only when state is ready', (tester) async {
    await _pumpGate(tester, LicenseUiState.ready);
    expect(find.text('WORKSPACE_MARKER'), findsOneWidget);
    expect(find.byType(LicenseScreen), findsNothing);
  });
}