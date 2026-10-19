import 'package:flutter/foundation.dart';

import '../../core/api_exception.dart';
import '../../core/device_api_client.dart';
import '../../core/device_storage.dart';

enum DeviceStatus { starting, unpaired, paired, unreachable }

const _licenseRequiredType = 'urn:flexicore:problem:license-required';

/// Text safe to show a person for a failed pairing or device check.
String describeDeviceError(ApiException error) {
  if (error.statusCode == 403 && error.type == _licenseRequiredType) {
    return 'This installation is not activated yet. Activate it on the '
        'computer first, then try again.';
  }
  return error.message;
}

/// Tracks whether this installation is paired with a FlexiCore server.
///
/// Only a 401 from the server ever removes the stored credential. A network
/// failure, a timeout or a server error keeps it, so a phone that is briefly
/// out of reach does not lose its pairing.
class DeviceSessionController extends ChangeNotifier {
  DeviceSessionController({required this.api, required this.storage});

  final DeviceApiClient api;
  final DeviceStorage storage;

  DeviceStatus status = DeviceStatus.starting;
  DeviceInfo? device;

  /// A message for the screen about the last state change, or null.
  String? notice;

  /// App start, and the retry button when the server was unreachable.
  Future<void> start() async {
    final stored = await storage.read();
    if (stored == null) {
      device = null;
      _set(DeviceStatus.unpaired);
      return;
    }

    try {
      device = await api.me(
        serverUrl: stored.serverUrl,
        credential: stored.credential,
      );
      _set(DeviceStatus.paired);
    } on ApiException catch (e) {
      device = null;
      if (e.isUnauthorized) {
        await storage.clear();
        _set(
          DeviceStatus.unpaired,
          'This device is no longer authorised. Pair it again with a new code.',
        );
      } else {
        _set(DeviceStatus.unreachable, describeDeviceError(e));
      }
    }
  }

  /// Pairs with a one-time code. Throws [ApiException] with a message that is
  /// safe to show; nothing is stored unless the server accepted the code.
  Future<void> pair({
    required String serverUrl,
    required String code,
    required String deviceName,
  }) async {
    final normalized = DeviceApiClient.normalizeServerUrl(serverUrl);
    if (normalized == null) {
      throw ApiException(
        statusCode: 400,
        title: 'Enter the server address, for example http://localhost:5265',
      );
    }
    final name = deviceName.trim();
    if (name.isEmpty || name.length > 80) {
      throw ApiException(
        statusCode: 400,
        title: 'The device name must be 1 to 80 characters',
      );
    }
    final cleanCode = code.trim();
    if (cleanCode.isEmpty) {
      throw ApiException(statusCode: 400, title: 'Enter the pairing code');
    }

    final DevicePairResult result;
    try {
      result = await api.pair(
        serverUrl: normalized,
        code: cleanCode,
        deviceName: name,
      );
    } on ApiException catch (e) {
      throw ApiException(
        statusCode: e.statusCode,
        title: describeDeviceError(e),
        type: e.type,
        retryAfter: e.retryAfter,
      );
    }

    await storage.write(
      StoredDevice(
        serverUrl: normalized,
        credential: result.credential,
        deviceName: name,
      ),
    );
    device = DeviceInfo(
      deviceId: result.deviceId,
      name: name,
      isOwner: result.isOwner,
    );
    _set(DeviceStatus.paired);
  }

  /// Forgets the pairing on this device only. Revoking the device on the
  /// server is done by an owner device.
  Future<void> forget() async {
    await storage.clear();
    device = null;
    _set(DeviceStatus.unpaired);
  }

  void _set(DeviceStatus next, [String? message]) {
    status = next;
    notice = message;
    notifyListeners();
  }
}
