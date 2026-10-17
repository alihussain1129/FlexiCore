# FlexiCore Flutter client

This Flutter application provides the FlexiCore authentication, Local activation and organization/company workspace flows. It currently does not contain transactional ERP modules.

For current implementation status, supported spreadsheet/document exchange, setup, and backup/recovery instructions, see the repository [README](../README.md).

Run the Windows client from this directory:

```powershell
flutter run -d windows --dart-define=API_BASE_URL=http://localhost:5265
```

Run client checks from this directory:

```powershell
dart format --output=none --set-exit-if-changed lib test
flutter analyze
flutter test
flutter build windows
```
