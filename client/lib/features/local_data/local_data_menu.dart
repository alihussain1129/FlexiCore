import 'dart:io';

import 'package:file_selector/file_selector.dart';
import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import '../../core/app_theme.dart';
import '../../data/local/app_database.dart';
import '../../data/local/database_backup.dart';
import '../../data/local/database_integrity.dart';
import '../profile/presentation/profile_controller.dart';
import '../profile/presentation/profile_edit_dialog.dart';

/// Closes the database, restores [backup], and reopens the app on the result.
typedef RestoreHandler = Future<({bool ok, String message})> Function(
  File backup,
);

enum _Action { profile, backup, restore, check }

class LocalDataMenu extends StatefulWidget {
  const LocalDataMenu({
    super.key,
    required this.db,
    required this.profile,
    required this.onRestore,
  });

  final AppDatabase db;
  final ProfileController profile;
  final RestoreHandler onRestore;

  @override
  State<LocalDataMenu> createState() => _LocalDataMenuState();
}

class _LocalDataMenuState extends State<LocalDataMenu> {
  bool _busy = false;

  bool get _desktop =>
      Platform.isWindows || Platform.isLinux || Platform.isMacOS;

  Future<void> _run(_Action action) async {
    if (_busy) return;
    setState(() => _busy = true);
    try {
      switch (action) {
        case _Action.profile:
          await showDialog<void>(
            context: context,
            builder: (_) => ProfileEditDialog(controller: widget.profile),
          );
        case _Action.backup:
          await _backup();
        case _Action.restore:
          await _restore();
        case _Action.check:
          await _check();
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _backup() async {
    final messenger = ScaffoldMessenger.of(context);
    try {
      final Directory folder;
      if (_desktop) {
        final chosen = await getDirectoryPath(
          confirmButtonText: 'Save backup here',
        );
        if (chosen == null) return;
        folder = Directory(chosen);
      } else {
        final docs = await getApplicationDocumentsDirectory();
        folder = Directory(p.join(docs.path, 'FlexiCore Backups'));
      }
      final file = await createBackup(
        widget.db,
        File(p.join(folder.path, backupFileName(DateTime.now()))),
      );
      messenger.showSnackBar(
        SnackBar(
          content: Text('Backup saved: ${file.path}'),
          duration: const Duration(seconds: 10),
        ),
      );
    } on BackupException catch (e) {
      messenger.showSnackBar(SnackBar(content: Text(e.message)));
    } catch (e) {
      messenger.showSnackBar(SnackBar(content: Text('Backup failed: $e')));
    }
  }

  Future<void> _restore() async {
    final messenger = ScaffoldMessenger.of(context);
    final navigator = Navigator.of(context, rootNavigator: true);

    final picked = await openFile(
      acceptedTypeGroups: const [
        XTypeGroup(label: 'FlexiCore backup', extensions: ['sqlite']),
      ],
    );
    if (picked == null || !mounted) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Restore from backup?'),
        content: Text(
          'Your current data will be replaced by the contents of '
          '${p.basename(picked.path)}.\n\n'
          'Your current database is kept as a safety copy in the app data '
          'folder and is not deleted.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('Restore'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => const PopScope(
        canPop: false,
        child: AlertDialog(
          content: Row(
            children: [
              SizedBox(
                width: 24,
                height: 24,
                child: CircularProgressIndicator(strokeWidth: 3),
              ),
              SizedBox(width: 16),
              Text('Restoring...'),
            ],
          ),
        ),
      ),
    );

    ({bool ok, String message}) result;
    try {
      result = await widget.onRestore(File(picked.path));
    } catch (e) {
      result = (ok: false, message: 'Restore failed: $e');
    }
    navigator.pop();
    messenger.showSnackBar(
      SnackBar(
        content: Text(result.message),
        duration: const Duration(seconds: 12),
      ),
    );
  }

  Future<void> _check() async {
    final report = await checkDatabaseIntegrity(widget.db);
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Row(
          children: [
            Icon(
              report.isOk ? Icons.verified_outlined : Icons.warning_amber,
              color: report.isOk ? AppColors.teal : AppColors.clay,
            ),
            const SizedBox(width: 10),
            Flexible(
              child: Text(report.isOk ? 'Data check passed' : 'Problems found'),
            ),
          ],
        ),
        content: SingleChildScrollView(
          child: SelectableText(
            report.isOk
                ? 'No problems were found in the local database.'
                : report.problems.take(10).join('\n'),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }

  PopupMenuEntry<_Action> _item(
    _Action value,
    IconData icon,
    String title,
    String subtitle,
  ) {
    return PopupMenuItem<_Action>(
      value: value,
      child: SizedBox(
        width: 270,
        child: ListTile(
          contentPadding: EdgeInsets.zero,
          leading: Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.tealSoft,
              borderRadius: BorderRadius.circular(12),
            ),
            child: Icon(icon, color: AppColors.teal, size: 22),
          ),
          title: Text(
            title,
            style: const TextStyle(fontWeight: FontWeight.w600),
          ),
          subtitle: Text(
            subtitle,
            style: const TextStyle(color: AppColors.muted, fontSize: 12),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= 560;
    return PopupMenuButton<_Action>(
      tooltip: 'Business and data',
      enabled: !_busy,
      onSelected: _run,
      offset: const Offset(0, 48),
      itemBuilder: (_) => [
        _item(
          _Action.profile,
          Icons.storefront_outlined,
          'Business profile',
          'View or change your business details',
        ),
        const PopupMenuDivider(),
        _item(
          _Action.backup,
          Icons.save_alt,
          'Back up now',
          'Save a safe copy of all your data',
        ),
        _item(
          _Action.restore,
          Icons.settings_backup_restore,
          'Restore from backup',
          'Replace current data with a saved copy',
        ),
        _item(
          _Action.check,
          Icons.fact_check_outlined,
          'Check data integrity',
          'Make sure your data is healthy',
        ),
      ],
      child: AnimatedOpacity(
        duration: const Duration(milliseconds: 150),
        opacity: _busy ? 0.5 : 1,
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
          decoration: BoxDecoration(
            color: Colors.white.withValues(alpha: 0.16),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: Colors.white.withValues(alpha: 0.25)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.tune, color: Colors.white, size: 20),
              if (wide) ...[
                const SizedBox(width: 8),
                const Text(
                  'Business & data',
                  style: TextStyle(
                    color: Colors.white,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
