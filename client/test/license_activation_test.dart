import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flexicore_client/features/license/license_service.dart';
import 'package:flutter_test/flutter_test.dart';

LicenseStatus _status(String state, {String? reason}) => LicenseStatus(
  mode: 'local',
  licenseRequired: true,
  state: state,
  reason: reason,
);

void main() {
  test('request code is loaded and exposed', () async {
    final c = LicenseController(
      fetchStatus: () async => _status('missing'),
      importLicense: (_) async => _status('valid'),
      fetchRequestCode: () async => 'FCR1-AAAA-BBBB',
    );
    expect(c.requestLoaded, isFalse);
    await c.loadRequestCode();
    expect(c.requestLoaded, isTrue);
    expect(c.requestCode, 'FCR1-AAAA-BBBB');
  });

  test('unavailable request code is reported as loaded and null', () async {
    final c = LicenseController(
      fetchStatus: () async => _status('missing'),
      importLicense: (_) async => _status('valid'),
      fetchRequestCode: () async => null,
    );
    await c.loadRequestCode();
    expect(c.requestLoaded, isTrue);
    expect(c.requestCode, isNull);
  });

  test('a throwing request fetch never crashes the screen', () async {
    final c = LicenseController(
      fetchStatus: () async => _status('missing'),
      importLicense: (_) async => _status('valid'),
      fetchRequestCode: () async => throw Exception('boom'),
    );
    await c.loadRequestCode();
    expect(c.requestLoaded, isTrue);
    expect(c.requestCode, isNull);
  });

  test('without a request fetcher the code stays null', () async {
    final c = LicenseController(
      fetchStatus: () async => _status('missing'),
      importLicense: (_) async => _status('valid'),
    );
    await c.loadRequestCode();
    expect(c.requestLoaded, isTrue);
    expect(c.requestCode, isNull);
  });

  test(
    'wrong-installation license shows the reissue hint and stays gated',
    () async {
      final c = LicenseController(
        fetchStatus: () async =>
            _status('invalid', reason: 'wrong_installation'),
        importLicense: (_) async => _status('valid'),
      );
      await c.check();
      expect(c.state, LicenseUiState.needsLicense);
      expect(c.hint, contains('different computer'));
    },
  );

  test(
    'an activation code is sent exactly as typed and unlocks the app',
    () async {
      String? sent;
      final c = LicenseController(
        fetchStatus: () async => _status('missing'),
        importLicense: (text) async {
          sent = text;
          return _status('valid');
        },
      );
      final ok = await c.submit('FCL1-ABCDE-FGHIJ');
      expect(ok, isTrue);
      expect(sent, 'FCL1-ABCDE-FGHIJ');
      expect(c.state, LicenseUiState.ready);
    },
  );

  test('a rejected import keeps the app gated and shows the message', () async {
    final c = LicenseController(
      fetchStatus: () async => _status('missing'),
      importLicense: (_) async => throw LicenseImportException(
        'This license was issued for a different computer.',
        statusCode: 422,
      ),
    );
    final ok = await c.submit('FCL1-ABCDE');
    expect(ok, isFalse);
    expect(c.state, isNot(LicenseUiState.ready));
    expect(c.importError, contains('different computer'));
  });
}
