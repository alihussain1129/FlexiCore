import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:flexicore_client/core/device_api_client.dart';
import 'package:flexicore_client/core/device_storage.dart';
import 'package:flexicore_client/features/device/device_gate.dart';
import 'package:flexicore_client/features/device/device_session_controller.dart';

const _server = 'http://localhost:5265';
const _deviceId = '0b9e0d6e-7a4c-4f3f-9d3f-0a1b2c3d4e5f';
const _credential =
    '0b9e0d6e7a4c4f3f9d3f0a1b2c3d4e5f.TEST_ONLY_SECRET_0123456789';

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

Future<(DeviceSessionController, InMemoryDeviceStorage)> _pumpGate(
  WidgetTester tester,
  Future<http.Response> Function(http.Request) handler, {
  StoredDevice? stored,
}) async {
  final storage = InMemoryDeviceStorage();
  if (stored != null) await storage.write(stored);
  final controller = DeviceSessionController(
    api: DeviceApiClient(httpClient: MockClient(handler)),
    storage: storage,
  );
  await tester.pumpWidget(
    MaterialApp(home: DeviceGate(controller: controller)),
  );
  await tester.pumpAndSettle();
  return (controller, storage);
}

void main() {
  testWidgets('with nothing stored the pairing screen is shown', (
    tester,
  ) async {
    await _pumpGate(tester, (_) async => http.Response('', 500));

    expect(find.text('Pair this device'), findsOneWidget);
    expect(find.byKey(const Key('pairing-submit')), findsOneWidget);
  });

  testWidgets(
    'a stored credential the server accepts shows the paired screen',
    (tester) async {
      await _pumpGate(
        tester,
        (_) async => _json(200, {
          'deviceId': _deviceId,
          'name': 'Front desk',
          'isOwner': true,
        }),
        stored: _stored,
      );

      expect(find.text('This device is paired'), findsOneWidget);
      expect(find.text('Front desk'), findsOneWidget);
      expect(find.text('Owner device'), findsOneWidget);
      expect(find.text('Forget this device'), findsOneWidget);
    },
  );

  testWidgets('a 401 returns to pairing and clears the credential', (
    tester,
  ) async {
    final (_, storage) = await _pumpGate(
      tester,
      (_) async => http.Response('', 401),
      stored: _stored,
    );

    expect(find.text('Pair this device'), findsOneWidget);
    expect(find.textContaining('no longer authorised'), findsOneWidget);
    expect(await storage.read(), isNull);
  });

  testWidgets('a network failure shows unreachable and keeps the credential', (
    tester,
  ) async {
    final (_, storage) = await _pumpGate(
      tester,
      (_) async => throw http.ClientException('down'),
      stored: _stored,
    );

    expect(find.text('Server unreachable'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
    expect(await storage.read(), isNotNull);
  });

  testWidgets('pairing with a valid code shows the paired screen', (
    tester,
  ) async {
    final (_, storage) = await _pumpGate(
      tester,
      (_) async => _json(200, {
        'deviceId': _deviceId,
        'isOwner': false,
        'credential': _credential,
      }),
    );

    await tester.enterText(
      find.byKey(const Key('pairing-code')),
      'ABCDE-12345',
    );
    await tester.enterText(find.byKey(const Key('pairing-name')), 'Tablet');
    await tester.ensureVisible(find.byKey(const Key('pairing-submit')));
    await tester.tap(find.byKey(const Key('pairing-submit')));
    await tester.pumpAndSettle();

    expect(find.text('This device is paired'), findsOneWidget);
    expect(find.text('Tablet'), findsOneWidget);
    expect(find.text('Standard device'), findsOneWidget);
    expect((await storage.read())?.credential, _credential);
  });

  testWidgets('a rejected code shows the server message and stores nothing', (
    tester,
  ) async {
    final (_, storage) = await _pumpGate(
      tester,
      (_) async => _json(400, {
        'type': 'urn:flexicore:problem:invalid-pairing-code',
        'title': 'The pairing code is invalid or has expired',
        'status': 400,
      }),
    );

    await tester.enterText(find.byKey(const Key('pairing-code')), 'WRONG');
    await tester.enterText(find.byKey(const Key('pairing-name')), 'Tablet');
    await tester.ensureVisible(find.byKey(const Key('pairing-submit')));
    await tester.tap(find.byKey(const Key('pairing-submit')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('pairing-error')), findsOneWidget);
    expect(
      find.text('The pairing code is invalid or has expired'),
      findsOneWidget,
    );
    expect(find.text('Pair this device'), findsOneWidget);
    expect(await storage.read(), isNull);
  });
}
