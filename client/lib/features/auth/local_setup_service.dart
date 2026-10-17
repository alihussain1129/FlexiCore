import 'dart:convert';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';

class LocalSetupService {
  LocalSetupService(this._api);

  final ApiClient _api;

  Future<bool> isSetupRequired() async {
    final response = await _api.getPublic('/api/v1/local/setup');
    if (response.statusCode != 200) throw ApiException.fromResponse(response);

    final decoded = jsonDecode(response.body);
    if (decoded is! Map || decoded['setupRequired'] is! bool) {
      throw ApiException(
        statusCode: response.statusCode,
        title: 'Unexpected local setup response',
      );
    }
    return decoded['setupRequired'] as bool;
  }

  Future<void> createFirstOwner({
    required String email,
    required String password,
  }) async {
    final response = await _api.postPublic('/api/v1/local/setup', {
      'email': email,
      'password': password,
    });
    if (response.statusCode != 201) throw ApiException.fromResponse(response);
  }
}
