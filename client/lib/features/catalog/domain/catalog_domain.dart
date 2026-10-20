/// Domain layer for the local catalog. No database types leak out of here.
library;

enum CatalogError { validation, duplicate, notFound, conflict, inUse }

class CatalogException implements Exception {
  CatalogException(this.error, this.message);

  final CatalogError error;
  final String message;

  @override
  String toString() => 'CatalogException(${error.name}): $message';
}

class CatalogUnit {
  const CatalogUnit({
    required this.id,
    required this.code,
    required this.name,
    required this.version,
    required this.updatedAt,
  });

  final String id;
  final String code;
  final String name;
  final int version;
  final DateTime updatedAt;
}

class CatalogProduct {
  const CatalogProduct({
    required this.id,
    required this.sku,
    required this.barcode,
    required this.name,
    required this.unitId,
    required this.salePriceE4,
    required this.costPriceE4,
    required this.version,
    required this.updatedAt,
  });

  final String id;
  final String sku;
  final String? barcode;
  final String name;
  final String? unitId;

  /// Price in ten-thousandths of the currency unit (1.5000 -> 15000).
  final int salePriceE4;
  final int costPriceE4;
  final int version;
  final DateTime updatedAt;
}

/// Validation rules. The patterns and limits come from the CHECK constraints
/// in server migration 0012_catalog.sql so both stores accept the same data.
/// (CatalogRules.cs on the server has not been compared yet.)
class CatalogRules {
  CatalogRules._();

  static final RegExp _unitCode = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$');
  static final RegExp _sku = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$');
  static final RegExp _barcode = RegExp(r'^[A-Za-z0-9-]{4,64}$');

  static String unitCode(String value) {
    final v = value.trim();
    if (!_unitCode.hasMatch(v)) {
      throw CatalogException(
        CatalogError.validation,
        'Unit code must be 1-20 characters: letters, digits, dot, '
        'underscore or hyphen, starting with a letter or digit.',
      );
    }
    return v;
  }

  static String sku(String value) {
    final v = value.trim();
    if (!_sku.hasMatch(v)) {
      throw CatalogException(
        CatalogError.validation,
        'SKU must be 1-40 characters: letters, digits, dot, underscore or '
        'hyphen, starting with a letter or digit.',
      );
    }
    return v;
  }

  /// Returns null for an empty barcode.
  static String? barcode(String? value) {
    final v = value?.trim();
    if (v == null || v.isEmpty) return null;
    if (!_barcode.hasMatch(v)) {
      throw CatalogException(
        CatalogError.validation,
        'Barcode must be 4-64 characters: letters, digits or hyphen.',
      );
    }
    return v;
  }

  static String name(String value) {
    final v = value.trim();
    if (v.isEmpty || v.length > 160) {
      throw CatalogException(
        CatalogError.validation,
        'Name must be between 1 and 160 characters.',
      );
    }
    return v;
  }

  static int priceE4(int value, String label) {
    if (value < 0) {
      throw CatalogException(
        CatalogError.validation,
        '$label cannot be negative.',
      );
    }
    return value;
  }
}

abstract class CatalogRepository {
  Future<List<CatalogUnit>> listUnits();
  Future<CatalogUnit> createUnit({required String code, required String name});
  Future<CatalogUnit> updateUnit(
    String id, {
    required String code,
    required String name,
    required int expectedVersion,
  });
  Future<void> deleteUnit(String id, {required int expectedVersion});

  Future<List<CatalogProduct>> listProducts({String? query});
  Future<CatalogProduct> createProduct({
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    int salePriceE4 = 0,
    int costPriceE4 = 0,
  });
  Future<CatalogProduct> updateProduct(
    String id, {
    required String sku,
    required String name,
    String? barcode,
    String? unitId,
    required int salePriceE4,
    required int costPriceE4,
    required int expectedVersion,
  });
  Future<void> deleteProduct(String id, {required int expectedVersion});
}
