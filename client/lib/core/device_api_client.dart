import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_exception.dart';

/// The answer to a successful pairing. [credential] is the only copy of the
/// device secret the server will ever give out, so it is kept out of [toString].
class DevicePairResult {
  const DevicePairResult({
    required this.deviceId,
    required this.isOwner,
    required this.credential,
  });

  final String deviceId;
  final bool isOwner;
  final String credential;

  @override
  String toString() => 'DevicePairResult($deviceId, owner: $isOwner)';
}

/// What the server says about the calling device.
class DeviceInfo {
  const DeviceInfo({
    required this.deviceId,
    required this.name,
    required this.isOwner,
  });

  final String deviceId;
  final String name;
  final bool isOwner;
}

/// Talks to the device endpoints. A device authenticates with
/// `Authorization: Device <id>.<secret>`; there is no token to refresh, so a
/// 401 means the server no longer accepts this device.
class DeviceApiClient {
  DeviceApiClient({http.Client? httpClient})
    : _http = httpClient ?? http.Client();

  final http.Client _http;

  static const _timeout = Duration(seconds: 15);
  static const _jsonHeaders = {'Content-Type': 'application/json'};

  /// Returns "scheme://host[:port]", or null if [input] is not a plain http or
  /// https server address (no path, query, fragment or user info).
  static String? normalizeServerUrl(String? input) {
    final text = input?.trim() ?? '';
    if (text.isEmpty) return null;
    final uri = Uri.tryParse(text);
    if (uri == null || !uri.hasAuthority || uri.host.isEmpty) return null;
    if (uri.scheme != 'http' && uri.scheme != 'https') return null;
    if (uri.userInfo.isNotEmpty || uri.hasQuery || uri.fragment.isNotEmpty) {
      return null;
    }
    if (uri.path.isNotEmpty && uri.path != '/') return null;
    return '${uri.scheme}://${uri.authority}';
  }

  /// Exchanges a one-time pairing code for a device credential.
  /// [serverUrl] must already be normalized.
  Future<DevicePairResult> pair({
    required String serverUrl,
    required String code,
    required String deviceName,
  }) async {
    final response = await _send(
      () => _http
          .post(
            Uri.parse('$serverUrl/api/v1/devices/pair'),
            headers: _jsonHeaders,
            body: jsonEncode({'code': code, 'deviceName': deviceName}),
          )
          .timeout(_timeout),
    );
    if (response.statusCode != 200) throw ApiException.fromResponse(response);

    final body = _decodeObject(response);
    final deviceId = body['deviceId'];
    final isOwner = body['isOwner'];
    final credential = body['credential'];
    if (deviceId is! String || isOwner is! bool || credential is! String) {
      throw _unexpectedResponse();
    }
    return DevicePairResult(
      deviceId: deviceId,
      isOwner: isOwner,
      credential: credential,
    );
  }

  /// Asks the server who this device is. Throws [ApiException]; a 401 means
  /// the device was revoked or the credential is wrong.
  Future<DeviceInfo> me({
    required String serverUrl,
    required String credential,
  }) async {
    final response = await _send(
      () => _http
          .get(
            Uri.parse('$serverUrl/api/v1/devices/me'),
            headers: {'Authorization': 'Device $credential'},
          )
          .timeout(_timeout),
    );
    if (response.statusCode != 200) throw ApiException.fromResponse(response);

    final body = _decodeObject(response);
    final deviceId = body['deviceId'];
    final name = body['name'];
    final isOwner = body['isOwner'];
    if (deviceId is! String || name is! String || isOwner is! bool) {
      throw _unexpectedResponse();
    }
    return DeviceInfo(deviceId: deviceId, name: name, isOwner: isOwner);
  }

  Map<String, dynamic> _decodeObject(http.Response response) {
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) return decoded;
    } on FormatException {
      // Falls through to the error below.
    }
    throw _unexpectedResponse();
  }

  ApiException _unexpectedResponse() => ApiException(
    statusCode: 500,
    title: 'The server sent an unexpected response',
  );

  /// Wraps network failures (and timeouts) into [ApiException].
  Future<http.Response> _send(Future<http.Response> Function() call) async {
    try {
      return await call();
    } on http.ClientException catch (e) {
      throw ApiException.network(e);
    } on Exception catch (e) {
      throw ApiException.network(e);
    }
  }

  void close() => _http.close();
}
