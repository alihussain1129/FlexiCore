import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/core/api_exception.dart';
import 'package:flexicore_client/features/license/license_controller.dart';
import 'package:flexicore_client/features/license/license_service.dart';

LicenseStatus _local(String state) =>
    LicenseStatus(mode: 'local', licenseRequired: true, state: state);

LicenseController _controller({
  Future<LicenseStatus?> Function()? status,
  Future<LicenseStatus> Function(String)? onImport,
}) => LicenseController(
  fetchStatus: status ?? () async => null,
  importLicense: onImport ?? (_) async => _local('valid'),
);

void main() {
  group('check', () {
    test('missing license status does not bypass the license gate', () async {
      final c = _controller(status: () async => null);
      await c.check();
      expect(c.state, LicenseUiState.unreachable);
      expect(c.problem, isNotNull);
    });

    test('hosted mode needs no license', () async {
      final c = _controller(
        status: () async => const LicenseStatus(
          mode: 'hosted',
          licenseRequired: false,
          state: 'notApplicable',
        ),
      );
      await c.check();
      expect(c.state, LicenseUiState.ready);
    });

    for (final state in ['valid', 'validInGrace']) {
      test('local mode with $state continues', () async {
        final c = _controller(status: () async => _local(state));
        await c.check();
        expect(c.state, LicenseUiState.ready);
      });
    }

    for (final state in ['missing', 'invalid', 'notYetValid']) {
      test('local mode with $state asks for a license', () async {
        final c = _controller(status: () async => _local(state));
        await c.check();
        expect(c.state, LicenseUiState.needsLicense);
      });
    }

    test('unavailable fails closed', () async {
      final c = _controller(status: () async => _local('unavailable'));
      await c.check();
      expect(c.state, LicenseUiState.unavailable);
      expect(c.problem, isNotNull);
    });

    test('an unknown state fails closed', () async {
      final c = _controller(status: () async => _local('somethingNew'));
      await c.check();
      expect(c.state, LicenseUiState.unavailable);
    });

    test('a network failure is unreachable and a retry can recover', () async {
      var calls = 0;
      final c = _controller(
        status: () async {
          calls++;
          if (calls == 1) {
            throw ApiException(statusCode: 0, title: 'offline');
          }
          return _local('valid');
        },
      );
      await c.check();
      expect(c.state, LicenseUiState.unreachable);
      await c.check();
      expect(c.state, LicenseUiState.ready);
    });

    test('an expired license explains itself', () async {
      final c = _controller(status: () async => _local('expired'));
      await c.check();
      expect(c.hint, contains('expired'));
    });
  });

  group('submit', () {
    test('empty text is refused without calling the server', () async {
      var calls = 0;
      final c = _controller(
        status: () async => _local('missing'),
        onImport: (_) async {
          calls++;
          return _local('valid');
        },
      );
      await c.check();
      expect(await c.submit('   '), isFalse);
      expect(calls, 0);
      expect(c.importError, isNotNull);
    });

    test('a valid license lets the app continue', () async {
      String? sent;
      final c = _controller(
        status: () async => _local('missing'),
        onImport: (text) async {
          sent = text;
          return _local('valid');
        },
      );
      await c.check();
      expect(await c.submit('{"license":1}\n'), isTrue);
      expect(sent, '{"license":1}\n');
      expect(c.state, LicenseUiState.ready);
      expect(c.busy, isFalse);
    });

    test(
      'a rejected license keeps the gate closed with a safe message',
      () async {
        final c = _controller(
          status: () async => _local('missing'),
          onImport: (_) async => throw LicenseImportException(
            'This license is not authentic and was rejected.',
            statusCode: 422,
          ),
        );
        await c.check();
        expect(await c.submit('x'), isFalse);
        expect(c.state, LicenseUiState.needsLicense);
        expect(c.importError, contains('not authentic'));
        expect(c.busy, isFalse);
      },
    );

    test('an unexpected error gives a generic message', () async {
      final c = _controller(
        status: () async => _local('missing'),
        onImport: (_) async => throw StateError('boom'),
      );
      await c.check();
      expect(await c.submit('x'), isFalse);
      expect(c.importError, isNot(contains('boom')));
      expect(c.state, LicenseUiState.needsLicense);
    });

    test('a second submit while busy is ignored', () async {
      var calls = 0;
      final gate = Completer<LicenseStatus>();
      final c = _controller(
        status: () async => _local('missing'),
        onImport: (_) {
          calls++;
          return gate.future;
        },
      );
      await c.check();
      final first = c.submit('x');
      expect(await c.submit('y'), isFalse);
      expect(calls, 1);
      gate.complete(_local('valid'));
      expect(await first, isTrue);
    });
  });
}
