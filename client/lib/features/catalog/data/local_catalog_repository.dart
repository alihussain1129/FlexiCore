import 'package:drift/drift.dart';

import '../../../core/ids.dart';
import '../../../data/local/app_database.dart';
import '../domain/catalog_domain.dart';

/// Authoritative local catalog store. Every write runs in a transaction, and
/// SQLite serializes writers, so check-then-write sequences here (for example
/// "no active products use this unit" then "delete the unit") cannot race.
class LocalCatalogRepository implements CatalogRepository {
  LocalCatalogRepository(
    this._db, {
    required this.companyId,
    this.deviceId,
    DateTime Function()? clock,
    String Function()? idGenerator,
  }) : _clock = clock ?? DateTime.now,
       _newId = idGenerator ?? generateUuidV4;

  final AppDatabase _db;
  final String companyId;
  final String? deviceId;
  final DateTime Function() _clock;
  final String Function() _newId;

  // ---------------------------------------------------------------- units

  @override
  Future<List<CatalogUnit>> listUnits() async {
    final rows =
        await (_db.select(_db.units)
              ..where(
                (u) => u.companyId.equals(companyId) & u.deletedAt.isNull(),
              )
              ..orderBy([(u) => OrderingTerm.asc(u.code)]))
            .get();
    return rows.map(_toUnit).toList();
  }

  @override
  Future<CatalogUnit> createUnit({
    required String code,
    required String name,
  }) async {
    final c = CatalogRules.unitCode(code);
    final n = CatalogRules.name(name);
    return _db.transaction(() async {
      if (await _activeUnitByCode(c) != null) {
        throw CatalogException(
          CatalogError.duplicate,
          'A unit with code "$c" already exists.',
        );
      }
      final id = _newId();
      await _db
          .into(_db.units)
          .insert(
            UnitsCompanion.insert(
              id: id,
              companyId: companyId,
              code: c,
              name: n,
              updatedAt: _clock(),
              originDeviceId: Value(deviceId),
            ),
          );
      return _toUnit(await _activeUnitOrThrow(id));
    });
  }

  @override
  Future<CatalogUnit> updateUnit(
    String id, {
    required String code,
    required String name,
    required int expectedVersion,
  }) async {
    final c = CatalogRules.unitCode(code);
    final n = CatalogRules.name(name);
    return _db.transaction(() async {
      final row = await _activeUnitOrThrow(id);
      _checkVersion(row.version, expectedVersion, 'unit');
      final clash = await _activeUnitByCode(c);
      if (clash != null && clash.id != id) {
        throw CatalogException(
          CatalogError.duplicate,
          'A unit with code "$c" already exists.',
        );
      }
      await (_db.update(_db.units)..where((u) => u.id.equals(id))).write(
        UnitsCompanion(
          code: Value(c),
          name: Value(n),
          version: Value(row.version + 1),
          updatedAt: Value(_clock()),
          originDeviceId: Value(deviceId),
        ),
      );
      return _toUnit(await _activeUnitOrThrow(id));
    });
  }

  @override
  Future<void> deleteUnit(String id, {required int expectedVersion}) {
    return _db.transaction(() async {
      final row = await _activeUnitOrThrow(id);
      _checkVersion(row.version, expectedVersion, 'unit');
      final countExpr = _db.products.id.count();
      final inUse =
          await (_db.selectOnly(_db.products)
                ..addColumns([countExpr])
                ..where(
                  _db.products.unitId.equals(id) &
                      _db.products.deletedAt.isNull(),
                ))
              .map((r) => r.read(countExpr) ?? 0)
              .getSingle();
      if (inUse > 0) {
        throw CatalogException(
          CatalogError.inUse,
          'This unit is used by $inUse product(s). Change or remove them first.',
        );
      }
      final now = _clock();
      await (_db.update(_db.units)..where((u) => u.id.equals(id))).write(
        UnitsCompanion(
          deletedAt: Value(now),
          version: Value(row.version + 1),
          updatedAt: Value(now),
          originDeviceId: Value(deviceId),
        ),
      );
    });
  }

  // ------------------------------------------------------------ products

  @override
  Future<List<CatalogProduct>> listProducts({String? query}) async {
    final q = query?.trim();
    final rows =
        await (_db.select(_db.products)
              ..where((p) {
                final base =
                    p.companyId.equals(companyId) & p.deletedAt.isNull();
                if (q == null || q.isEmpty) return base;
                return base &
                    (p.name.contains(q) |
                        p.sku.contains(q) |
                        p.barcode.contains(q));
              })
              ..orderBy([(p) => OrderingTerm.asc(p.name)]))
            .get();
    return rows.map(_toProduct).toList();
  }

