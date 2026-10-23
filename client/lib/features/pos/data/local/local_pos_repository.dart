import 'package:drift/drift.dart';

import '../../../../core/ids.dart';
import '../../../../data/local/app_database.dart';
import '../../domain/pos_pricing.dart';
import 'local_pos_settings_repository.dart';

class PosException implements Exception {
  const PosException(this.message);

  final String message;

  @override
  String toString() => 'PosException: $message';
}

class PosCartItem {
  const PosCartItem({required this.productId, required this.quantityE4});

  final String productId;

  /// Quantity in E4 units: 1.0000 is 10000.
  final int quantityE4;
}

class PosCheckoutRequest {
  const PosCheckoutRequest({
    required this.clientRequestId,
    required this.items,
    required this.paymentMethod,
    required this.amountPaidE4,
    required this.createdBy,
    this.warehouseId,
    this.orderDiscountE4 = 0,
    this.lineDiscountsE4 = const <String, int>{},
    this.discountReason,
    this.cashTenderedE4,
  });

  final String clientRequestId;
  final List<PosCartItem> items;
  final String paymentMethod;

  /// The amount applied to this sale. It can never be more than the total.
  final int amountPaidE4;
  final String createdBy;
  final String? warehouseId;

  /// Order-level discount as a fixed E4 amount.
  final int orderDiscountE4;

  /// Line-level discounts as fixed E4 amounts, keyed by product id.
  final Map<String, int> lineDiscountsE4;
  final String? discountReason;

  /// Cash handed over by the customer. Change is this minus [amountPaidE4].
  /// Only allowed for the Cash method.
  final int? cashTenderedE4;
}

class PosReceiptLine {
  const PosReceiptLine({
    required this.productId,
    required this.sku,
    required this.name,
    required this.quantityE4,
    required this.unitPriceE4,
    required this.lineTotalE4,
    this.discountE4 = 0,
    this.taxE4 = 0,
  });

  final String productId;
  final String sku;
  final String name;
  final int quantityE4;
  final int unitPriceE4;

  /// Line total after discount and before tax.
  final int lineTotalE4;
  final int discountE4;
  final int taxE4;
}

class PosReceipt {
  const PosReceipt({
    required this.id,
    required this.receiptNumber,
    required this.warehouseId,
    required this.createdAt,
    required this.subtotalE4,
    required this.totalE4,
    required this.amountPaidE4,
    required this.paymentMethod,
    required this.lines,
    this.discountE4 = 0,
    this.taxE4 = 0,
    this.changeE4 = 0,
    this.taxRateBp = 0,
    this.status = 'completed',
  });

  final String id;
  final String receiptNumber;
  final String warehouseId;
  final DateTime createdAt;
  final int subtotalE4;
  final int totalE4;
  final int amountPaidE4;
  final String paymentMethod;
  final List<PosReceiptLine> lines;
  final int discountE4;
  final int taxE4;
  final int changeE4;
  final int taxRateBp;
  final String status;
}

