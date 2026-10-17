import 'dart:io';

import 'package:file_selector/file_selector.dart';
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
  static const _maxFileChars = 20000;

  final _text = TextEditingController();
  String? _localMessage;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<LicenseController>().loadRequestCode();
    });
  }

  @override
  void dispose() {
    _text.dispose();
    super.dispose();
  }

  Future<void> _paste() async {
    final data = await Clipboard.getData(Clipboard.kTextPlain);
    final text = data?.text;
    if (text != null && mounted) {
      setState(() => _localMessage = null);
      _text.text = text;
    }
  }

  Future<void> _chooseFile() async {
    const group = XTypeGroup(
      label: 'FlexiCore license',
      extensions: ['lic', 'txt', 'json'],
    );
    final file = await openFile(acceptedTypeGroups: [group]);
    if (file == null || !mounted) return;
    final size = await file.length();
    if (size > _maxFileChars) {
      if (mounted) {
        setState(
          () => _localMessage = 'That file is too large to be a license.',
        );
      }
      return;
    }
    String text;
    try {
      text = await file.readAsString();
    } catch (_) {
      if (mounted) {
        setState(() => _localMessage = 'That file could not be read as text.');
      }
      return;
    }
    if (!mounted) return;
    setState(() => _localMessage = null);
    _text.text = text;
    await context.read<LicenseController>().submit(text);
  }

  Future<void> _copyRequest(String code) async {
    await Clipboard.setData(ClipboardData(text: code));
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(const SnackBar(content: Text('Request code copied.')));
  }

  Future<void> _saveRequest(String code) async {
    final location = await getSaveLocation(
      suggestedName: 'flexicore-request.txt',
      acceptedTypeGroups: [
        const XTypeGroup(label: 'Text', extensions: ['txt']),
      ],
    );
    if (location == null) return;
    String message;
    try {
      await File(location.path).writeAsString('$code\n');
      message = 'Request file saved.';
    } on IOException {
      message = 'The request file could not be saved.';
    }
    if (!mounted) return;
    ScaffoldMessenger.of(context)
        .showSnackBar(SnackBar(content: Text(message)));
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
            constraints: const BoxConstraints(maxWidth: 600),
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

  Widget _notice(ThemeData theme, String message) {
    return DecoratedBox(
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
                message,
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: theme.colorScheme.onSecondaryContainer,
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _requestCard(ThemeData theme, LicenseController controller) {
    final code = controller.requestCode;
    return DecoratedBox(
      decoration: BoxDecoration(
        color: theme.colorScheme.surfaceContainerHighest,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: theme.colorScheme.outlineVariant),
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Step 1: send this request code to the vendor',
              style: theme.textTheme.titleSmall?.copyWith(
                fontWeight: FontWeight.w600,
              ),
            ),
            const SizedBox(height: 8),
            if (code != null) ...[
              SelectableText(
                code,
                style: const TextStyle(fontFamily: 'monospace', fontSize: 12),
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  TextButton.icon(
                    onPressed: () => _copyRequest(code),
                    icon: const Icon(Icons.copy, size: 18),
                    label: const Text('Copy'),
                  ),
                  TextButton.icon(
                    onPressed: () => _saveRequest(code),
                    icon: const Icon(Icons.save_alt, size: 18),
                    label: const Text('Save as file'),
                  ),
                ],
              ),
            ] else if (!controller.requestLoaded)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 4),
                child: LinearProgressIndicator(),
              )
            else
              Text(
                'This computer\'s request code is not available right now. '
                'Make sure FlexiCore is running on this computer, then use Check again.',
                style: theme.textTheme.bodySmall,
              ),
          ],
        ),
      ),
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
          'FlexiCore works offline. The vendor issues a license for this '
          'computer from your request code. Then enter the activation code '
          'you receive, or choose the license file.',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        if (hint != null) ...[const SizedBox(height: 16), _notice(theme, hint)],
        const SizedBox(height: 20),
        _requestCard(theme, controller),
        const SizedBox(height: 20),
        Text(
          'Step 2: activate',
          style: theme.textTheme.titleSmall?.copyWith(
            fontWeight: FontWeight.w600,
          ),
        ),
        const SizedBox(height: 10),
        TextField(
          controller: _text,
          enabled: !controller.busy,
          minLines: 4,
          maxLines: 10,
          keyboardType: TextInputType.multiline,
          style: const TextStyle(fontFamily: 'monospace', fontSize: 13),
          decoration: InputDecoration(
            labelText: 'Activation code (FCL1-...) or license text',
            alignLabelWithHint: true,
            errorText: _localMessage ?? controller.importError,
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
            TextButton.icon(
              onPressed: controller.busy ? null : _chooseFile,
              icon: const Icon(Icons.folder_open),
              label: const Text('Choose .lic file'),
            ),
            const Spacer(),
            FilledButton.icon(
              onPressed: controller.busy
                  ? null
                  : () {
                      setState(() => _localMessage = null);
                      controller.submit(_text.text);
                    },
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
        const SizedBox(height: 4),
        Align(
          alignment: Alignment.centerLeft,
          child: TextButton.icon(
            onPressed: controller.busy ? null : controller.check,
            icon: const Icon(Icons.refresh, size: 18),
            label: const Text('Check again'),
          ),
        ),
      ],
    );
  }
}
