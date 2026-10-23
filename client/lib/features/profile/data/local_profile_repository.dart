import 'package:drift/drift.dart';

import '../../../data/local/app_database.dart';
import '../domain/business_profile.dart';

class LocalProfileRepository {
  LocalProfileRepository(
    this._db, {
    required this.companyId,
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  static const String rowId = 'business-profile';

  final AppDatabase _db;
  final String companyId;
  final DateTime Function() _clock;

  Future<BusinessProfile?> load() async {
    final row = await (_db.select(
      _db.businessProfiles,
    )..where((t) => t.id.equals(rowId))).getSingleOrNull();
    return row == null ? null : _map(row);
  }

  /// Creates the profile. Never overwrites an existing one.
  Future<BusinessProfile> create(ProfileInput input) async {
    final c = ProfileRules.clean(input);
    final now = _clock();
    try {
      return await _db.transaction(() async {
        if (await load() != null) {
          throw ProfileException(
            'A business profile already exists on this device.',
          );
        }
        await _db
            .into(_db.businessProfiles)
            .insert(
              BusinessProfilesCompanion.insert(
                id: rowId,
                companyId: companyId,
                businessName: c.businessName,
                industry: c.industry,
                currency: c.currency,
                ownerName: c.ownerName,
                createdAt: now,
                updatedAt: now,
              ),
            );
        return (await load())!;
      });
    } on ProfileException {
      rethrow;
    } catch (e) {
      throw ProfileException(
        'The profile could not be saved. Your data has not been changed. ($e)',
      );
    }
  }

  /// Updates the existing profile; keeps createdAt.
  Future<BusinessProfile> update(ProfileInput input) async {
    final c = ProfileRules.clean(input);
    try {
      return await _db.transaction(() async {
        final updated =
            await (_db.update(
              _db.businessProfiles,
            )..where((t) => t.id.equals(rowId))).write(
              BusinessProfilesCompanion(
                businessName: Value(c.businessName),
                industry: Value(c.industry),
                currency: Value(c.currency),
                ownerName: Value(c.ownerName),
                updatedAt: Value(_clock()),
              ),
            );
        if (updated == 0) {
          throw ProfileException('There is no business profile to update.');
        }
        return (await load())!;
      });
    } on ProfileException {
      rethrow;
    } catch (e) {
      throw ProfileException(
        'The profile could not be saved. Your data has not been changed. ($e)',
      );
    }
  }

  BusinessProfile _map(BusinessProfileRow r) => BusinessProfile(
    businessName: r.businessName,
    industry: r.industry,
    currency: r.currency,
    ownerName: r.ownerName,
    createdAt: r.createdAt,
    updatedAt: r.updatedAt,
  );
}
