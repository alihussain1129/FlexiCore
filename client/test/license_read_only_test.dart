import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flexicore_client/features/license/license_service.dart';

LicenseStatus status(String state, {String mode = 'local', bool required = true}) =>
    LicenseStatus(mode: mode, licenseRequired: required, state: state);

LicenseController build(LicenseStatus Function() current) => LicenseController(
  fetchStatus: () async => current(),
  importLicense: (_) async => current(),
);

void main() {
  test('expired gate is read-only', () {
    expect(status('expired').gate, LicenseGate.readOnly);
    expect(status('missing').gate, LicenseGate.needsLicense);
    expect(status('invalid').gate, LicenseGate.needsLicense);
    expect(status('notYetValid').gate, LicenseGate.needsLicense);
    expect(status('unavailable').gate, LicenseGate.unavailable);
  });

  test('expired license opens the app in read-only mode', () async {
    final c = build(() => status('expired'));
    await c.check();
    expect(c.state, LicenseUiState.ready);
    expect(c.isReadOnly, isTrue);
  });

  test('valid license is not read-only', () async {
    final c = build(() => status('valid'));
    await c.check();
    expect(c.state, LicenseUiState.ready);
    expect(c.isReadOnly, isFalse);
  });

  test('missing license still needs activation', () async {
    final c = build(() => status('missing'));
    await c.check();
    expect(c.state, LicenseUiState.needsLicense);
    expect(c.isReadOnly, isFalse);
  });

  test('hosted mode is never read-only', () async {
    final c = build(() => status('notApplicable', mode: 'hosted', required: false));
    await c.check();
    expect(c.state, LicenseUiState.ready);
    expect(c.isReadOnly, isFalse);
  });

  test('renew then continue returns to read-only', () async {
    final c = build(() => status('expired'));
    await c.check();
    c.openActivation();
    expect(c.state, LicenseUiState.needsLicense);
    c.closeActivation();
    await Future<void>.delayed(Duration.zero);
    await Future<void>.delayed(Duration.zero);
    expect(c.state, LicenseUiState.ready);
    expect(c.isReadOnly, isTrue);
  });

  test('refreshStatus picks up an expiry that happened while signed in', () async {
    var current = status('valid');
    final c = build(() => current);
    await c.check();
    expect(c.isReadOnly, isFalse);
    current = status('expired');
    await c.refreshStatus();
    expect(c.isReadOnly, isTrue);
    expect(c.state, LicenseUiState.ready);
  });
}