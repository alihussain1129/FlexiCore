import 'package:flutter/material.dart';

import 'device_session_controller.dart';

/// Asks first, then forgets the pairing on this device only. Revoking the
/// device on the server is done by an owner device.
Future<void> confirmAndForget(
  BuildContext context,
  DeviceSessionController controller,
) async {
  final confirmed = await showDialog<bool>(
    context: context,
    builder: (dialogContext) => AlertDialog(
      title: const Text('Forget this device?'),
      content: const Text(
        'This removes the pairing from this phone only. You will need a new '
        'pairing code to connect it again.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(dialogContext).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed: () => Navigator.of(dialogContext).pop(true),
          child: const Text('Forget'),
        ),
      ],
    ),
  );
  if (confirmed == true) await controller.forget();
}

class PairedScreen extends StatelessWidget {
  const PairedScreen({super.key, required this.controller});

  final DeviceSessionController controller;

  @override
  Widget build(BuildContext context) {
    final device = controller.device;
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
                  const Icon(Icons.verified_user_outlined, size: 56),
                  const SizedBox(height: 16),
                  Text(
                    'This device is paired',
                    style: Theme.of(context).textTheme.headlineSmall,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 24),
                  Card(
                    child: Column(
                      children: [
                        ListTile(
                          leading: const Icon(Icons.smartphone),
                          title: Text(device?.name ?? ''),
                          subtitle: const Text('Device name'),
                        ),
                        ListTile(
                          leading: const Icon(Icons.admin_panel_settings),
                          title: Text(
                            (device?.isOwner ?? false)
                                ? 'Owner device'
                                : 'Standard device',
                          ),
                          subtitle: const Text('Device role'),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'The server accepted this device. Opening the business '
                    'workspace from this device is not available yet.',
                    style: Theme.of(context).textTheme.bodyMedium,
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 24),
                  OutlinedButton(
                    onPressed: () => confirmAndForget(context, controller),
                    child: const Text('Forget this device'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
