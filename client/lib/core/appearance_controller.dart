import 'package:flutter/material.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

class AppearanceController extends ChangeNotifier {
  AppearanceController([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _key = 'flexicore.appearance.dark';
  final FlutterSecureStorage _storage;

  ThemeMode _themeMode = ThemeMode.light;
  ThemeMode get themeMode => _themeMode;
  bool get isDark => _themeMode == ThemeMode.dark;

  Future<void> load() async {
    try {
      final saved = await _storage.read(key: _key);
      _themeMode = saved == 'true' ? ThemeMode.dark : ThemeMode.light;
      notifyListeners();
    } catch (_) {
      // If preference storage is unavailable, keep the safe light default.
    }
  }

  Future<void> toggle() async {
    _themeMode = isDark ? ThemeMode.light : ThemeMode.dark;
    notifyListeners();
    try {
      await _storage.write(key: _key, value: isDark.toString());
    } catch (_) {
      // Theme still works for this session if persistence is unavailable.
    }
  }

  Future<void> setTheme(ThemeMode mode) async {
    _themeMode = mode;
    notifyListeners();
    try {
      await _storage.write(
        key: _key,
        value: (mode == ThemeMode.dark).toString(),
      );
    } catch (_) {
      // Theme still works for this session if persistence is unavailable.
    }
  }
}
