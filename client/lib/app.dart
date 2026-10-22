import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'core/appearance_controller.dart';
import 'features/auth/login_screen.dart';
import 'features/auth/local_setup_screen.dart';
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

/// Shows a renewal banner above the app when the license has expired (read-only mode).
class _ReadOnlyFrame extends StatelessWidget {
  const _ReadOnlyFrame({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    final license = context.watch<LicenseController>();
    if (!license.isReadOnly) return child;
    final theme = Theme.of(context);
    final expired = license.status?.license?.expiresAt;
    final when = expired == null
        ? ''
        : ' on ${expired.toUtc().toIso8601String().substring(0, 10)}';
    return Column(
      children: [
        Material(
          color: theme.colorScheme.errorContainer,
          child: SafeArea(
            bottom: false,
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
              child: Row(
                children: [
                  Icon(
                    Icons.lock_clock,
                    size: 20,
                    color: theme.colorScheme.onErrorContainer,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Your license expired$when. FlexiCore is read-only: you can view and export, but not change data.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onErrorContainer,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  FilledButton(
                    onPressed: license.openActivation,
                    child: const Text('Renew license'),
                  ),
                ],
              ),
            ),
          ),
        ),
        Expanded(
          child: MediaQuery.removePadding(
            context: context,
            removeTop: true,
            child: child,
          ),
        ),
      ],
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
        return const _ReadOnlyFrame(child: WorkspaceScreen());
    }
  }
}
