import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/appearance_controller.dart';
import 'core/flexicore_theme.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/local_setup_screen.dart';
import 'features/auth/session_controller.dart';
import 'features/license/license_controller.dart';
import 'features/license/license_screen.dart';
import 'features/workspace/workspace_screen.dart';

class FlexiCoreApp extends StatelessWidget {
  const FlexiCoreApp({super.key});

  @override
  Widget build(BuildContext context) {
    final appearance = context.watch<AppearanceController>();

    return MaterialApp(
      title: 'FlexiCore',
      debugShowCheckedModeBanner: false,
      themeMode: appearance.themeMode,
      theme: FlexiCoreTheme.light,
      darkTheme: FlexiCoreTheme.dark,
      themeAnimationDuration: FlexiCoreMotion.platformDuration(
        FlexiCoreMotion.navigation,
      ),
      themeAnimationCurve: FlexiCoreMotion.emphasizedCurve,
      home: const _Root(),
    );
  }
}

class _Root extends StatelessWidget {
  const _Root();

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();

    final license = context.watch<LicenseController>();
    switch (license.state) {
      case LicenseUiState.checking:
        return const Scaffold(body: Center(child: CircularProgressIndicator()));
      case LicenseUiState.needsLicense:
      case LicenseUiState.unavailable:
      case LicenseUiState.unreachable:
        return const LicenseScreen();
      case LicenseUiState.ready:
        break;
    }

    if (license.isLocalMode && session.status == SessionStatus.signedOut) {
      return const LocalSetupScreen();
    }

    switch (session.status) {
      case SessionStatus.starting:
        return const Scaffold(body: Center(child: CircularProgressIndicator()));
      case SessionStatus.signedOut:
        return const LoginScreen();
      case SessionStatus.signedIn:
        return const WorkspaceScreen();
    }
  }
}
