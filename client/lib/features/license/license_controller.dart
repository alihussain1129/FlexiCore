import 'package:flutter/foundation.dart';

import '../../core/api_exception.dart';
import 'license_service.dart';

/// What the root of the app shows before sign-in.
enum LicenseUiState { checking, ready, needsLicense, unavailable, unreachable }

/// Decides, from the server's license status only, whether the app may go on
/// to sign-in. Anything it cannot decide fails closed.
class LicenseController extends ChangeNotifier {
  LicenseController({
    required Future<LicenseStatus?> Function() fetchStatus,
    required Future<LicenseStatus> Function(String text) importLicense,
    Future<String?> Function()? fetchRequestCode,
  }) : _fetchStatus = fetchStatus,
       _importLicense = importLicense,
       _fetchRequestCode = fetchRequestCode;

  final Future<LicenseStatus?> Function() _fetchStatus;
  final Future<LicenseStatus> Function(String text) _importLicense;
  final Future<String?> Function()? _fetchRequestCode;
  String? _requestCode;
  bool _requestLoaded = false;

  LicenseUiState _state = LicenseUiState.checking;
  LicenseStatus? _status;
  String? _problem;
  String? _importError;
  bool _busy = false;

  LicenseUiState get state => _state;
  String? get problem => _problem;
  String? get importError => _importError;
  bool get busy => _busy;

  /// This computer's activation request code, or null when unavailable.
  String? get requestCode => _requestCode;
  bool get requestLoaded => _requestLoaded;

  Future<void> loadRequestCode() async {
    final fetch = _fetchRequestCode;
    if (fetch != null) {
      try {
        _requestCode = await fetch();
      } catch (_) {
        _requestCode = null;
      }
    }
    _requestLoaded = true;
    notifyListeners();
  }

  /// Extra explanation when a license is installed but not usable.
  String? get hint {
    if (_status?.reason == 'wrong_installation') {
      return "This license was issued for a different computer. Send this computer's request code to the vendor to get a new one.";
    }
    if (_status?.reason == 'not_bound') {
      return 'The installed license is not tied to this computer. Ask the vendor for a new license made from your request code.';
    }
    switch (_status?.state) {
      case 'expired':
        return 'Your license has expired. Paste a new license file to continue.';
      case 'invalid':
        return 'The installed license is not valid. Paste a valid license file to continue.';
      case 'notYetValid':
        return 'The installed license is not valid yet. Check the date on this computer, or paste a different license.';
      default:
        return null;
    }
  }

  Future<void> check() async {
    _state = LicenseUiState.checking;
    _problem = null;
    _importError = null;
    notifyListeners();
    try {
      _apply(await _fetchStatus());
    } on ApiException catch (e) {
      _fail(e.message);
    } catch (_) {
      _fail('Could not check the license status.');
    }
    notifyListeners();
  }

  /// Sends the pasted text exactly as given. Returns true when the app may go on.
  Future<bool> submit(String text) async {
    if (_busy) return false;
    if (text.trim().isEmpty) {
      _importError = 'Paste the contents of your license file first.';
      notifyListeners();
      return false;
    }
    _busy = true;
    _importError = null;
    notifyListeners();
    try {
      _apply(await _importLicense(text));
    } on LicenseImportException catch (e) {
      _importError = e.message;
    } catch (_) {
      _importError = 'The license could not be activated. Please try again.';
    }
    _busy = false;
    notifyListeners();
    return _state == LicenseUiState.ready;
  }

  void _apply(LicenseStatus? status) {
    _status = status;
    _problem = null;
    if (status == null) {
      // The server offers no anonymous license access to this caller
      // (Hosted mode, or another machine): normal sign-in applies.
      _state = LicenseUiState.ready;
      return;
    }
    switch (status.gate) {
      case LicenseGate.proceed:
        _state = LicenseUiState.ready;
      case LicenseGate.needsLicense:
        _state = LicenseUiState.needsLicense;
      case LicenseGate.unavailable:
        _state = LicenseUiState.unavailable;
        _problem = 'This copy of FlexiCore cannot verify licenses right now. Contact the vendor, or try again later.';
    }
  }

  void _fail(String message) {
    _status = null;
    _state = LicenseUiState.unreachable;
    _problem = message;
  }
}
