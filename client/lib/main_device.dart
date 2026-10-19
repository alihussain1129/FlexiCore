import 'package:flutter/material.dart';

import 'core/device_api_client.dart';
import 'core/device_storage.dart';
import 'features/device/device_gate.dart';
import 'features/device/device_session_controller.dart';

/// Separate entry point for device pairing. Run with:
///   flutter run -t lib/main_device.dart
/// The normal app entry point (main.dart) is not changed.
void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(
    DeviceApp(
      controller: DeviceSessionController(
        api: DeviceApiClient(),
        storage: SecureDeviceStorage(),
      ),
    ),
  );
}

class DeviceApp extends StatelessWidget {
  const DeviceApp({super.key, required this.controller});

  final DeviceSessionController controller;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'FlexiCore Device',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
        useMaterial3: true,
      ),
      home: DeviceGate(controller: controller),
    );
  }
}
