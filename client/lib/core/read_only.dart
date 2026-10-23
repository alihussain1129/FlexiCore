import 'package:flutter/widgets.dart';
import 'package:provider/provider.dart';

import '../data/local/database_opener.dart' show ReadOnlyLicenseException;
import '../features/license/license_controller.dart';

export '../data/local/database_opener.dart' show ReadOnlyLicenseException;

/// True when the license has expired and the app is read-only.
/// Call it from a build method. It returns false when no license is
/// provided (for example in small widget tests).
bool isReadOnlyOf(BuildContext context) {
  try {
    return context.watch<LicenseController>().isReadOnly;
  } on ProviderNotFoundException {
    return false;
  }
}

/// Shows the clean "license expired" text for a blocked write,
/// and "prefix: error" for everything else.
String friendlyError(Object error, String prefix) {
  if (error is ReadOnlyLicenseException) return error.toString();
  return '$prefix: $error';
}