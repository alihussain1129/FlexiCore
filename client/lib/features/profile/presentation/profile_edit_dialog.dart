import 'package:flutter/material.dart';

import '../domain/business_profile.dart';
import 'profile_controller.dart';

/// Shows the saved business profile and lets the owner change it.
/// The creation date is shown but cannot be edited.
class ProfileEditDialog extends StatefulWidget {
  const ProfileEditDialog({super.key, required this.controller});

  final ProfileController controller;

  @override
  State<ProfileEditDialog> createState() => _ProfileEditDialogState();
}

class _ProfileEditDialogState extends State<ProfileEditDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.controller.profile?.businessName ?? '',
  );
  late final TextEditingController _owner = TextEditingController(
    text: widget.controller.profile?.ownerName ?? '',
  );
  late String? _industry = widget.controller.profile?.industry;
  late String? _currency = widget.controller.profile?.currency;
  bool _attempted = false;

  @override
  void dispose() {
    _name.dispose();
    _owner.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    setState(() => _attempted = true);
    final ok = await widget.controller.save(
      ProfileInput(
        businessName: _name.text,
        industry: _industry ?? '',
        currency: _currency ?? '',
        ownerName: _owner.text,
      ),
    );
    if (ok && mounted) {
      Navigator.pop(context);
    }
  }

  String _date(DateTime t) {
    final d = t.toLocal();
    String two(int n) => n.toString().padLeft(2, '0');
    return '${d.year}-${two(d.month)}-${two(d.day)}';
  }

  @override
  Widget build(BuildContext context) {
    final c = widget.controller;
    return ListenableBuilder(
      listenable: c,
      builder: (context, _) {
        final saving = c.saving;
        final errs = _attempted ? c.fieldErrors : const <String, String>{};
        final err = _attempted ? c.error : null;
        final created = c.profile?.createdAt;
        final industries = [
          ...ProfileRules.industries,
          if (_industry != null && !ProfileRules.industries.contains(_industry))
            _industry!,
        ];
        final currencies = [
          ...ProfileRules.currencies,
          if (_currency != null && !ProfileRules.currencies.contains(_currency))
            _currency!,
        ];
        return AlertDialog(
          title: const Text('Business profile'),
          content: SizedBox(
            width: 420,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (created != null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Text('Created ${_date(created)}'),
                    ),
                  TextField(
                    controller: _name,
                    enabled: !saving,
                    decoration: InputDecoration(
                      labelText: 'Business name',
                      errorText: errs['businessName'],
                    ),
                  ),
                  DropdownButtonFormField<String>(
                    initialValue: _industry,
                    decoration: InputDecoration(
                      labelText: 'Industry',
                      errorText: errs['industry'],
                    ),
                    items: [
                      for (final i in industries)
                        DropdownMenuItem(value: i, child: Text(i)),
                    ],
                    onChanged: saving
                        ? null
                        : (v) => setState(() => _industry = v),
                  ),
                  DropdownButtonFormField<String>(
                    initialValue: _currency,
                    decoration: InputDecoration(
                      labelText: 'Currency',
                      errorText: errs['currency'],
                    ),
                    items: [
                      for (final cur in currencies)
                        DropdownMenuItem(value: cur, child: Text(cur)),
                    ],
                    onChanged: saving
                        ? null
                        : (v) => setState(() => _currency = v),
                  ),
                  TextField(
                    controller: _owner,
                    enabled: !saving,
                    decoration: InputDecoration(
                      labelText: 'Owner name',
                      errorText: errs['ownerName'],
                    ),
                  ),
                  if (err != null)
                    Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: Text(
                        err,
                        style: TextStyle(
                          color: Theme.of(context).colorScheme.error,
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: saving ? null : () => Navigator.pop(context),
              child: const Text('Close'),
            ),
            FilledButton(
              onPressed: saving ? null : _save,
              child: saving
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Save'),
            ),
          ],
        );
      },
    );
  }
}
