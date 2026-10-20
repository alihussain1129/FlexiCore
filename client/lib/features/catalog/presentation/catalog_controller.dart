import 'package:flutter/foundation.dart';

import '../../../core/money.dart';
import '../domain/catalog_domain.dart';

/// Holds catalog state for the screen. Talks only to [CatalogRepository], so
/// it works with the local database and is tested without any UI.
///
/// Every mutating method returns null on success or a user-readable message on
/// failure, and always reloads afterwards so the screen shows current data
/// (for example after a version conflict).
class CatalogController extends ChangeNotifier {
  CatalogController(this._repo);

  final CatalogRepository _repo;

  List<CatalogUnit> units = const [];
  List<CatalogProduct> products = const [];
  String query = '';
  bool loading = false;
  String? loadError;

  Future<void> load() async {
    loading = true;
    notifyListeners();
    try {
      units = await _repo.listUnits();
      products = await _repo.listProducts(query: query);
      loadError = null;
    } catch (e) {
      loadError = 'Could not read the catalog: $e';
    }
    loading = false;
    notifyListeners();
  }

  Future<void> search(String value) {
    query = value;
    return load();
  }

  Future<String?> addUnit(String code, String name) =>
      _run(() => _repo.createUnit(code: code, name: name));

  Future<String?> saveUnit(CatalogUnit unit, String code, String name) => _run(
    () => _repo.updateUnit(
      unit.id,
      code: code,
      name: name,
      expectedVersion: unit.version,
    ),
  );

  Future<String?> removeUnit(CatalogUnit unit) =>
      _run(() => _repo.deleteUnit(unit.id, expectedVersion: unit.version));

  Future<String?> addProduct({
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    required String salePrice,
    required String costPrice,
  }) async {
    final sale = Money.parseE4(salePrice.trim().isEmpty ? '0' : salePrice);
    if (sale == null) {
      return 'Sale price is not a valid amount (example: 12.50).';
    }
    final cost = Money.parseE4(costPrice.trim().isEmpty ? '0' : costPrice);
    if (cost == null) {
      return 'Cost price is not a valid amount (example: 9.75).';
    }
    return _run(
      () => _repo.createProduct(
        sku: sku,
        name: name,
        barcode: barcode,
        unitId: unitId,
        salePriceE4: sale,
        costPriceE4: cost,
      ),
    );
  }

  Future<String?> saveProduct(
    CatalogProduct product, {
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    required String salePrice,
    required String costPrice,
  }) async {
    final sale = Money.parseE4(salePrice.trim().isEmpty ? '0' : salePrice);
    if (sale == null) {
      return 'Sale price is not a valid amount (example: 12.50).';
    }
    final cost = Money.parseE4(costPrice.trim().isEmpty ? '0' : costPrice);
    if (cost == null) {
      return 'Cost price is not a valid amount (example: 9.75).';
    }
    return _run(
      () => _repo.updateProduct(
        product.id,
        sku: sku,
        name: name,
        barcode: barcode,
        unitId: unitId,
        salePriceE4: sale,
        costPriceE4: cost,
        expectedVersion: product.version,
      ),
    );
  }

  Future<String?> removeProduct(CatalogProduct product) => _run(
    () => _repo.deleteProduct(product.id, expectedVersion: product.version),
  );

  Future<String?> _run(Future<Object?> Function() action) async {
    String? message;
    try {
      await action();
    } on CatalogException catch (e) {
      message = e.message;
    } catch (e) {
      message = 'Unexpected error: $e';
    }
    await load();
    return message;
  }
}
