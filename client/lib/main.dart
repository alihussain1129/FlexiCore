import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/api_client.dart';
import 'core/config.dart';
import 'core/token_storage.dart';
import 'features/auth/auth_service.dart';
import 'features/auth/session_controller.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final storage = SecureTokenStorage();
  late final SessionController session;
  final api = ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    storage: storage,
    onSessionExpired: () => session.markSignedOut(),
  );
  session = SessionController(
    auth: AuthService(api),
    api: api,
    storage: storage,
  )..start();

  runApp(
    MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: api),
        ChangeNotifierProvider<SessionController>.value(value: session),
      ],
      child: const FlexiCoreApp(),
    ),
  );
}