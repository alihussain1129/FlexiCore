/// Domain layer for local inventory. No database types leak out of here.
library;

/// One whole unit is 10,000 in the integer "E4" representation used for
/// quantities and money. No floating point is used for stock or money.
const int kE4 = 10000;

/// Largest quantity of a single movement: 10,000,000 units.
const int kMaxQuantityE4 = 100000000000;

/// Largest unit cost: 100,000,000.
const int kMaxCostE4 = 1000000000000;

const int kMaxReasonLength = 200;

class InventoryException implements Exception {
  const InventoryException(this.message);

  final String message;

  @override
  String toString() => message;
}

enum StockMovementType {
  opening,
  receipt,
  issue,
  adjustment,
  reversal;

  String get dbValue => name;

  String get label => switch (this) {
    StockMovementType.opening => 'Opening stock',
    StockMovementType.receipt => 'Received',
    StockMovementType.issue => 'Issued',
    StockMovementType.adjustment => 'Adjustment',
    StockMovementType.reversal => 'Reversal',
  };

  static StockMovementType fromDb(String value) {
    for (final t in StockMovementType.values) {
      if (t.name == value) return t;
    }
    throw InventoryException('Unknown movement type "$value".');
  }
}

/// Parses "12", "12.5" or "0.0001" into E4 units. Returns null if invalid.
/// A comma is rejected on purpose: "1,5" is ambiguous and must not become 15.
int? parseQtyE4(String input) {
  final m = RegExp(r'^(\d{1,12})(?:\.(\d{1,4}))?$').firstMatch(input.trim());
  if (m == null) return null;
  final whole = int.parse(m.group(1)!);
  final fraction = int.parse((m.group(2) ?? '').padRight(4, '0'));
  return whole * kE4 + fraction;
}

/// Parses an optional unit cost into the inventory E4 integer representation.
/// Accepts ungrouped digits or correctly grouped thousands (for example 3,000.50).
int? parseUnitCostE4(String input) {
  final text = input.trim();
  if (text.isEmpty) return null;

  final valid = RegExp(
    r'^(?:\d{1,12}|\d{1,3}(?:,\d{3})+)(?:\.\d{1,4})?$',
  );
  if (!valid.hasMatch(text)) return null;

  final normalized = text.replaceAll(',', '');
  final parts = normalized.split('.');
  final whole = BigInt.tryParse(parts[0]);
  if (whole == null) return null;

  final fractionText = parts.length == 2 ? parts[1].padRight(4, '0') : '0000';
  final fraction = BigInt.tryParse(fractionText);
  if (fraction == null) return null;

  final result = whole * BigInt.from(kE4) + fraction;
  if (result > BigInt.from(0x7FFFFFFFFFFFFFFF)) return null;
  return result.toInt();
}
String formatQtyE4(int value, {int minDecimals = 0}) =>
    formatE4Big(BigInt.from(value), minDecimals: minDecimals);

String formatE4Big(BigInt value, {int minDecimals = 0}) {
  final negative = value.isNegative;
  final abs = value.abs();
  final whole = abs ~/ BigInt.from(kE4);
  var fraction = (abs % BigInt.from(kE4)).toString().padLeft(4, '0');
  var end = 4;
  while (end > minDecimals && fraction[end - 1] == '0') {
    end--;
  }
  fraction = fraction.substring(0, end);
  final grouped = whole.toString().replaceAllMapped(
    RegExp(r'\B(?=(\d{3})+(?!\d))'),
    (_) => ',',
  );
  return '${negative ? '-' : ''}$grouped${fraction.isEmpty ? '' : '.$fraction'}';
}

String formatTimestamp(DateTime d) {
  String two(int n) => n.toString().padLeft(2, '0');
  return '${d.year}-${two(d.month)}-${two(d.day)} ${two(d.hour)}:${two(d.minute)}';
}

class Warehouse {
  const Warehouse({
    required this.id,
    required this.code,
    required this.name,
    required this.isDefault,
  });

  final String id;
  final String code;
  final String name;
  final bool isDefault;
}

class StockLine {
  const StockLine({
    required this.productId,
    required this.sku,
    required this.name,
    required this.onHandE4,
    required this.costPriceE4,
    this.barcode,
    this.unitCode,
  });

  final String productId;
  final String sku;
  final String name;
  final String? barcode;
  final String? unitCode;
  final int onHandE4;
  final int costPriceE4;

  bool get outOfStock => onHandE4 <= 0;
}

class StockSummary {
  StockSummary({
    this.productCount = 0,
    this.stockedCount = 0,
    this.outOfStockCount = 0,
    this.movementCount = 0,
    this.lastMovementAt,
    BigInt? valueE4,
  }) : valueE4 = valueE4 ?? BigInt.zero;

