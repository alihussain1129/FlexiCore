import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flexicore_client/features/license/license_service.dart';

LicenseStatus _status(String state, {String? reason}) => LicenseStatus(
  mode: 'local',
  licenseRequired: true,
  state: state,
  reason: reason,
);

LicenseController _controller(LicenseStatus s) =>
    LicenseController(fetchStatus: () async => s, importLicense: (_) async => s);

void main() {
  test('a revoked license maps to needsLicense', () {
    expect(_status('revoked').gate, LicenseGate.needsLicense);
  });

  test('a revoked license keeps the app gated and says it was revoked', () async {
    final c = _controller(_status('revoked'));
    await c.check();
    expect(c.state, LicenseUiState.needsLicense);
    expect(c.hint, contains('revoked'));
  });

  test('a license for another computer explains the mismatch', () async {
    final c = _controller(_status('invalid', reason: 'wrong_installation'));
    await c.check();
    expect(c.hint, contains('different computer'));
  });

  test('an unbound license explains that it is not tied to this computer', () async {
    final c = _controller(_status('invalid', reason: 'not_bound'));
    await c.check();
    expect(c.hint, contains('not tied to this computer'));
  });

  test('a stale revocation snapshot explains that revocation cannot be confirmed', () async {
    final c = _controller(_status('invalid', reason: 'revocation_snapshot_stale'));
    await c.check();
    expect(c.hint, contains('revocation status'));
  });

  for (final state in ['valid', 'validInGrace']) {
    test('$state continues and shows no hint', () async {
      final c = _controller(_status(state));
      await c.check();
      expect(c.state, LicenseUiState.ready);
      expect(c.hint, isNull);
    });
  }
}
