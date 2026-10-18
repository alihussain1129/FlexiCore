import 'package:flexicore_client/core/api_exception.dart';
import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flutter_test/flutter_test.dart';

LicenseController _make(Object error) => LicenseController(
      fetchStatus: () async => throw error,
      importLicense: (_) async => throw UnimplementedError(),
    );

void main() {
  test('network failure is unreachable', () async {
    final c = _make(ApiException.network(Exception('refused')));
    await c.check();
    expect(c.state, LicenseUiState.unreachable);
  });

  for (final code in [401, 403, 500]) {
    test('HTTP $code is blocked but not unreachable', () async {
      final c = _make(ApiException(statusCode: code, title: 'x'));
      await c.check();
      expect(c.state, LicenseUiState.unavailable);
      expect(c.state, isNot(LicenseUiState.ready));
      expect(c.problem, contains('$code'));
    });
  }
}
