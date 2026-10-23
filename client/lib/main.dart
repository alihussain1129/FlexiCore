import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/api_client.dart';
import 'core/app_theme.dart';
import 'core/appearance_controller.dart';
import 'core/config.dart';
import 'core/token_storage.dart';
import 'features/license/license_controller.dart';
import 'features/license/license_screen.dart';
import 'features/license/license_service.dart';
import 'local_workspace.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();

  final storage = SecureTokenStorage();
  final appearance = AppearanceController()..load();

  late final LicenseController license;
  final api = ApiClient(
    baseUrl: AppConfig.apiBaseUrl,
    storage: storage,
    onLicenseBlocked: () => license.refreshStatus(),
  );

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
        ChangeNotifierProvider<AppearanceController>.value(
          value: appearance,
        ),
        ChangeNotifierProvider<LicenseController>.value(value: license),
      ],
      child: const FlexiCoreApp(),
    ),
  );
}

class FlexiCoreApp extends StatelessWidget {
  const FlexiCoreApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FlexiCore',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home: const LicenseGate(),
    );
  }
}

/// Shows the activation screen until the license state is `ready`,
/// and only then builds the workspace.
class LicenseGate extends StatelessWidget {
  const LicenseGate({
    super.key,
    this.workspaceBuilder = _defaultWorkspace,
  });

  final WidgetBuilder workspaceBuilder;

  static Widget _defaultWorkspace(BuildContext _) => const LocalWorkspaceApp();

  @override
  Widget build(BuildContext context) {
    final license = context.watch<LicenseController>();

    if (license.state != LicenseUiState.ready) {
      return const LicenseScreen();
    }

    return workspaceBuilder(context);
  }
}