  final int productCount;
  final int stockedCount;
  final int outOfStockCount;
  final int movementCount;
  final DateTime? lastMovementAt;

  /// Sum over products of (quantity on hand x product cost price), in E4.
  /// This is not a valuation by receipt cost.
  final BigInt valueE4;
}

class MovementView {
  const MovementView({
    required this.id,
    required this.productId,
    required this.productName,
    required this.sku,
    required this.warehouseName,
    required this.type,
    required this.qtyE4,
    required this.occurredAt,
    required this.createdBy,
    required this.isReversed,
    this.unitCostE4,
    this.reason,
    this.reversesMovementId,
  });

  final String id;
  final String productId;
  final String productName;
  final String sku;
  final String warehouseName;
  final StockMovementType type;
  final int qtyE4;
  final int? unitCostE4;
  final String? reason;
  final DateTime occurredAt;
  final String createdBy;
  final String? reversesMovementId;
  final bool isReversed;

  bool get canReverse => type != StockMovementType.reversal && !isReversed;
}

class MovementRequest {
  const MovementRequest({
    required this.productId,
    required this.warehouseId,
    required this.type,
    required this.quantityE4,
    required this.requestId,
    required this.actor,
    this.unitCostE4,
    this.reason,
    this.occurredAt,
  });

  final String productId;
  final String warehouseId;
  final StockMovementType type;

  /// Receipt, opening and issue: a positive amount. Adjustment: signed.
  final int quantityE4;
  final int? unitCostE4;
  final String? reason;

  /// The same id twice means the same request: it is applied only once.
  final String requestId;
  final String actor;
  final DateTime? occurredAt;
}

class InventoryRules {
  InventoryRules._();

  /// The quantity as it is stored in the ledger.
  static int signedQuantity(StockMovementType type, int quantityE4) =>
      type == StockMovementType.issue ? -quantityE4 : quantityE4;

  static void validate(MovementRequest r) {
    if (r.productId.isEmpty || r.warehouseId.isEmpty) {
      throw const InventoryException('Choose a product and a warehouse.');
    }
    if (r.requestId.isEmpty || r.requestId.length > 80) {
      throw const InventoryException('The request id is missing or too long.');
    }
    final actor = r.actor.trim();
    if (actor.isEmpty || actor.length > 120) {
      throw const InventoryException(
        'The person making the change is unknown.',
      );
    }
    final q = r.quantityE4;
    if (q == 0) throw const InventoryException('Quantity must not be zero.');
    if (q.abs() > kMaxQuantityE4) {
      throw const InventoryException('Quantity is too large.');
    }
    switch (r.type) {
      case StockMovementType.receipt:
      case StockMovementType.opening:
      case StockMovementType.issue:
        if (q < 0) {
          throw const InventoryException('Quantity must be greater than zero.');
        }
      case StockMovementType.adjustment:
        if ((r.reason ?? '').trim().isEmpty) {
          throw const InventoryException(
            'Please give a reason for the adjustment.',
          );
        }
      case StockMovementType.reversal:
        throw const InventoryException(
          'Reversals are created from an existing movement.',
        );
    }
    final cost = r.unitCostE4;
    if (cost != null) {
      if (r.type != StockMovementType.receipt &&
          r.type != StockMovementType.opening) {
        throw const InventoryException(
          'Unit cost applies to received stock only.',
        );
      }
      if (cost < 0 || cost > kMaxCostE4) {
        throw const InventoryException('Unit cost is out of range.');
      }
    }
    if ((r.reason ?? '').length > kMaxReasonLength) {
      throw const InventoryException('The note is too long.');
    }
    final moment = r.occurredAt;
    if (moment != null) {
      if (moment.isAfter(DateTime.now().add(const Duration(days: 1)))) {
        throw const InventoryException('The date cannot be in the future.');
      }
      if (moment.year < 2000) {
        throw const InventoryException('The date is too old.');
      }
    }
  }
}

abstract class InventoryRepository {
  Future<Warehouse> ensureDefaultWarehouse();
  Future<List<Warehouse>> warehouses();
  Future<List<StockLine>> stockLines({
    required String warehouseId,
    String search = '',
    int limit = 500,
  });
  Future<StockSummary> summary();
  Future<List<MovementView>> recentMovements({int limit = 8});
  Future<List<MovementView>> movementsForProduct(
    String productId, {
    int limit = 100,
  });
  Future<void> record(MovementRequest request);
  Future<void> reverse({
    required String movementId,
    required String requestId,
    required String actor,
    String? reason,
  });

  /// Compares the ledger with the balances. An empty list means consistent.
  Future<List<String>> reconcile();
}
