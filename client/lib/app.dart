import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'features/auth/login_screen.dart';
import 'features/auth/session_controller.dart';

class FlexiCoreApp extends StatelessWidget {
  const FlexiCoreApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FlexiCore',
      theme: ThemeData(colorSchemeSeed: const Color(0xFF1F6F5C), useMaterial3: true),
      home: const _Root(),
    );
  }
}

class _Root extends StatelessWidget {
  const _Root();

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    switch (session.status) {
      case SessionStatus.starting:
        return const Scaffold(body: Center(child: CircularProgressIndicator()));
      case SessionStatus.signedOut:
        return const LoginScreen();
      case SessionStatus.signedIn:
        return const _Home();
    }
  }
}

/// Placeholder until organizations arrive in the next commit.
class _Home extends StatelessWidget {
  const _Home();

  @override
  Widget build(BuildContext context) {
    final session = context.watch<SessionController>();
    return Scaffold(
      appBar: AppBar(
        title: const Text('FlexiCore'),
        actions: [
          TextButton(
            onPressed: () => context.read<SessionController>().logout(),
            child: const Text('Sign out'),
          ),
        ],
      ),
      body: Center(child: Text('Signed in as ${session.user?.email}')),
    );
  }
}