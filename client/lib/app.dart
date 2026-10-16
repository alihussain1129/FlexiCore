import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/appearance_controller.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/session_controller.dart';
import 'features/license/license_controller.dart';
import 'features/license/license_screen.dart';
import 'features/workspace/workspace_screen.dart';

class FlexiCoreApp extends StatelessWidget {
  const FlexiCoreApp({super.key});

  static const _brandColor = Color(0xFF167D68);

  @override
  Widget build(BuildContext context) {
    final appearance = context.watch<AppearanceController>();

    return MaterialApp(
      title: 'FlexiCore',
      debugShowCheckedModeBanner: false,
      themeMode: appearance.themeMode,
      theme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: _brandColor,
          brightness: Brightness.light,
        ),
        scaffoldBackgroundColor: const Color(0xFFF6F8FA),
        cardTheme: const CardThemeData(
          elevation: 0,
          margin: EdgeInsets.zero,
          clipBehavior: Clip.antiAlias,
        ),
        inputDecorationTheme: const InputDecorationTheme(
          filled: true,
          border: OutlineInputBorder(),
        ),
        appBarTheme: const AppBarTheme(
          centerTitle: false,
          scrolledUnderElevation: 0,
        ),
      ),
      darkTheme: ThemeData(
        useMaterial3: true,
        colorScheme: ColorScheme.fromSeed(
          seedColor: _brandColor,
          brightness: Brightness.dark,
        ),
        cardTheme: const CardThemeData(
          elevation: 0,
          margin: EdgeInsets.zero,
          clipBehavior: Clip.antiAlias,
        ),
        inputDecorationTheme: const InputDecorationTheme(
          filled: true,
          border: OutlineInputBorder(),
        ),
        appBarTheme: const AppBarTheme(
          centerTitle: false,
          scrolledUnderElevation: 0,
        ),
      ),
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
