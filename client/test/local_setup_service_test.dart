import 'dart:convert';

import 'package:flexicore_client/core/api_client.dart';
import 'package:flexicore_client/core/token_storage.dart';
import 'package:flexicore_client/features/auth/local_setup_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('reads first-owner setup availability anonymously', () async {
    final service = LocalSetupService(
      ApiClient(
        baseUrl: 'http://test',
        storage: InMemoryTokenStorage(),
        httpClient: MockClient((request) async {
          expect(request.method, 'GET');
          expect(request.url.path, '/api/v1/local/setup');
          expect(request.headers.containsKey('authorization'), isFalse);
          return http.Response('{"setupRequired":true}', 200);
        }),
      ),
    );

    expect(await service.isSetupRequired(), isTrue);
  });

  test('creates the first local owner anonymously', () async {
    final service = LocalSetupService(
      ApiClient(
        baseUrl: 'http://test',
        storage: InMemoryTokenStorage(),
        httpClient: MockClient((request) async {
          expect(request.method, 'POST');
          expect(request.url.path, '/api/v1/local/setup');
          expect(request.headers.containsKey('authorization'), isFalse);
          expect(jsonDecode(request.body), {
            'username': 'owner',
            'password': 'test-password',
          });
          return http.Response(
            '{"id":"00000000-0000-0000-0000-000000000001","username":"owner"}',
            201,
          );
        }),
      ),
    );

    await expectLater(
      service.createFirstOwner(
        username: 'owner',
        password: 'test-password',
      ),
      completes,
    );
  });
}
