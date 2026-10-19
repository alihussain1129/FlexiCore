import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:flexicore_client/core/api_exception.dart';
import 'package:flexicore_client/core/device_api_client.dart';
import 'package:flexicore_client/core/device_storage.dart';
import 'package:flexicore_client/features/device/device_session_controller.dart';

const _server = 'http://localhost:5265';
const _deviceId = '0b9e0d6e-7a4c-4f3f-9d3f-0a1b2c3d4e5f';
const _credential = '0b9e0d6e7a4c4f3f9d3f0a1b2c3d4e5f.TEST_ONLY_SECRET_0123456789';

const _stored = StoredDevice(
  serverUrl: _server,
  credential: _credential,
  deviceName: 'Front desk',
);

http.Response _json(int status, Object body) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

http.Response _meOk() => _json(200, {
  'deviceId': _deviceId,
  'name': 'Front desk',
  'isOwner': true,
});

http.Response _pairOk() => _json(200, {
  'deviceId': _deviceId,
  'isOwner': false,
  'credential': _credential,
});

class _Setup {
  _Setup(Future<http.Response> Function(http.Request) handler) {
    controller = DeviceSessionController(
      api: DeviceApiClient(
        httpClient: MockClient((request) {
          calls++;
          return handler(request);
        }),
      ),
      storage: storage,
    );
  }

  final storage = InMemoryDeviceStorage();
  int calls = 0;
  late final DeviceSessionController controller;
}

void main() {
  group('start', () {
    test('with nothing stored it is unpaired and makes no request', () async {
      final s = _Setup((_) async => _meOk());

      await s.controller.start();

      expect(s.controller.status, DeviceStatus.unpaired);
      expect(s.calls, 0);
    });

    test('a stored credential the server accepts is paired', () async {
      final s = _Setup((_) async => _meOk());
      await s.storage.write(_stored);

      await s.controller.start();

      expect(s.controller.status, DeviceStatus.paired);
      expect(s.controller.device?.name, 'Front desk');
      expect(s.controller.device?.isOwner, isTrue);
    });

    test('a 401 clears the credential and returns to pairing', () async {
      final s = _Setup((_) async => http.Response('', 401));
      await s.storage.write(_stored);

      await s.controller.start();

      expect(s.controller.status, DeviceStatus.unpaired);
      expect(s.controller.notice, contains('no longer authorised'));
      expect(await s.storage.read(), isNull);
    });

    test('a network failure keeps the credential and reports unreachable', () async {
      final s = _Setup((_) async => throw http.ClientException('down'));
      await s.storage.write(_stored);

      await s.controller.start();

      expect(s.controller.status, DeviceStatus.unreachable);
      expect(s.controller.notice, isNotNull);
      expect(await s.storage.read(), isNotNull);
    });

    test('a server error keeps the credential and reports unreachable', () async {
      final s = _Setup((_) async => http.Response('', 500));
      await s.storage.write(_stored);

      await s.controller.start();

      expect(s.controller.status, DeviceStatus.unreachable);
      expect(await s.storage.read(), isNotNull);
    });

    test('retrying after an outage reaches the paired state', () async {
      var up = false;
      final s = _Setup((_) async {
        if (!up) throw http.ClientException('down');
        return _meOk();
      });
      await s.storage.write(_stored);

      await s.controller.start();
      expect(s.controller.status, DeviceStatus.unreachable);

      up = true;
      await s.controller.start();
      expect(s.controller.status, DeviceStatus.paired);
      expect(s.controller.notice, isNull);
    });
  });

  group('pair', () {
    test('success stores the credential, server and name', () async {
      final s = _Setup((_) async => _pairOk());
      await s.controller.start();

      await s.controller.pair(
        serverUrl: ' $_server/ ',
        code: ' ABCDE-12345 ',
        deviceName: ' Tablet ',
      );

      expect(s.controller.status, DeviceStatus.paired);
      expect(s.controller.device?.name, 'Tablet');
      final saved = await s.storage.read();
      expect(saved?.serverUrl, _server);
      expect(saved?.credential, _credential);
      expect(saved?.deviceName, 'Tablet');
    });

    test('a rejected code throws and stores nothing', () async {
      final s = _Setup(
        (_) async => _json(400, {
          'type': 'urn:flexicore:problem:invalid-pairing-code',
          'title': 'The pairing code is invalid or has expired',
          'status': 400,
        }),
      );
      await s.controller.start();

      await expectLater(
        s.controller.pair(serverUrl: _server, code: 'X', deviceName: 'Tablet'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            'The pairing code is invalid or has expired',
          ),
        ),
      );

      expect(s.controller.status, DeviceStatus.unpaired);
      expect(await s.storage.read(), isNull);
    });

    test('a missing license gives the activation message', () async {
      final s = _Setup(
        (_) async => _json(403, {
          'type': 'urn:flexicore:problem:license-required',
          'title': 'A valid license is required',
          'status': 403,
        }),
      );
      await s.controller.start();

      await expectLater(
        s.controller.pair(serverUrl: _server, code: 'X', deviceName: 'Tablet'),
        throwsA(
          isA<ApiException>().having(
            (e) => e.message,
            'message',
            contains('not activated'),
          ),
        ),
      );
      expect(await s.storage.read(), isNull);
    });

    test('bad input is rejected locally with no request', () async {
      final s = _Setup((_) async => _pairOk());
      await s.controller.start();

      final cases = <Map<String, String>>[
        {'url': 'localhost:5265', 'code': 'ABCDE-12345', 'name': 'Tablet'},
        {'url': _server, 'code': '   ', 'name': 'Tablet'},
        {'url': _server, 'code': 'ABCDE-12345', 'name': '   '},
        {'url': _server, 'code': 'ABCDE-12345', 'name': 'x' * 81},
      ];
      for (final c in cases) {
        await expectLater(
          s.controller.pair(
            serverUrl: c['url']!,
            code: c['code']!,
            deviceName: c['name']!,
          ),
          throwsA(
            isA<ApiException>().having((e) => e.statusCode, 'statusCode', 400),
          ),
          reason: '$c',
        );
      }

      expect(s.calls, 0);
      expect(await s.storage.read(), isNull);
    });
  });

  group('forget', () {
    test('clears the stored device and returns to pairing', () async {
      final s = _Setup((_) async => _meOk());
      await s.storage.write(_stored);
      await s.controller.start();
      expect(s.controller.status, DeviceStatus.paired);

      await s.controller.forget();

      expect(s.controller.status, DeviceStatus.unpaired);
      expect(s.controller.device, isNull);
      expect(await s.storage.read(), isNull);
    });
  });

  group('describeDeviceError', () {
    test('rate limiting reports the wait', () {
      final text = describeDeviceError(
        ApiException(
          statusCode: 429,
          title: 'Too Many Requests',
          retryAfter: const Duration(seconds: 30),
        ),
      );
      expect(text, contains('30 seconds'));
    });
  });
}