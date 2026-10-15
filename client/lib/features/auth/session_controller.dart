import 'package:flutter/foundation.dart';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';
import '../../core/token_storage.dart';
import 'auth_service.dart';

enum SessionStatus { starting, signedOut, signedIn }

class SessionController extends ChangeNotifier {
  SessionController({
    required AuthService auth,
    required ApiClient api,
    required TokenStorage storage,
  })  : _auth = auth,
        _api = api,
        _storage = storage;

  final AuthService _auth;
  final ApiClient _api;
  final TokenStorage _storage;

  SessionStatus status = SessionStatus.starting;
  UserProfile? user;

  /// App start: try to resume from the stored refresh token.
  Future<void> start() async {
    if (await _api.restoreSession()) {
      try {
        user = await _auth.me();
        status = SessionStatus.signedIn;
        notifyListeners();
        return;
      } on ApiException {
        // Fall through to signed out.
      }
    }
    status = SessionStatus.signedOut;
    notifyListeners();
  }

  Future<void> login(String email, String password) async {
    await _auth.login(email, password);
    user = await _auth.me();
    status = SessionStatus.signedIn;
    notifyListeners();
  }

  Future<void> register(String email, String password) async {
    await _auth.register(email, password);
    await login(email, password);
  }

  Future<void> logout() async {
    final token = await _storage.readRefreshToken();
    await _auth.logout(token);
    markSignedOut();
  }

  /// Also called when the API reports the session can no longer be refreshed.
  void markSignedOut() {
    user = null;
    status = SessionStatus.signedOut;
    notifyListeners();
  }
}