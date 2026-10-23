import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';
import 'local_setup_service.dart';
import 'login_screen.dart';
import 'session_controller.dart';

class LocalSetupScreen extends StatefulWidget {
  const LocalSetupScreen({super.key});

  @override
  State<LocalSetupScreen> createState() => _LocalSetupScreenState();
}

class _LocalSetupScreenState extends State<LocalSetupScreen> {
  late final LocalSetupService _service;
  final _username = TextEditingController();
  final _password = TextEditingController();
  final _formKey = GlobalKey<FormState>();
  bool? _setupRequired;
  bool _loading = true;
  bool _busy = false;
  String? _error;
  String? _loginNotice;

  @override
  void initState() {
    super.initState();
    _service = LocalSetupService(context.read<ApiClient>());
    _load();
  }

  @override
  void dispose() {
    _username.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final required = await _service.isSetupRequired();
      if (mounted) setState(() => _setupRequired = required);
    } on ApiException catch (error) {
      if (mounted) setState(() => _error = error.message);
    } catch (_) {
      if (mounted) {
        setState(
          () => _error = 'Could not check local setup. Please try again.',
        );
      }
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _createOwner() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final username = _username.text.trim();
      await _service.createFirstOwner(
        username: username,
        password: _password.text,
      );
      if (!mounted) return;
      setState(() => _setupRequired = false);
      await context.read<SessionController>().login(username, _password.text);
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _error = error.message;
          if (error.statusCode == 409) _setupRequired = false;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _setupRequired = false;
          _loginNotice = 'Your local owner account was created. Sign in below to continue.';
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_setupRequired == false && !_loading) {
      return LoginScreen(notice: _loginNotice);
    }

    if (_loading || _setupRequired == null && _error == null) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    if (_error != null && _setupRequired == null) {
      return Scaffold(
        body: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text('Local setup is unavailable'),
                  const SizedBox(height: 12),
                  Text(_error!, textAlign: TextAlign.center),
                  const SizedBox(height: 20),
                  FilledButton.icon(
                    onPressed: _load,
                    icon: const Icon(Icons.refresh),
                    label: const Text('Try again'),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      body: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 440),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Form(
              key: _formKey,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Set up your local workspace',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Create the first local owner account. This setup is available only on this computer and only once.',
                  ),
                  const SizedBox(height: 24),
                  TextFormField(
                    controller: _username,
                    decoration: const InputDecoration(
                      labelText: 'Owner username',
                    ),
                    keyboardType: TextInputType.text,
                    validator: (value) =>
                        value == null || value.trim().length < 3
                        ? 'Use at least 3 characters.'
                        : null,
                  ),
                  const SizedBox(height: 12),
                  TextFormField(
                    controller: _password,
                    decoration: const InputDecoration(
                      labelText: 'Password',
                      helperText: '12 to 128 characters; must not contain your username',
                    ),
                    obscureText: true,
                    validator: (value) =>
                        value == null || value.runes.length < 12
                        ? 'Use at least 12 characters.'
                        : null,
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: 12),
                    Text(
                      _error!,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ],
                  const SizedBox(height: 20),
                  FilledButton(
                    onPressed: _busy ? null : _createOwner,
                    child: Text(_busy ? 'Setting up...' : 'Create local owner'),
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