  @override
  Future<CatalogProduct> createProduct({
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    int salePriceE4 = 0,
    int costPriceE4 = 0,
  }) async {
    final s = CatalogRules.sku(sku);
    final n = CatalogRules.name(name);
    final b = CatalogRules.barcode(barcode);
    final sale = CatalogRules.priceE4(salePriceE4, 'Sale price');
    final cost = CatalogRules.priceE4(costPriceE4, 'Cost price');
    return _db.transaction(() async {
      if (unitId != null) await _activeUnitOrThrow(unitId);
      if (await _activeProductBySku(s) != null) {
        throw CatalogException(
          CatalogError.duplicate,
          'A product with SKU "$s" already exists.',
        );
      }
      final id = _newId();
      await _db
          .into(_db.products)
          .insert(
            ProductsCompanion.insert(
              id: id,
              companyId: companyId,
              sku: s,
              name: n,
              updatedAt: _clock(),
              barcode: Value(b),
              unitId: Value(unitId),
              salePriceE4: Value(sale),
              costPriceE4: Value(cost),
              originDeviceId: Value(deviceId),
            ),
          );
      return _toProduct(await _activeProductOrThrow(id));
    });
  }

  @override
  Future<CatalogProduct> updateProduct(
    String id, {
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    required int salePriceE4,
    required int costPriceE4,
    required int expectedVersion,
  }) async {
    final s = CatalogRules.sku(sku);
    final n = CatalogRules.name(name);
    final b = CatalogRules.barcode(barcode);
    final sale = CatalogRules.priceE4(salePriceE4, 'Sale price');
    final cost = CatalogRules.priceE4(costPriceE4, 'Cost price');
    return _db.transaction(() async {
      final row = await _activeProductOrThrow(id);
      _checkVersion(row.version, expectedVersion, 'product');
      if (unitId != null) await _activeUnitOrThrow(unitId);
      final clash = await _activeProductBySku(s);
      if (clash != null && clash.id != id) {
        throw CatalogException(
          CatalogError.duplicate,
          'A product with SKU "$s" already exists.',
        );
      }
      await (_db.update(_db.products)..where((p) => p.id.equals(id))).write(
        ProductsCompanion(
          sku: Value(s),
          name: Value(n),
          barcode: Value(b),
          unitId: Value(unitId),
          salePriceE4: Value(sale),
          costPriceE4: Value(cost),
          version: Value(row.version + 1),
          updatedAt: Value(_clock()),
          originDeviceId: Value(deviceId),
        ),
      );
      return _toProduct(await _activeProductOrThrow(id));
    });
  }

  @override
  Future<void> deleteProduct(String id, {required int expectedVersion}) {
    return _db.transaction(() async {
      final row = await _activeProductOrThrow(id);
      _checkVersion(row.version, expectedVersion, 'product');
      final now = _clock();
      await (_db.update(_db.products)..where((p) => p.id.equals(id))).write(
        ProductsCompanion(
          deletedAt: Value(now),
          version: Value(row.version + 1),
          updatedAt: Value(now),
          originDeviceId: Value(deviceId),
        ),
      );
    });
  }

  // ------------------------------------------------------------- helpers

  void _checkVersion(int actual, int expected, String what) {
    if (actual != expected) {
      throw CatalogException(
        CatalogError.conflict,
        'This $what was changed elsewhere. Reload it and try again.',
      );
    }
  }

  Future<UnitRow?> _activeUnitByCode(String code) {
    return (_db.select(_db.units)..where(
          (u) =>
              u.companyId.equals(companyId) &
              u.code.equals(code) &
              u.deletedAt.isNull(),
        ))
        .getSingleOrNull();
  }

  Future<UnitRow> _activeUnitOrThrow(String id) async {
    final row =
        await (_db.select(_db.units)..where(
              (u) =>
                  u.id.equals(id) &
                  u.companyId.equals(companyId) &
                  u.deletedAt.isNull(),
            ))
            .getSingleOrNull();
    if (row == null) {
      throw CatalogException(CatalogError.notFound, 'Unit not found.');
    }
    return row;
  }

  Future<ProductRow?> _activeProductBySku(String sku) {
    return (_db.select(_db.products)..where(
          (p) =>
              p.companyId.equals(companyId) &
              p.sku.equals(sku) &
              p.deletedAt.isNull(),
        ))
        .getSingleOrNull();
  }

  Future<ProductRow> _activeProductOrThrow(String id) async {
    final row =
        await (_db.select(_db.products)..where(
              (p) =>
                  p.id.equals(id) &
                  p.companyId.equals(companyId) &
                  p.deletedAt.isNull(),
            ))
            .getSingleOrNull();
    if (row == null) {
      throw CatalogException(CatalogError.notFound, 'Product not found.');
    }
    return row;
  }

  CatalogUnit _toUnit(UnitRow r) => CatalogUnit(
    id: r.id,
    code: r.code,
    name: r.name,
    version: r.version,
    updatedAt: r.updatedAt,
  );

  CatalogProduct _toProduct(ProductRow r) => CatalogProduct(
    id: r.id,
    sku: r.sku,
    barcode: r.barcode,
    name: r.name,
    unitId: r.unitId,
    salePriceE4: r.salePriceE4,
    costPriceE4: r.costPriceE4,
    version: r.version,
    updatedAt: r.updatedAt,
  );
}
