import 'dart:convert';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';

class UserProfile {
  UserProfile({required this.id, required this.email, this.username});

  factory UserProfile.fromJson(Map<String, dynamic> json) =>
      UserProfile(
        id: json['id'] as String,
        email: (json['email'] as String?) ?? (json['username'] as String?) ?? '',
        username: json['username'] as String?,
      );

  final String id;
  final String email;
  final String? username;

  /// What to show in the UI: the username for local accounts, else the email.
  String get displayName => username ?? email;
}

class AuthService {
  AuthService(this._api);

  final ApiClient _api;

  Future<void> register(String email, String password) async {
    final response = await _api.postPublic('/api/v1/auth/register', {
      'email': email,
      'password': password,
    });
    if (response.statusCode != 201) throw ApiException.fromResponse(response);
  }

  Future<void> login(String email, String password) async {
    final response = await _api.postPublic('/api/v1/auth/login', {
      'email': email,
      'password': password,
    });
    if (response.statusCode != 200) throw ApiException.fromResponse(response);
    await _api.acceptTokens(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<UserProfile> me() async {
    final response = await _api.send('GET', '/api/v1/auth/me');
    if (response.statusCode != 200) throw ApiException.fromResponse(response);
    return UserProfile.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  /// Revokes the session on the server if possible, then always clears locally.
  Future<void> logout(String? refreshToken) async {
    try {
      if (refreshToken != null) {
        await _api.postPublic('/api/v1/auth/logout', {
          'refreshToken': refreshToken,
        });
      }
    } on ApiException {
      // Offline logout still clears the device.
    } finally {
      await _api.clearSession();
    }
  }
}