/// Offline checkout repository.
///
/// A checkout writes the sale, its lines, payment, stock ledger movements,
/// and stock balance projections inside one SQLite transaction.
class LocalPosRepository {
  LocalPosRepository(
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

  Future<PosReceipt> checkout(PosCheckoutRequest request) {
    final requestId = request.clientRequestId.trim();
    final method = request.paymentMethod.trim();
    final cashier = request.createdBy.trim();
    final reason = request.discountReason?.trim();

    if (requestId.isEmpty || requestId.length > 80) {
      throw const PosException(
        'Checkout request ID must be 1\u201380 characters.',
      );
    }
    if (method.isEmpty || method.length > 40) {
      throw const PosException('Enter a valid payment method.');
    }
    if (cashier.isEmpty || cashier.length > 120) {
      throw const PosException('Enter a valid cashier name.');
    }
    if (request.items.isEmpty) {
      throw const PosException('Add at least one product to the cart.');
    }
    if (request.amountPaidE4 < 0) {
      throw const PosException('Payment cannot be negative.');
    }

    final seen = <String>{};
    for (final item in request.items) {
      if (item.productId.trim().isEmpty || item.quantityE4 <= 0) {
        throw const PosException(
          'Each cart item needs a product and a positive quantity.',
        );
      }
      if (!seen.add(item.productId)) {
        throw const PosException(
          'A product appears more than once in the cart. Combine its quantity.',
        );
      }
    }

    if (request.orderDiscountE4 < 0) {
      throw const PosException('Discount cannot be negative.');
    }
    for (final entry in request.lineDiscountsE4.entries) {
      if (entry.value < 0) {
        throw const PosException('Discount cannot be negative.');
      }
      if (!seen.contains(entry.key)) {
        throw const PosException(
          'A line discount refers to a product that is not in the cart.',
        );
      }
    }
    if (reason != null && reason.length > 120) {
      throw const PosException('The discount reason is too long.');
    }

    final tendered = request.cashTenderedE4;
    if (tendered != null) {
      if (method.toLowerCase() != 'cash') {
        throw const PosException('Change can only be given for cash payments.');
      }
      if (tendered < request.amountPaidE4) {
        throw const PosException('Cash received is less than the amount paid.');
      }
    }
    final changeE4 = tendered == null ? 0 : tendered - request.amountPaidE4;

    return _db.transaction(() async {
      // Retries with the same request ID return the original receipt. They
      // never write a second sale or deduct stock a second time.
      final existing =
          await (_db.select(_db.sales)..where(
                (s) =>
                    s.companyId.equals(companyId) &
                    s.clientRequestId.equals(requestId),
              ))
              .getSingleOrNull();

      if (existing != null) {
        return _returnExistingOrThrow(
          existing,
          request: request,
          paymentMethod: method,
          changeE4: changeE4,
        );
      }

      final warehouse = await _resolveWarehouse(request.warehouseId);
      final prepared = <({PosCartItem item, ProductRow product})>[];

      // Read and validate the entire cart before writing any sale records.
      for (final item in request.items) {
        final product =
            await (_db.select(_db.products)..where(
                  (p) =>
                      p.id.equals(item.productId) &
                      p.companyId.equals(companyId) &
                      p.deletedAt.isNull(),
                ))
                .getSingleOrNull();

        if (product == null) {
          throw PosException(
            'Product ${item.productId} was not found or is inactive.',
          );
        }
        prepared.add((item: item, product: product));
      }

      final config = await LocalPosSettingsRepository(
        _db,
        companyId: companyId,
      ).load();

      final PosPricing pricing;
      try {
        pricing = PosPricing.calculate(
          lines: [
            for (final entry in prepared)
              PosPriceInput(
                quantityE4: entry.item.quantityE4,
                unitPriceE4: entry.product.salePriceE4,
                lineDiscountE4:
                    request.lineDiscountsE4[entry.item.productId] ?? 0,
              ),
          ],
          orderDiscountE4: request.orderDiscountE4,
          taxRateBp: config.taxRateBp,
        );
      } on PosPricingException catch (e) {
        throw PosException(e.message);
      }

      if (pricing.discountE4 * 10000 >
          pricing.subtotalE4 * config.maxDiscountBp) {
        throw PosException(
          'Discount exceeds the allowed maximum of '
          '${PosPricing.percentLabel(config.maxDiscountBp)}.',
        );
      }

      if (request.amountPaidE4 > pricing.totalE4) {
        throw const PosException('Payment cannot exceed the sale total.');
      }

      // Validate all stock balances before starting the writes. The checks
      // and writes still happen in this same transaction.
      for (final entry in prepared) {
        final balance =
            await (_db.select(_db.stockBalances)..where(
                  (b) =>
                      b.companyId.equals(companyId) &
                      b.productId.equals(entry.product.id) &
                      b.warehouseId.equals(warehouse.id),
                ))
                .getSingleOrNull();

        final onHandE4 = balance?.qtyE4 ?? 0;
        if (entry.item.quantityE4 > onHandE4) {
          throw PosException(
            'Insufficient stock for ${entry.product.name}. '
            'Available: $onHandE4 E4; requested: ${entry.item.quantityE4} E4.',
          );
        }
      }

      final now = _clock().toUtc();
      final saleId = _newId();
      final receiptNumber =
          'FC-${now.microsecondsSinceEpoch}-${saleId.substring(0, 8)}';

      await _db
          .into(_db.sales)
          .insert(
            SalesCompanion.insert(
              id: saleId,
              companyId: companyId,
              receiptNumber: receiptNumber,
              clientRequestId: requestId,
              warehouseId: warehouse.id,
              subtotalE4: pricing.subtotalE4,
              discountE4: Value(pricing.discountE4),
              taxE4: Value(pricing.taxE4),
              totalE4: pricing.totalE4,
              amountPaidE4: request.amountPaidE4,
              paymentMethod: method,
              createdAt: now,
              createdBy: cashier,
              originDeviceId: Value(deviceId),
              changeE4: Value(changeE4),
              taxRateBp: Value(config.taxRateBp),
              discountReason: Value(
                reason == null || reason.isEmpty ? null : reason,
              ),
            ),
          );

      for (var index = 0; index < prepared.length; index++) {
        final entry = prepared[index];
        final priced = pricing.lines[index];
        final movementId = _newId();

        await _db
            .into(_db.saleLines)
            .insert(
              SaleLinesCompanion.insert(
                id: _newId(),
                saleId: saleId,
                productId: entry.product.id,
                skuSnapshot: entry.product.sku,
                nameSnapshot: entry.product.name,
                quantityE4: entry.item.quantityE4,
                unitPriceE4: entry.product.salePriceE4,
                discountE4: Value(priced.discountE4),
                lineTotalE4: priced.netE4,
                taxE4: Value(priced.taxE4),
              ),
            );

        await _db
            .into(_db.stockMovements)
            .insert(
              StockMovementsCompanion.insert(
                id: movementId,
                companyId: companyId,
                productId: entry.product.id,
                warehouseId: warehouse.id,
                movementType: 'issue',
                qtyE4: -entry.item.quantityE4,
                unitCostE4: Value(entry.product.costPriceE4),
                sourceType: const Value('sale'),
                sourceId: Value(saleId),
                clientRequestId: 'sale:$saleId:$index',
                occurredAt: now,
                createdAt: now,
                createdBy: cashier,
                originDeviceId: Value(deviceId),
              ),
            );

        final balance =
            await (_db.select(_db.stockBalances)..where(
                  (b) =>
                      b.companyId.equals(companyId) &
                      b.productId.equals(entry.product.id) &
                      b.warehouseId.equals(warehouse.id),
                ))
                .getSingle();

        await _db
            .into(_db.stockBalances)
            .insertOnConflictUpdate(
              StockBalancesCompanion.insert(
                companyId: companyId,
                productId: entry.product.id,
                warehouseId: warehouse.id,
                qtyE4: balance.qtyE4 - entry.item.quantityE4,
                updatedAt: now,
              ),
            );
      }

      await _db
          .into(_db.salePayments)
          .insert(
            SalePaymentsCompanion.insert(
              id: _newId(),
              saleId: saleId,
              amountE4: request.amountPaidE4,
              method: method,
              receivedAt: now,
            ),
          );

      return receiptForSale(saleId);
    });
  }

  /// Newest sales first. Use [limit] and [offset] to page through history.
  Future<List<SaleRow>> recentSales({int limit = 50, int offset = 0}) {
    final query = _db.select(_db.sales)
      ..where((s) => s.companyId.equals(companyId))
      ..orderBy([(s) => OrderingTerm.desc(s.createdAt)])
      ..limit(limit, offset: offset);
    return query.get();
  }

  Future<WarehouseRow> _resolveWarehouse(String? requestedId) async {
    final query = _db.select(_db.warehouses)
      ..where(
        (w) =>
            w.companyId.equals(companyId) &
            w.deletedAt.isNull() &
            (requestedId != null
                ? w.id.equals(requestedId)
                : w.isDefault.equals(true)),
      );

    final warehouse = await query.getSingleOrNull();
    if (warehouse == null) {
      throw const PosException(
        'No matching active warehouse was found. Configure a default warehouse first.',
      );
    }
    return warehouse;
  }

  Future<PosReceipt> _returnExistingOrThrow(
    SaleRow existing, {
    required PosCheckoutRequest request,
    required String paymentMethod,
    required int changeE4,
  }) async {
    final requestedDiscountE4 =
        request.orderDiscountE4 +
        request.lineDiscountsE4.values.fold<int>(0, (a, b) => a + b);

    final requestedReason = request.discountReason?.trim();
    final normalizedReason =
        requestedReason == null || requestedReason.isEmpty
        ? null
        : requestedReason;

    if (existing.paymentMethod != paymentMethod ||
        existing.amountPaidE4 != request.amountPaidE4 ||
        existing.discountE4 != requestedDiscountE4 ||
        existing.changeE4 != changeE4 ||
        existing.createdBy != request.createdBy.trim() ||
        existing.discountReason != normalizedReason ||
        (request.warehouseId != null &&
            existing.warehouseId != request.warehouseId)) {
      throw const PosException(
        'This checkout request ID was already used with different details.',
      );
    }

    final storedLines = await (_db.select(
      _db.saleLines,
    )..where((l) => l.saleId.equals(existing.id))).get();

    if (storedLines.length != request.items.length) {
      throw const PosException(
        'This checkout request ID was already used for a different cart.',
      );
    }

    for (final item in request.items) {
      final matching = storedLines.where(
        (line) => line.productId == item.productId,
      );
      if (matching.length != 1 ||
          matching.single.quantityE4 != item.quantityE4 ||
          matching.single.discountE4 !=
              (request.lineDiscountsE4[item.productId] ?? 0)) {
        throw const PosException(
          'This checkout request ID was already used for a different cart.',
        );
      }
    }

    return receiptForSale(existing.id);
  }

  Future<PosReceipt> receiptForSale(String saleId) async {
    final sale =
        await (_db.select(_db.sales)..where(
              (s) => s.id.equals(saleId) & s.companyId.equals(companyId),
            ))
            .getSingleOrNull();

    if (sale == null) {
      throw const PosException('The saved sale could not be found.');
    }

    final rows = await (_db.select(
      _db.saleLines,
    )..where((l) => l.saleId.equals(saleId))).get();

    return PosReceipt(
      id: sale.id,
      receiptNumber: sale.receiptNumber,
      warehouseId: sale.warehouseId,
      createdAt: sale.createdAt,
      subtotalE4: sale.subtotalE4,
      totalE4: sale.totalE4,
      amountPaidE4: sale.amountPaidE4,
      paymentMethod: sale.paymentMethod,
      discountE4: sale.discountE4,
      taxE4: sale.taxE4,
      changeE4: sale.changeE4,
      taxRateBp: sale.taxRateBp,
      status: sale.status,
      lines: rows
          .map(
            (line) => PosReceiptLine(
              productId: line.productId,
              sku: line.skuSnapshot,
              name: line.nameSnapshot,
              quantityE4: line.quantityE4,
              unitPriceE4: line.unitPriceE4,
              lineTotalE4: line.lineTotalE4,
              discountE4: line.discountE4,
              taxE4: line.taxE4,
            ),
          )
          .toList(),
    );
  }
}
