import 'package:flutter/material.dart';

import 'device_session_controller.dart';
import 'paired_screen.dart';
import 'pairing_screen.dart';

/// Chooses the screen from the device session state and starts the session
/// check once when it first appears.
class DeviceGate extends StatefulWidget {
  const DeviceGate({super.key, required this.controller});

  final DeviceSessionController controller;

  @override
  State<DeviceGate> createState() => _DeviceGateState();
}

class _DeviceGateState extends State<DeviceGate> {
  String? _startError;

  @override
  void initState() {
    super.initState();
    _start();
  }

  Future<void> _start() async {
    if (_startError != null) setState(() => _startError = null);
    try {
      await widget.controller.start();
    } catch (_) {
      // The local secure store could not be read. The credential is kept.
      if (mounted) {
        setState(
          () =>
              _startError = 'Could not read the saved pairing on this device.',
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = widget.controller;
    return ListenableBuilder(
      listenable: controller,
      builder: (context, _) {
        if (_startError != null) {
          return _UnreachableView(
            title: 'Pairing unavailable',
            message: _startError!,
            onRetry: _start,
            onForget: null,
          );
        }
        switch (controller.status) {
          case DeviceStatus.starting:
            return const Scaffold(
              body: Center(child: CircularProgressIndicator()),
            );
          case DeviceStatus.unpaired:
            return PairingScreen(controller: controller);
          case DeviceStatus.paired:
            return PairedScreen(controller: controller);
          case DeviceStatus.unreachable:
            return _UnreachableView(
              title: 'Server unreachable',
              message:
                  controller.notice ??
                  'The server could not be reached. Your pairing is kept.',
              onRetry: _start,
              onForget: () => confirmAndForget(context, controller),
            );
        }
      },
    );
  }
}

class _UnreachableView extends StatelessWidget {
  const _UnreachableView({
    required this.title,
    required this.message,
    required this.onRetry,
    required this.onForget,
  });

  final String title;
  final String message;
  final Future<void> Function() onRetry;
  final Future<void> Function()? onForget;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('FlexiCore device')),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Icon(Icons.cloud_off, size: 56),
                  const SizedBox(height: 16),
                  Text(
                    title,
                    style: Theme.of(context).textTheme.headlineSmall,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  Text(message, textAlign: TextAlign.center),
                  const SizedBox(height: 24),
                  FilledButton(onPressed: onRetry, child: const Text('Retry')),
                  if (onForget != null) ...[
                    const SizedBox(height: 8),
                    TextButton(
                      onPressed: onForget,
                      child: const Text('Forget this device'),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
