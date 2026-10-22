// ignore_for_file: prefer_initializing_formals

import 'dart:async';
import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_exception.dart';
import 'token_storage.dart';

/// Talks to the FlexiCore API. Holds the access token in memory only and
/// keeps the refresh token in [TokenStorage].
///
/// The server revokes a whole session if a refresh token is used twice, so at
/// most one refresh is ever in flight: concurrent callers share one future.
class ApiClient {
  ApiClient({
    required this.baseUrl,
    required TokenStorage storage,
    http.Client? httpClient,
    this.onSessionExpired,
    this.onLicenseBlocked,
  }) : _storage = storage,
       _http = httpClient ?? http.Client();

  final String baseUrl;
  final TokenStorage _storage;
  final http.Client _http;

  /// Called when a refresh is rejected, so the app can return to login.
  final void Function()? onSessionExpired;

  /// Called when the server refuses a request because of the license (HTTP 403, license-required).
  final void Function()? onLicenseBlocked;

  String? _accessToken;
  Future<bool>? _refreshing;

  bool get hasAccessToken => _accessToken != null;

  Uri _uri(String path) => Uri.parse('$baseUrl$path');

  static const _jsonHeaders = {'Content-Type': 'application/json'};

  /// Stores a login/refresh response. The refresh token is saved first.
  Future<void> acceptTokens(Map<String, dynamic> body) async {
    await _storage.writeRefreshToken(body['refreshToken'] as String);
    _accessToken = body['accessToken'] as String;
  }

  /// Drops all local credentials.
  Future<void> clearSession() async {
    _accessToken = null;
    await _storage.clear();
  }

  /// A request without authentication (register, login, logout).
  Future<http.Response> postPublic(String path, Map<String, dynamic> body) =>
      _send(
        () => _http.post(
          _uri(path),
          headers: _jsonHeaders,
          body: jsonEncode(body),
        ),
      );

  /// An anonymous GET with no token (license status before sign-in).
  Future<http.Response> getPublic(String path) =>
      _send(() => _http.get(_uri(path), headers: _jsonHeaders));

  /// An anonymous PUT of JSON text sent exactly as given, with no token and no
  /// re-encoding of the document (license import before sign-in).
  Future<http.Response> putJsonTextPublic(String path, String jsonText) =>
      _send(() => _http.put(_uri(path), headers: _jsonHeaders, body: jsonText));

  /// An authenticated request. On 401 it refreshes once and retries once.
  Future<http.Response> send(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    var response = await _sendAuthed(method, path, body);
    if (response.statusCode != 401) return _checkLicense(response);

    final refreshed = await _refreshOnce();
    if (!refreshed) {
      onSessionExpired?.call();
      return response;
    }
    return _checkLicense(await _sendAuthed(method, path, body));
  }

  http.Response _checkLicense(http.Response response) {
    if (response.statusCode == 403 &&
        response.body.contains('license-required')) {
      onLicenseBlocked?.call();
    }
    return response;
  }

  Future<http.Response> _sendAuthed(
    String method,
    String path,
    Map<String, dynamic>? body,
  ) {
    return _send(() {
      final request = http.Request(method, _uri(path));
      request.headers.addAll(_jsonHeaders);
      final token = _accessToken;
      if (token != null) request.headers['Authorization'] = 'Bearer $token';
      if (body != null) request.body = jsonEncode(body);
      return _http.send(request).then(http.Response.fromStream);
    });
  }

  /// Wraps network failures into [ApiException].
  Future<http.Response> _send(Future<http.Response> Function() call) async {
    try {
      return await call();
    } on http.ClientException catch (e) {
      throw ApiException.network(e);
    } on Exception catch (e) {
      throw ApiException.network(e);
    }
  }

  /// Tries to restore a session from the stored refresh token (app start).
  Future<bool> restoreSession() => _refreshOnce();

  Future<bool> _refreshOnce() {
    final inFlight = _refreshing;
    if (inFlight != null) return inFlight;
    final future = _doRefresh().whenComplete(() => _refreshing = null);
    _refreshing = future;
    return future;
  }

  Future<bool> _doRefresh() async {
    final stored = await _storage.readRefreshToken();
    if (stored == null) return false;

    final http.Response response;
    try {
      response = await _send(
        () => _http.post(
          _uri('/api/v1/auth/refresh'),
          headers: _jsonHeaders,
          body: jsonEncode({'refreshToken': stored}),
        ),
      );
    } on ApiException {
      // Server unreachable: keep the stored token, the user may retry later.
      return false;
    }

    if (response.statusCode == 200) {
      await acceptTokens(jsonDecode(response.body) as Map<String, dynamic>);
      return true;
    }
    if (response.statusCode == 401) {
      // Rejected for good (expired, revoked, or reused): forget it.
      await clearSession();
    }
    return false;
  }

  void close() => _http.close();
}
