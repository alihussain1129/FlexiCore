import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import 'license_controller.dart';

class LicenseScreen extends StatefulWidget {
  const LicenseScreen({super.key});

  @override
  State<LicenseScreen> createState() => _LicenseScreenState();
}

class _LicenseScreenState extends State<LicenseScreen> {
  final _text = TextEditingController();

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  Future<void> _paste() async {
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final text = data?.text;
    if (text != null && mounted) {
      _text.text = text;
    }
  }

  @override
  Widget build(BuildContext context) {
    final controller = context.watch<LicenseController>();
    final theme = Theme.of(context);
    final blocked =
        controller.state == LicenseUiState.unavailable ||
        controller.state == LicenseUiState.unreachable;

    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 560),
            child: Card(
              elevation: 4,
              shadowColor: theme.colorScheme.shadow.withValues(alpha: 0.25),
              child: Padding(
                padding: const EdgeInsets.all(28),
                child: AnimatedSize(
                  duration: const Duration(milliseconds: 220),
                  curve: Curves.easeOut,
                  alignment: Alignment.topCenter,
                  child: blocked
                      ? _blocked(theme, controller)
                      : _activate(theme, controller),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _blocked(ThemeData theme, LicenseController controller) {
    final unreachable = controller.state == LicenseUiState.unreachable;
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          unreachable ? Icons.cloud_off_outlined : Icons.lock_outline,
          size: 48,
          color: theme.colorScheme.primary,
        ),
        const SizedBox(height: 16),
        Text(
          unreachable ? 'Cannot reach FlexiCore' : 'License check unavailable',
          style: theme.textTheme.headlineSmall,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 12),
        Text(
          controller.problem ?? 'Please try again.',
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 24),
        FilledButton.icon(
          onPressed: controller.check,
          icon: const Icon(Icons.refresh),
          label: const Text('Try again'),
        ),
      ],
    );
  }

  Widget _activate(ThemeData theme, LicenseController controller) {
    final hint = controller.hint;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          Icons.verified_outlined,
          size: 40,
          color: theme.colorScheme.primary,
        ),
        const SizedBox(height: 16),
        Text(
          'Activate FlexiCore',
          style: theme.textTheme.headlineSmall?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
        const SizedBox(height: 8),
        Text(
          'Open the license file you received in a text editor, copy everything, and paste it below.',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        if (hint != null) ...[
          const SizedBox(height: 16),
          DecoratedBox(
            decoration: BoxDecoration(
              color: theme.colorScheme.secondaryContainer,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline,
                    size: 20,
                    color: theme.colorScheme.onSecondaryContainer,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      hint,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSecondaryContainer,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
        const SizedBox(height: 20),
        TextField(
          controller: _text,
          enabled: !controller.busy,
          minLines: 6,
          maxLines: 12,
          keyboardType: TextInputType.multiline,
          style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
          decoration: InputDecoration(
            labelText: 'License file contents',
            alignLabelWithHint: true,
            errorText: controller.importError,
            errorMaxLines: 3,
          ),
        ),
        const SizedBox(height: 16),
        Row(
          children: [
            TextButton.icon(
              onPressed: controller.busy ? null : _paste,
              icon: const Icon(Icons.content_paste),
              label: const Text('Paste'),
            ),
            const Spacer(),
            FilledButton.icon(
              onPressed: controller.busy
                  ? null
                  : () => controller.submit(_text.text),
              icon: controller.busy
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.vpn_key_outlined),
              label: Text(controller.busy ? 'Checking...' : 'Activate'),
            ),
          ],
        ),
      ],
    );
  }
}
