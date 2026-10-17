import 'dart:convert';

import '../../core/api_client.dart';
import '../../core/api_exception.dart';

class Organization {
  const Organization({
    required this.id,
    required this.name,
    required this.slug,
    required this.role,
  });

  factory Organization.fromJson(Map<String, dynamic> json) => Organization(
    id: json['id'] as String,
    name: json['name'] as String,
    slug: json['slug'] as String,
    role: json['role'] as String,
  );

  final String id;
  final String name;
  final String slug;
  final String role;
}

class Company {
  const Company({
    required this.id,
    required this.organizationId,
    required this.name,
    required this.slug,
    required this.industryKey,
    required this.role,
  });

  factory Company.fromJson(Map<String, dynamic> json) => Company(
    id: json['id'] as String,
    organizationId: json['organizationId'] as String,
    name: json['name'] as String,
    slug: json['slug'] as String,
    industryKey: json['industryKey'] as String,
    role: json['role'] as String,
  );

  final String id;
  final String organizationId;
  final String name;
  final String slug;
  final String industryKey;
  final String role;
}

class TenancyService {
  TenancyService(this._api);

  final ApiClient _api;

  Future<List<Organization>> listOrganizations({
    int limit = 100,
    int offset = 0,
  }) async {
    final response = await _api.send(
      'GET',
      '/api/v1/organizations/?limit=$limit&offset=$offset',
    );
    if (response.statusCode != 200) {
      throw ApiException.fromResponse(response);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw ApiException(
        statusCode: response.statusCode,
        title: 'Unexpected organization response',
      );
    }
    return decoded
        .map(
          (item) =>
              Organization.fromJson(Map<String, dynamic>.from(item as Map)),
        )
        .toList();
  }

  Future<List<Company>> listCompanies({int limit = 100, int offset = 0}) async {
    final response = await _api.send(
      'GET',
      '/api/v1/companies/?limit=$limit&offset=$offset',
    );
    if (response.statusCode != 200) {
      throw ApiException.fromResponse(response);
    }
    final decoded = jsonDecode(response.body);
    if (decoded is! List) {
      throw ApiException(
        statusCode: response.statusCode,
        title: 'Unexpected company response',
      );
    }
    return decoded
        .map((item) => Company.fromJson(Map<String, dynamic>.from(item as Map)))
        .toList();
  }

  Future<List<Organization>> listAllOrganizations({
    int pageSize = 100,
    int maxRows = 100000,
  }) async {
    final organizations = <Organization>[];
    var offset = 0;
    while (true) {
      final page = await listOrganizations(limit: pageSize, offset: offset);
      if (organizations.length + page.length > maxRows) {
        throw ApiException(
          statusCode: 413,
          title: 'Organization export limit exceeded',
        );
      }
      organizations.addAll(page);
      if (page.length < pageSize) return organizations;
      offset += page.length;
    }
  }

  Future<List<Company>> listAllCompanies({
    int pageSize = 100,
    int maxRows = 100000,
  }) async {
    final companies = <Company>[];
    var offset = 0;
    while (true) {
      final page = await listCompanies(limit: pageSize, offset: offset);
      if (companies.length + page.length > maxRows) {
        throw ApiException(
          statusCode: 413,
          title: 'Company export limit exceeded',
        );
      }
      companies.addAll(page);
      if (page.length < pageSize) return companies;
      offset += page.length;
    }
  }

  Future<Organization> createOrganization({
    required String name,
    required String slug,
  }) async {
    final response = await _api.send(
      'POST',
      '/api/v1/organizations/',
      body: {'name': name, 'slug': slug},
    );
    if (response.statusCode != 201) {
      throw ApiException.fromResponse(response);
    }
    return Organization.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
  }

  Future<Company> createCompany({
    required String organizationId,
    required String name,
    required String slug,
    required String industryKey,
  }) async {
    final response = await _api.send(
      'POST',
      '/api/v1/organizations/$organizationId/companies',
      body: {'name': name, 'slug': slug, 'industryKey': industryKey},
    );
    if (response.statusCode != 201) {
      throw ApiException.fromResponse(response);
    }
    return Company.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }
}
