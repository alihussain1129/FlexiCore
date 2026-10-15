/// Build-time settings. Override the API address with:
///   flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5080
class AppConfig {
  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://localhost:5265',
  );
}