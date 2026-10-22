// ignore_for_file: use_null_aware_elements

import 'dart:convert';

import 'package:flexicore_client/core/api_client.dart';
import 'package:flexicore_client/core/token_storage.dart';
import 'package:flexicore_client/features/license/license_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

http.Response _json(int status, Object body) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

Map<String, dynamic> _status(
  String state, {
  String mode = 'local',
  bool licenseRequired = true,
  String? reason,
  Map<String, dynamic>? license,
}) => {
  'mode': mode,
  'licenseRequired': licenseRequired,
  'state': state,
  'reason': reason,
  'license': license,
  'checkedAt': '2026-06-01T00:00:00Z',
};

Map<String, dynamic> _problem(int status, {String? reason}) => {
  'type': 'urn:flexicore:problem:license-rejected',
  'title': 'The license was rejected',
  'status': status,
  if (reason case final value?) 'reason': value,
};

void main() {
  late InMemoryTokenStorage storage;

  LicenseService build(MockClient mock) => LicenseService(
    ApiClient(baseUrl: 'http://test', storage: storage, httpClient: mock),
  );

  setUp(() => storage = InMemoryTokenStorage());

  group('gate', () {
    const expected = {
      'valid': LicenseGate.proceed,
      'validInGrace': LicenseGate.proceed,
      'missing': LicenseGate.needsLicense,
      'invalid': LicenseGate.needsLicense,
      'expired': LicenseGate.readOnly,
      'notYetValid': LicenseGate.needsLicense,
      'unavailable': LicenseGate.unavailable,
      'someFutureState': LicenseGate.unavailable,
    };

    expected.forEach((state, gate) {
      test('local $state maps to $gate', () {
        expect(LicenseStatus.fromJson(_status(state)).gate, gate);
      });
    });

    test('hosted mode never needs a license', () {
      final status = LicenseStatus.fromJson(
        _status('notApplicable', mode: 'hosted', licenseRequired: false),
      );
      expect(status.gate, LicenseGate.proceed);
    });
  });

  group('fetchStatus', () {
    test('sends an anonymous GET, even when a token exists', () async {
      await ApiClient(
        baseUrl: 'http://test',
        storage: storage,
      ).acceptTokens({'refreshToken': 'r0', 'accessToken': 'a0'});
      late http.BaseRequest seen;
      final api = ApiClient(
        baseUrl: 'http://test',
        storage: storage,
        httpClient: MockClient((request) async {
          seen = request;
          return _json(200, _status('missing'));
        }),
      );
      await api.acceptTokens({'refreshToken': 'r0', 'accessToken': 'a0'});

      final status = await LicenseService(api).fetchStatus();

      expect(status!.state, 'missing');
      expect(seen.method, 'GET');
      expect(seen.url.path, '/api/v1/license/status');
      expect(seen.headers.containsKey('Authorization'), isFalse);
    });

    test('parses license details', () async {
      final service = build(
        MockClient(
          (request) async => _json(
            200,
            _status(
              'valid',
              license: {
                'licenseId': 'LIC-0001',
                'licensee': 'Test Customer',
                'expiresAt': '2027-01-01T00:00:00Z',
                'graceEndsAt': null,
              },
            ),
          ),
        ),
      );

      final status = await service.fetchStatus();

      expect(status!.license!.licenseId, 'LIC-0001');
      expect(status.license!.expiresAt, DateTime.utc(2027, 1, 1));
      expect(status.license!.graceEndsAt, isNull);
    });

    test(
      'a 401 means anonymous access is not offered and returns null',
      () async {
        final service = build(MockClient((request) async => _json(401, {})));
        expect(await service.fetchStatus(), isNull);
      },
    );

    test('a network failure throws a network ApiException', () async {
      final service = build(
        MockClient((request) async {
          throw http.ClientException('down');
        }),
      );
      await expectLater(
        service.fetchStatus(),
        throwsA(predicate((e) => e.toString().contains('ApiException(0'))),
      );
    });

    test('a 500 throws', () async {
      final service = build(MockClient((request) async => _json(500, {})));
      await expectLater(service.fetchStatus(), throwsA(anything));
    });
  });

  group('importLicense', () {
    const licenseText = '{"licensee":"Test Customer","signature":"abc"}';

    test('sends the text exactly, as JSON, with no token', () async {
      late http.Request seen;
      final api = ApiClient(
        baseUrl: 'http://test',
        storage: storage,
        httpClient: MockClient((request) async {
          seen = request;
          return _json(200, {..._status('valid'), 'replaced': true});
        }),
      );
      await api.acceptTokens({'refreshToken': 'r0', 'accessToken': 'a0'});

      final status = await LicenseService(api).importLicense(licenseText);

      expect(status.state, 'valid');
      expect(seen.method, 'PUT');
      expect(seen.url.path, '/api/v1/license');
      expect(seen.headers['Content-Type'], startsWith('application/json'));
      expect(seen.headers.containsKey('Authorization'), isFalse);
      expect(seen.body, licenseText);
    });

    final failures = <String, (http.Response, String)>{
      '400': (_json(400, _problem(400)), 'not a valid'),
      '401': (_json(401, {}), 'only available on the computer'),
      '409': (_json(409, _problem(409)), 'newer license'),
      '413': (_json(413, _problem(413)), 'too large'),
      '415': (_json(415, _problem(415)), 'as JSON'),
      '422 expired': (_json(422, _problem(422, reason: 'expired')), 'expired'),
      '422 invalid_signature': (
        _json(422, _problem(422, reason: 'invalid_signature')),
        'not authentic',
      ),
      '429': (http.Response('', 429), 'Too many attempts'),
      '503': (_json(503, _problem(503)), 'cannot verify or store'),
    };

    failures.forEach((name, entry) {
      test('$name gives a safe message that never echoes the file', () async {
        final service = build(MockClient((request) async => entry.$1));

        await expectLater(
          service.importLicense(licenseText),
          throwsA(
            isA<LicenseImportException>()
                .having((e) => e.message, 'message', contains(entry.$2))
                .having(
                  (e) => e.message,
                  'message',
                  isNot(contains('Test Customer')),
                ),
          ),
        );
      });
    });

    test('a network failure is reported as a network error', () async {
      final service = build(
        MockClient((request) async {
          throw http.ClientException('down');
        }),
      );

      await expectLater(
        service.importLicense(licenseText),
        throwsA(
          isA<LicenseImportException>().having(
            (e) => e.isNetworkError,
            'isNetworkError',
            isTrue,
          ),
        ),
      );
    });

    test('an unreadable 200 body is an error, not a success', () async {
      final service = build(
        MockClient((request) async => http.Response('nope', 200)),
      );

      await expectLater(
        service.importLicense(licenseText),
        throwsA(isA<LicenseImportException>()),
      );
    });
  });
}
