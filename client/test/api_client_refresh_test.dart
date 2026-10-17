import 'dart:async';
import 'dart:convert';

import 'package:flexicore_client/core/api_client.dart';
import 'package:flexicore_client/core/token_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

http.Response _json(int status, Object body) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

Map<String, dynamic> _tokens(String access, String refresh) => {
  'accessToken': access,
  'tokenType': 'Bearer',
  'expiresIn': 900,
  'refreshToken': refresh,
};

void main() {
  late InMemoryTokenStorage storage;
  late int refreshCalls;
  late List<String> refreshTokensSeen;
  late bool expired;

  ApiClient build(MockClient mock) {
    return ApiClient(
      baseUrl: 'http://test',
      storage: storage,
      httpClient: mock,
      onSessionExpired: () => expired = true,
    );
  }

  setUp(() {
    storage = InMemoryTokenStorage();
    refreshCalls = 0;
    refreshTokensSeen = [];
    expired = false;
  });

  test('two concurrent 401s cause exactly one refresh request', () async {
    await storage.writeRefreshToken('r0');
    final gate = Completer<void>();

    final mock = MockClient((request) async {
      if (request.url.path == '/api/v1/auth/refresh') {
        refreshCalls++;
        refreshTokensSeen.add(
          jsonDecode(request.body)['refreshToken'] as String,
        );
        await gate.future; // hold the refresh open so both callers overlap
        return _json(200, _tokens('a1', 'r1'));
      }
      final auth = request.headers['Authorization'];
      return auth == 'Bearer a1' ? _json(200, {'ok': true}) : _json(401, {});
    });
    final client = build(mock);

    final first = client.send('GET', '/api/v1/auth/me');
    final second = client.send('GET', '/api/v1/auth/me');
    await Future<void>.delayed(const Duration(milliseconds: 50));
    gate.complete();
    final results = await Future.wait([first, second]);

    expect(results.map((r) => r.statusCode), [200, 200]);
    expect(refreshCalls, 1);
    expect(refreshTokensSeen, ['r0']);
    expect(await storage.readRefreshToken(), 'r1');
  });

  test('the new refresh token is saved before the retry is sent', () async {
    await storage.writeRefreshToken('r0');
    String? storedWhenRetried;

    final mock = MockClient((request) async {
      if (request.url.path == '/api/v1/auth/refresh') {
        return _json(200, _tokens('a1', 'r1'));
      }
      if (request.headers['Authorization'] == 'Bearer a1') {
        storedWhenRetried = await storage.readRefreshToken();
        return _json(200, {'ok': true});
      }
      return _json(401, {});
    });

    final response = await build(mock).send('GET', '/api/v1/auth/me');

    expect(response.statusCode, 200);
    expect(storedWhenRetried, 'r1');
  });

  test('a rejected refresh clears the session and reports expiry', () async {
    await storage.writeRefreshToken('r0');

    final mock = MockClient((request) async {
      if (request.url.path == '/api/v1/auth/refresh') return _json(401, {});
      return _json(401, {});
    });

    final response = await build(mock).send('GET', '/api/v1/auth/me');

    expect(response.statusCode, 401);
    expect(expired, isTrue);
    expect(await storage.readRefreshToken(), isNull);
  });

  test('a 429 on refresh keeps the stored token', () async {
    await storage.writeRefreshToken('r0');

    final mock = MockClient((request) async {
      if (request.url.path == '/api/v1/auth/refresh') {
        return http.Response('', 429, headers: {'retry-after': '30'});
      }
      return _json(401, {});
    });

    await build(mock).send('GET', '/api/v1/auth/me');

    expect(await storage.readRefreshToken(), 'r0');
  });

  test('a successful request never triggers a refresh', () async {
    await storage.writeRefreshToken('r0');

    final mock = MockClient((request) async {
      if (request.url.path == '/api/v1/auth/refresh') refreshCalls++;
      return _json(200, {'ok': true});
    });

    await build(mock).send('GET', '/api/v1/auth/me');

    expect(refreshCalls, 0);
  });
}
