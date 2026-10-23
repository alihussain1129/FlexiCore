import 'package:flutter/material.dart';

import '../domain/business_profile.dart';
import 'profile_controller.dart';

class ProfileSetupScreen extends StatefulWidget {
  const ProfileSetupScreen({super.key, required this.controller});

  final ProfileController controller;

  @override
  State<ProfileSetupScreen> createState() => _ProfileSetupScreenState();
}

class _ProfileSetupScreenState extends State<ProfileSetupScreen> {
  static const _ink = Color(0xFF16302F);
  static const _paper = Color(0xFFF3EFE7);
  static const _accent = Color(0xFF0F5C5A);

  final _name = TextEditingController();
  final _owner = TextEditingController();
  String? _industry;
  String? _currency = 'PKR';

  @override
  void dispose() {
    _name.dispose();
    _owner.dispose();
    super.dispose();
  }

  Future<void> _submit() => widget.controller.save(
    ProfileInput(
      businessName: _name.text,
      industry: _industry ?? '',
      currency: _currency ?? '',
      ownerName: _owner.text,
    ),
  );

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: _paper,
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: TweenAnimationBuilder<double>(
              tween: Tween(begin: 0, end: 1),
              duration: const Duration(milliseconds: 450),
              curve: Curves.easeOutCubic,
              builder: (context, t, child) => Opacity(
                opacity: t,
                child: Transform.translate(
                  offset: Offset(0, 16 * (1 - t)),
                  child: child,
                ),
              ),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 520),
                child: Container(
                  padding: const EdgeInsets.all(28),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(18),
                    boxShadow: [
                      BoxShadow(
                        color: _ink.withValues(alpha: 0.10),
                        blurRadius: 32,
                        offset: const Offset(0, 14),
                      ),
                    ],
                  ),
                  child: ListenableBuilder(
                    listenable: widget.controller,
                    builder: (context, _) => _form(context),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _form(BuildContext context) {
    final c = widget.controller;
    final errs = c.fieldErrors;
    final saving = c.saving;
    return Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'Set up your business',
          style: TextStyle(
            fontSize: 26,
            fontWeight: FontWeight.w700,
            color: _ink,
          ),
        ),
        const SizedBox(height: 6),
        Text(
          'Stored on this device only. No account or internet needed.',
          style: TextStyle(color: _ink.withValues(alpha: 0.65)),
        ),
        const SizedBox(height: 24),
        TextField(
          controller: _name,
          enabled: !saving,
          textInputAction: TextInputAction.next,
          decoration: InputDecoration(
            labelText: 'Business name',
            errorText: errs['businessName'],
            border: const OutlineInputBorder(),
          ),
        ),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          initialValue: _industry,
          decoration: InputDecoration(
            labelText: 'Industry',
            errorText: errs['industry'],
            border: const OutlineInputBorder(),
          ),
          items: [
            for (final i in ProfileRules.industries)
              DropdownMenuItem(value: i, child: Text(i)),
          ],
          onChanged: saving ? null : (v) => setState(() => _industry = v),
        ),
        const SizedBox(height: 16),
        DropdownButtonFormField<String>(
          initialValue: _currency,
          decoration: InputDecoration(
            labelText: 'Currency',
            errorText: errs['currency'],
            border: const OutlineInputBorder(),
          ),
          items: [
            for (final cur in ProfileRules.currencies)
              DropdownMenuItem(value: cur, child: Text(cur)),
          ],
          onChanged: saving ? null : (v) => setState(() => _currency = v),
        ),
        const SizedBox(height: 16),
        TextField(
          controller: _owner,
          enabled: !saving,
          onSubmitted: (_) => saving ? null : _submit(),
          decoration: InputDecoration(
            labelText: 'Owner name',
            errorText: errs['ownerName'],
            border: const OutlineInputBorder(),
          ),
        ),
        AnimatedSize(
          duration: const Duration(milliseconds: 200),
          child: c.error == null
              ? const SizedBox(width: double.infinity)
              : Padding(
                  padding: const EdgeInsets.only(top: 16),
                  child: Text(
                    c.error!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
        ),
        const SizedBox(height: 24),
        FilledButton(
          onPressed: saving ? null : _submit,
          style: FilledButton.styleFrom(
            backgroundColor: _accent,
            padding: const EdgeInsets.symmetric(vertical: 16),
          ),
          child: AnimatedSwitcher(
            duration: const Duration(milliseconds: 180),
            child: saving
                ? const SizedBox(
                    key: ValueKey('busy'),
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(
                      strokeWidth: 2.5,
                      color: Colors.white,
                    ),
                  )
                : const Text('Create business', key: ValueKey('idle')),
          ),
        ),
      ],
    );
  }
}
