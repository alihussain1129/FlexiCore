import 'dart:convert';

import 'package:http/http.dart' as http;

/// An error from the API (problem+json) or from the network (statusCode 0).
class ApiException implements Exception {
  ApiException({
    required this.statusCode,
    required this.title,
    this.detail,
    this.type,
    this.fieldErrors = const {},
    this.retryAfter,
  });

  /// No HTTP response at all: server down, DNS, timeout.
  factory ApiException.network(Object error) => ApiException(
    statusCode: 0,
    title: 'Cannot reach the server',
    detail: error.toString(),
  );

  /// Builds an exception from a non-success response. Never throws.
  factory ApiException.fromResponse(http.Response response) {
    String? title;
    String? detail;
    String? type;
    final fieldErrors = <String, List<String>>{};

    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) {
        title = decoded['title'] as String?;
        detail = decoded['detail'] as String?;
        type = decoded['type'] as String?;
        final errors = decoded['errors'];
        if (errors is Map<String, dynamic>) {
          errors.forEach((field, messages) {
            if (messages is List) {
              fieldErrors[field] = messages.map((m) => m.toString()).toList();
            }
          });
        }
      }
    } catch (_) {
      // Body was empty or not JSON; fall back to the status code below.
    }

    Duration? retryAfter;
    final header = response.headers['retry-after'];
    final seconds = header == null ? null : int.tryParse(header.trim());
    if (seconds != null) retryAfter = Duration(seconds: seconds);

    return ApiException(
      statusCode: response.statusCode,
      title: title ?? 'Request failed (${response.statusCode})',
      detail: detail,
      type: type,
      fieldErrors: fieldErrors,
      retryAfter: retryAfter,
    );
  }

  final int statusCode;
  final String title;
  final String? detail;
  final String? type;
  final Map<String, List<String>> fieldErrors;
  final Duration? retryAfter;

  bool get isNetworkError => statusCode == 0;
  bool get isRateLimited => statusCode == 429;
  bool get isUnauthorized => statusCode == 401;

  /// Text safe to show a person.
  String get message {
    if (isNetworkError) {
      return 'Cannot reach the server. Check your connection and try again.';
    }
    if (isRateLimited) {
      final wait = retryAfter?.inSeconds;
      return wait == null
          ? 'Too many requests. Please wait a moment and try again.'
          : 'Too many requests. Please try again in $wait seconds.';
    }
    if (fieldErrors.isNotEmpty) {
      return fieldErrors.values.expand((m) => m).join('\n');
    }
    return detail ?? title;
  }

  @override
  String toString() => 'ApiException($statusCode, $title)';
}
