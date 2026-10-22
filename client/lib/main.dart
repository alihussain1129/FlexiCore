import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'core/api_client.dart';
import 'core/appearance_controller.dart';
import 'core/config.dart';
import 'core/token_storage.dart';
import 'features/license/license_controller.dart';
import 'features/license/license_service.dart';
import 'features/auth/auth_service.dart';
import 'features/auth/session_controller.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final storage = SecureTokenStorage();
  final appearance = AppearanceController()..load();

  late final LicenseController license;
  late final SessionController session;
  final api = ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    storage: storage,
    onSessionExpired: () => session.markSignedOut(),
    onLicenseBlocked: () => license.refreshStatus(),
  );

  session = SessionController(
    auth: AuthService(api),
    api: api,
    storage: storage,
  )..start();

  final licenseService = LicenseService(api);
  license = LicenseController(
    fetchStatus: licenseService.fetchStatus,
    importLicense: licenseService.importLicense,
    fetchRequestCode: licenseService.fetchRequestCode,
  )..check();

  runApp(
    MultiProvider(
      providers: [
        Provider<ApiClient>.value(value: api),
        ChangeNotifierProvider<SessionController>.value(value: session),
        ChangeNotifierProvider<AppearanceController>.value(value: appearance),
        ChangeNotifierProvider<LicenseController>.value(value: license),
      ],
      child: const FlexiCoreApp(),
    ),
  );
}
