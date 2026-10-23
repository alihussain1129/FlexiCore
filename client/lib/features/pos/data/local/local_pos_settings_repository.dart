import 'package:drift/drift.dart';

import '../../../../data/local/app_database.dart';

/// POS settings. Defaults keep the old behaviour: no tax, any discount.
class PosConfig {
  const PosConfig({
    this.taxRateBp = 0,
    this.maxDiscountBp = 10000,
    this.receiptFooter,
  });

  /// Tax added on top of prices, in basis points (1500 is 15%).
  final int taxRateBp;

  /// Largest total discount allowed on one sale, in basis points of the
  /// sale subtotal.
  final int maxDiscountBp;

  final String? receiptFooter;

  static const PosConfig defaults = PosConfig();
}

class LocalPosSettingsRepository {
  LocalPosSettingsRepository(
    this._db, {
    required this.companyId,
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  final AppDatabase _db;
  final String companyId;
  final DateTime Function() _clock;

  Future<PosConfig> load() async {
    final row = await (_db.select(
      _db.posSettings,
    )..where((s) => s.companyId.equals(companyId))).getSingleOrNull();
    if (row == null) return PosConfig.defaults;
    return PosConfig(
      taxRateBp: row.taxRateBp,
      maxDiscountBp: row.maxDiscountBp,
      receiptFooter: row.receiptFooter,
    );
  }

  Future<PosConfig> save(PosConfig config) async {
    if (config.taxRateBp < 0 || config.taxRateBp > 10000) {
      throw ArgumentError('Tax rate must be between 0% and 100%.');
    }
    if (config.maxDiscountBp < 0 || config.maxDiscountBp > 10000) {
      throw ArgumentError('Maximum discount must be between 0% and 100%.');
    }
    final footer = config.receiptFooter?.trim();
    if (footer != null && footer.length > 200) {
      throw ArgumentError('The receipt footer is too long (200 characters).');
    }
    await _db
        .into(_db.posSettings)
        .insertOnConflictUpdate(
          PosSettingsCompanion.insert(
            id: 'pos-settings',
            companyId: companyId,
            taxRateBp: Value(config.taxRateBp),
            maxDiscountBp: Value(config.maxDiscountBp),
            receiptFooter: Value(
              footer == null || footer.isEmpty ? null : footer,
            ),
            updatedAt: _clock().toUtc(),
          ),
        );
    return load();
  }
}
