import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// What a paired device keeps locally. The credential is a secret: it is shown
/// by nothing in this app and never appears in [toString].
class StoredDevice {
  const StoredDevice({
    required this.serverUrl,
    required this.credential,
    required this.deviceName,
  });

  final String serverUrl;
  final String credential;
  final String deviceName;

  @override
  String toString() => 'StoredDevice($deviceName at $serverUrl)';
}

abstract class DeviceStorage {
  Future<StoredDevice?> read();
  Future<void> write(StoredDevice device);
  Future<void> clear();
}

/// Production storage backed by the platform's secure store (Android Keystore,
/// Windows credential store).
class SecureDeviceStorage implements DeviceStorage {
  SecureDeviceStorage([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  static const _credentialKey = 'flexicore.device.credential';
  static const _serverKey = 'flexicore.device.server';
  static const _nameKey = 'flexicore.device.name';

  final FlutterSecureStorage _storage;

  @override
  Future<StoredDevice?> read() async {
    final credential = await _storage.read(key: _credentialKey);
    final server = await _storage.read(key: _serverKey);
    final name = await _storage.read(key: _nameKey);
    if (credential == null || server == null) return null;
    return StoredDevice(
      serverUrl: server,
      credential: credential,
      deviceName: name ?? '',
    );
  }

  /// The credential is written last, so an interrupted write never leaves a
  /// device that looks paired.
  @override
  Future<void> write(StoredDevice device) async {
    await _storage.write(key: _serverKey, value: device.serverUrl);
    await _storage.write(key: _nameKey, value: device.deviceName);
    await _storage.write(key: _credentialKey, value: device.credential);
  }

  /// The credential is removed first for the same reason.
  @override
  Future<void> clear() async {
    await _storage.delete(key: _credentialKey);
    await _storage.delete(key: _serverKey);
    await _storage.delete(key: _nameKey);
  }
}

/// For tests only.
class InMemoryDeviceStorage implements DeviceStorage {
  StoredDevice? _device;

  @override
  Future<StoredDevice?> read() async => _device;

  @override
  Future<void> write(StoredDevice device) async => _device = device;

  @override
  Future<void> clear() async => _device = null;
}