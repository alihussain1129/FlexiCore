import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:flexicore_client/core/api_exception.dart';
import 'package:flexicore_client/core/device_api_client.dart';

const _server = 'http://localhost:5265';
const _deviceId = '0b9e0d6e-7a4c-4f3f-9d3f-0a1b2c3d4e5f';
const _secret = 'TEST_ONLY_SECRET_abcdefghijklmnopqrstuvwxyz0123';
const _credential = '0b9e0d6e7a4c4f3f9d3f0a1b2c3d4e5f.$_secret';

http.Response _json(int status, Object body) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

void main() {
  group('normalizeServerUrl', () {
    test('accepts http and https addresses and drops a trailing slash', () {
      expect(
        DeviceApiClient.normalizeServerUrl('http://localhost:5265'),
        'http://localhost:5265',
      );
      expect(
        DeviceApiClient.normalizeServerUrl('  http://localhost:5265/ '),
        'http://localhost:5265',
      );
      expect(
        DeviceApiClient.normalizeServerUrl('https://pc.local'),
        'https://pc.local',
      );
    });

    test('rejects anything else', () {
      final bad = <String?>[
        null,
        '',
        '   ',
        'localhost:5265',
        'ftp://host',
        'http://',
        'http://host/path',
        'http://host?x=1',
        'http://user:pw@host',
      ];
      for (final input in bad) {
        expect(
          DeviceApiClient.normalizeServerUrl(input),
          isNull,
          reason: '$input',
        );
      }
    });
  });

  group('pair', () {
    test('posts the code and name and returns the credential', () async {
      late http.Request seen;
      final client = DeviceApiClient(
        httpClient: MockClient((request) async {
          seen = request;
          return _json(200, {
            'deviceId': _deviceId,
            'isOwner': true,
            'credential': _credential,
          });
        }),
      );

      final result = await client.pair(
        serverUrl: _server,
        code: 'ABCDE-12345',
        deviceName: 'Front desk',
      );

      expect(seen.method, 'POST');
      expect(seen.url.toString(), '$_server/api/v1/devices/pair');
      expect(jsonDecode(seen.body), {
        'code': 'ABCDE-12345',
        'deviceName': 'Front desk',
      });
      expect(seen.headers.containsKey('Authorization'), isFalse);
      expect(result.deviceId, _deviceId);
      expect(result.isOwner, isTrue);
      expect(result.credential, _credential);
    });

    test('a rejected code throws the server message', () async {
      final client = DeviceApiClient(
        httpClient: MockClient(
          (_) async => _json(400, {
            'type': 'urn:flexicore:problem:invalid-pairing-code',
            'title': 'The pairing code is invalid or has expired',
            'status': 400,
          }),
        ),
      );

      await expectLater(
        client.pair(serverUrl: _server, code: 'X', deviceName: 'Phone'),
        throwsA(
          isA<ApiException>()
              .having((e) => e.statusCode, 'statusCode', 400)
              .having(
                (e) => e.message,
                'message',
                'The pairing code is invalid or has expired',
              ),
        ),
      );
    });

    test('keeps the problem type so a missing license can be recognised', () async {
      final client = DeviceApiClient(
        httpClient: MockClient(
          (_) async => _json(403, {
            'type': 'urn:flexicore:problem:license-required',
            'title': 'A valid license is required',
            'status': 403,
          }),
        ),
      );

      await expectLater(
        client.pair(serverUrl: _server, code: 'X', deviceName: 'Phone'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.type,
            'type',
            'urn:flexicore:problem:license-required',
          ),
        ),
      );
    });

    test('a network failure becomes a network ApiException', () async {
      final client = DeviceApiClient(
        httpClient: MockClient((_) async => throw http.ClientException('boom')),
      );

      await expectLater(
        client.pair(serverUrl: _server, code: 'X', deviceName: 'Phone'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.isNetworkError,
            'isNetworkError',
            isTrue,
          ),
        ),
      );
    });

    test('a 200 with an unusable body is an error, not a pairing', () async {
      for (final body in ['{}', 'not json', '[]']) {
        final client = DeviceApiClient(
          httpClient: MockClient((_) async => http.Response(body, 200)),
        );
        await expectLater(
          client.pair(serverUrl: _server, code: 'X', deviceName: 'Phone'),
          throwsA(
            isA<ApiException>().having(
              (e) => e.statusCode,
              'statusCode',
              500,
            ),
          ),
          reason: body,
        );
      }
    });
  });

  group('me', () {
    test('sends the device credential in the Authorization header', () async {
      late http.Request seen;
      final client = DeviceApiClient(
        httpClient: MockClient((request) async {
          seen = request;
          return _json(200, {
            'deviceId': _deviceId,
            'name': 'Front desk',
            'isOwner': false,
          });
        }),
      );

      final info = await client.me(serverUrl: _server, credential: _credential);

      expect(seen.method, 'GET');
      expect(seen.url.toString(), '$_server/api/v1/devices/me');
      expect(seen.headers['Authorization'], 'Device $_credential');
      expect(info.deviceId, _deviceId);
      expect(info.name, 'Front desk');
      expect(info.isOwner, isFalse);
    });

    test('a 401 is reported as unauthorized', () async {
      final client = DeviceApiClient(
        httpClient: MockClient((_) async => http.Response('', 401)),
      );

      await expectLater(
        client.me(serverUrl: _server, credential: _credential),
        throwsA(
          isA<ApiException>().having(
            (e) => e.isUnauthorized,
            'isUnauthorized',
            isTrue,
          ),
        ),
      );
    });
  });

  group('secrets', () {
    test('the credential is not in toString output', () async {
      const result = DevicePairResult(
        deviceId: _deviceId,
        isOwner: true,
        credential: _credential,
      );
      expect(result.toString(), isNot(contains(_secret)));

      final client = DeviceApiClient(
        httpClient: MockClient((_) async => http.Response('', 401)),
      );
      try {
        await client.me(serverUrl: _server, credential: _credential);
        fail('expected an ApiException');
      } on ApiException catch (e) {
        expect(e.toString(), isNot(contains(_secret)));
        expect(e.message, isNot(contains(_secret)));
      }
    });
  });
}