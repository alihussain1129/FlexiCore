import 'package:flutter/foundation.dart';

import '../data/local_profile_repository.dart';
import '../domain/business_profile.dart';

class ProfileController extends ChangeNotifier {
  ProfileController(this._repo);

  final LocalProfileRepository _repo;

  BusinessProfile? _profile;
  bool _saving = false;
  String? _error;
  Map<String, String> _fieldErrors = const {};

  BusinessProfile? get profile => _profile;
  bool get saving => _saving;
  String? get error => _error;
  Map<String, String> get fieldErrors => _fieldErrors;

  Future<void> load() async {
    _profile = await _repo.load();
    notifyListeners();
  }

  /// Creates the profile if none exists, otherwise updates it.
  /// Returns false (without touching the database) if a save is running.
  Future<bool> save(ProfileInput input) async {
    if (_saving) return false;
    _saving = true;
    _error = null;
    _fieldErrors = const {};
    notifyListeners();
    try {
      _profile = _profile == null
          ? await _repo.create(input)
          : await _repo.update(input);
      return true;
    } on ProfileException catch (e) {
      _error = e.message;
      _fieldErrors = e.fieldErrors;
      return false;
    } finally {
      _saving = false;
      notifyListeners();
    }
  }
}
