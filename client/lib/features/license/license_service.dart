import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../core/api_client.dart';
import '../../core/api_exception.dart';

/// What the app should do next, decided only from the server's license status.
enum LicenseGate {
  /// No license step is needed, or the license is valid.
  proceed,

  /// Show the activation screen.
  needsLicense,

  /// The license has expired. Sign-in and reading continue; the server blocks changes.
  readOnly,

  /// The server cannot decide (for example this build has no trusted keys).
  /// Fail closed: show a notice and offer a retry.
  unavailable,
}

class LicenseInfo {
  const LicenseInfo({
    required this.licenseId,
    required this.licensee,
    this.expiresAt,
    this.graceEndsAt,
  });

  factory LicenseInfo.fromJson(Map<String, dynamic> json) => LicenseInfo(
    licenseId: json['licenseId'] as String,
    licensee: json['licensee'] as String,
    expiresAt: _date(json['expiresAt']),
    graceEndsAt: _date(json['graceEndsAt']),
  );

  final String licenseId;
  final String licensee;
  final DateTime? expiresAt;
  final DateTime? graceEndsAt;

  static DateTime? _date(Object? value) =>
      value is String ? DateTime.tryParse(value) : null;
}

class LicenseStatus {
  const LicenseStatus({
    required this.mode,
    required this.licenseRequired,
    required this.state,
    this.reason,
    this.license,
  });

  factory LicenseStatus.fromJson(Map<String, dynamic> json) {
    final license = json['license'];
    return LicenseStatus(
      mode: json['mode'] as String,
      licenseRequired: json['licenseRequired'] as bool,
      state: json['state'] as String,
      reason: json['reason'] as String?,
      license: license is Map<String, dynamic>
          ? LicenseInfo.fromJson(license)
          : null,
    );
  }

  final String mode;
  final bool licenseRequired;
  final String state;
  final String? reason;
  final LicenseInfo? license;

  LicenseGate get gate {
    if (mode != 'local' || !licenseRequired) return LicenseGate.proceed;
    switch (state) {
      case 'valid':
      case 'validInGrace':
        return LicenseGate.proceed;
      case 'missing':
      case 'invalid':
      case 'notYetValid':
        return LicenseGate.needsLicense;
      case 'expired':
        return LicenseGate.readOnly;
      default:
        // 'unavailable' or a state this client does not know: never let it through.
        return LicenseGate.unavailable;
    }
  }
}

/// A failed import, with text that is safe to show. It never contains any part
/// of the submitted file.
class LicenseImportException implements Exception {
  LicenseImportException(this.message, {required this.statusCode});

  final String message;
  final int statusCode;

  bool get isNetworkError => statusCode == 0;

  @override
  String toString() => 'LicenseImportException($statusCode)';
}

class LicenseService {
  LicenseService(this._api);

  final ApiClient _api;

  static const statusPath = '/api/v1/license/status';
  static const importPath = '/api/v1/license';
  static const requestPath = '/api/v1/license/request';

  /// Returns null on 401: the server does not offer anonymous license access to
  /// this caller (Hosted mode, or not the machine running the server).
  /// Throws [ApiException] for network failures and any other error.
  Future<LicenseStatus?> fetchStatus() async {
    final response = await _api.getPublic(statusPath);
    if (response.statusCode == 401) return null;
    if (response.statusCode != 200) throw ApiException.fromResponse(response);
    return _parse(response);
  }

  /// This computer's activation request code (FCR1-...), or null when the
  /// server cannot provide one. Never throws.
  Future<String?> fetchRequestCode() async {
    try {
      final response = await _api.getPublic(requestPath);
      if (response.statusCode != 200) return null;
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        final code = decoded['requestCode'];
        if (code is String && code.startsWith('FCR1-')) return code;
      }
    } catch (_) {
      // Unavailable: the screen explains it.
    }
    return null;
  }

  /// Sends the license text or activation code exactly as given. Throws [LicenseImportException].
  Future<LicenseStatus> importLicense(String text) async {
    final http.Response response;
    try {
      response = await _api.putJsonTextPublic(importPath, text);
    } on ApiException catch (e) {
      throw LicenseImportException(e.message, statusCode: e.statusCode);
    }

    if (response.statusCode == 200) {
      try {
        return _parse(response);
      } on ApiException catch (e) {
        throw LicenseImportException(e.message, statusCode: e.statusCode);
      }
    }
    throw LicenseImportException(
      _messageFor(response.statusCode, _reasonOf(response)),
      statusCode: response.statusCode,
    );
  }

  static LicenseStatus _parse(http.Response response) {
    try {
      return LicenseStatus.fromJson(
        jsonDecode(response.body) as Map<String, dynamic>,
      );
    } catch (_) {
      throw ApiException(
        statusCode: response.statusCode,
        title: 'Unexpected response from the server',
      );
    }
  }

  static String? _reasonOf(http.Response response) {
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) return decoded['reason'] as String?;
    } catch (_) {
      // Not JSON: no reason.
    }
    return null;
  }

  static String _messageFor(int status, String? reason) {
    switch (status) {
      case 400:
        return 'This file is not a valid FlexiCore license.';
      case 401:
        return 'Activation is only available on the computer that runs FlexiCore.';
      case 409:
        return 'A newer license is already installed. This file was not applied.';
      case 413:
        return 'This file is too large to be a license.';
      case 415:
        return 'The license could not be sent as JSON.';
      case 422:
        if (reason == 'expired') {
          return 'This license has expired. Ask for a new license file.';
        }
        if (reason == 'wrong_installation') {
          return "This license was issued for a different computer. Send this computer's request code to the vendor to get a new one.";
        }
        if (reason == 'not_bound') {
          return 'This license is not tied to this computer. Ask the vendor for a license made from your request code.';
        }
        if (reason == 'invalid_signature') {
          return 'This license is not authentic and was rejected.';
        }
        return reason == null
            ? 'This license was rejected.'
            : 'This license was rejected ($reason).';
      case 429:
        return 'Too many attempts. Wait a minute and try again.';
      case 503:
        return 'The server cannot verify or store licenses right now.';
      default:
        return 'The license could not be activated (error $status).';
    }
  }
}
