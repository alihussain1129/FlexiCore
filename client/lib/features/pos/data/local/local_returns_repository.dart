import 'dart:math' as math;

import 'package:drift/drift.dart';

import '../../../../core/ids.dart';
import '../../../../data/local/app_database.dart';
import '../../domain/pos_format.dart';

class ReturnException implements Exception {
  const ReturnException(this.message);

  final String message;

  @override
  String toString() => message;
}

class ReturnLineRequest {
  const ReturnLineRequest({
    required this.saleLineId,
    required this.quantityE4,
    this.restock = true,
  });

  final String saleLineId;
  final int quantityE4;

  /// True: the goods go back on the shelf and stock goes up.
  /// False: the money is refunded but stock is left as it is (damaged goods).
  final bool restock;
}

class ReturnRequest {
  const ReturnRequest({
    required this.clientRequestId,
    required this.saleId,
    required this.lines,
    required this.refundMethod,
    required this.reason,
    required this.createdBy,
  });

  /// The same id twice means the same return: it is applied only once.
  final String clientRequestId;
  final String saleId;
  final List<ReturnLineRequest> lines;
  final String refundMethod;
  final String reason;
  final String createdBy;
}

/// One sale line with what has already been returned.
class ReturnableLine {
  const ReturnableLine({
    required this.saleLineId,
    required this.productId,
    required this.sku,
    required this.name,
    required this.unitPriceE4,
    required this.soldQtyE4,
    required this.soldNetE4,
    required this.soldTaxE4,
    required this.returnedQtyE4,
    required this.returnedNetE4,
    required this.returnedTaxE4,
  });

  final String saleLineId;
  final String productId;
  final String sku;
  final String name;
  final int unitPriceE4;
  final int soldQtyE4;

  /// Line total after discount and before tax, as saved on the sale.
  final int soldNetE4;
  final int soldTaxE4;
  final int returnedQtyE4;
  final int returnedNetE4;
  final int returnedTaxE4;

  int get remainingQtyE4 => soldQtyE4 - returnedQtyE4;
  int get remainingNetE4 => soldNetE4 - returnedNetE4;
  int get remainingTaxE4 => soldTaxE4 - returnedTaxE4;
}

class QuotedLine {
  const QuotedLine({
    required this.saleLineId,
    required this.quantityE4,
    required this.netE4,
    required this.taxE4,
  });

  final String saleLineId;
  final int quantityE4;
  final int netE4;
  final int taxE4;
}

class ReturnQuote {
  const ReturnQuote({
    required this.lines,
    required this.netE4,
    required this.taxE4,
    required this.valueE4,
    required this.refundE4,
    required this.fullyReturned,
  });

  final List<QuotedLine> lines;
  final int netE4;
  final int taxE4;

  /// Value of the goods coming back, tax included.
  final int valueE4;

  /// Money paid out now.
  final int refundE4;

  /// True when nothing on the sale is left to return after this return.
  final bool fullyReturned;

  /// The part of the value that is not paid out: it reduces what the
  /// customer still owes on a part-paid sale.
  int get creditE4 => valueE4 - refundE4;
}

/// A saved sale seen from the returns side. [quote] is pure: the screen and
/// the repository use the same numbers.
class ReturnableSale {
  const ReturnableSale({
    required this.saleId,
    required this.receiptNumber,
    required this.status,
    required this.paymentMethod,
    required this.warehouseId,
    required this.createdAt,
    required this.totalE4,
    required this.paidE4,
    required this.lines,
    required this.previousValueE4,
    required this.previousRefundE4,
  });

  final String saleId;
  final String receiptNumber;
  final String status;
  final String paymentMethod;
  final String warehouseId;
  final DateTime createdAt;
  final int totalE4;
  final int paidE4;
  final List<ReturnableLine> lines;

  /// Goods value and money already returned by earlier returns.
  final int previousValueE4;
  final int previousRefundE4;

  int get outstandingE4 => totalE4 > paidE4 ? totalE4 - paidE4 : 0;

  bool get canReturn =>
      status != 'voided' && lines.any((l) => l.remainingQtyE4 > 0);

  static int _share(int total, int part, int whole) {
    if (whole <= 0) return 0;
    final scaled =
        BigInt.from(total) * BigInt.from(part) * BigInt.two +
        BigInt.from(whole);
    return (scaled ~/ (BigInt.from(whole) * BigInt.two)).toInt();
  }

  ReturnQuote quote(Map<String, int> quantities) {
    final known = <String>{for (final l in lines) l.saleLineId};
    for (final id in quantities.keys) {
      if (!known.contains(id)) {
        throw const ReturnException(
          'A line to return does not belong to this sale.',
        );
      }
    }

    final quoted = <QuotedLine>[];
    var net = 0;
    var tax = 0;
    var done = true;
    for (final line in lines) {
      final q = quantities[line.saleLineId] ?? 0;
      if (q < 0) {
        throw const ReturnException('A quantity cannot be negative.');
      }
      if (q == 0) {
        if (line.remainingQtyE4 > 0) {
          done = false;
        }
        continue;
      }
      if (q > line.remainingQtyE4) {
        throw ReturnException(
          'Only ${PosFormat.plain(line.remainingQtyE4)} of ${line.name} '
          'can still be returned.',
        );
      }
      final int lineNet;
      final int lineTax;
      if (q == line.remainingQtyE4) {
        lineNet = line.remainingNetE4;
        lineTax = line.remainingTaxE4;
      } else {
        done = false;
        lineNet = math.min(
          _share(line.soldNetE4, q, line.soldQtyE4),
          line.remainingNetE4,
        );
        lineTax = math.min(
          _share(line.soldTaxE4, q, line.soldQtyE4),
          line.remainingTaxE4,
        );
      }
      quoted.add(
        QuotedLine(
          saleLineId: line.saleLineId,
          quantityE4: q,
          netE4: lineNet,
          taxE4: lineTax,
        ),
      );
      net += lineNet;
      tax += lineTax;
    }
    if (quoted.isEmpty) {
      throw const ReturnException('Choose at least one item to return.');
    }

    final value = net + tax;
    var refundable = previousValueE4 + value - outstandingE4;
    if (refundable < 0) {
      refundable = 0;
    }
    if (refundable > paidE4) {
      refundable = paidE4;
    }
    var refund = refundable - previousRefundE4;
    if (refund < 0) {
      refund = 0;
    }

    return ReturnQuote(
      lines: quoted,
      netE4: net,
      taxE4: tax,
      valueE4: value,
      refundE4: refund,
      fullyReturned: done,
    );
  }
}

class ReturnReceiptLine {
  const ReturnReceiptLine({
    required this.saleLineId,
    required this.productId,
    required this.sku,
    required this.name,
    required this.quantityE4,
    required this.netE4,
    required this.taxE4,
    required this.restocked,
  });

  final String saleLineId;
  final String productId;
  final String sku;
  final String name;
  final int quantityE4;
  final int netE4;
  final int taxE4;
  final bool restocked;
}

class ReturnReceipt {
  const ReturnReceipt({
    required this.id,
    required this.returnNumber,
    required this.saleId,
    required this.saleReceiptNumber,
    required this.createdAt,
    required this.createdBy,
    required this.reason,
    required this.refundMethod,
    required this.netE4,
    required this.taxE4,
    required this.totalE4,
    required this.refundE4,
    required this.lines,
  });

  final String id;
  final String returnNumber;
  final String saleId;
  final String saleReceiptNumber;
  final DateTime createdAt;
  final String createdBy;
  final String reason;
  final String refundMethod;
  final int netE4;
  final int taxE4;
  final int totalE4;
  final int refundE4;
  final List<ReturnReceiptLine> lines;

  int get creditE4 => totalE4 - refundE4;
}

class ReturnsSummary {
  const ReturnsSummary({
    required this.count,
    required this.netE4,
    required this.taxE4,
    required this.valueE4,
    required this.refundE4,
    required this.refundsByMethod,
  });

  static const ReturnsSummary empty = ReturnsSummary(
    count: 0,
    netE4: 0,
    taxE4: 0,
    valueE4: 0,
    refundE4: 0,
    refundsByMethod: <String, int>{},
  );

  final int count;
  final int netE4;
  final int taxE4;

  /// Goods value returned, tax included.
  final int valueE4;

  /// Money paid out.
  final int refundE4;
  final Map<String, int> refundsByMethod;
}

/// Returns and refunds against saved sales.
///
/// A return never edits the sale. It writes a return document, its lines, an
/// optional stock receipt and the new sale status in one transaction.
class LocalReturnsRepository {
  LocalReturnsRepository(
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

  /// The sale with everything already returned. Read only.
  Future<ReturnableSale> returnable(String saleId) async {
    final sale =
        await (_db.select(_db.sales)..where(
              (s) => s.id.equals(saleId) & s.companyId.equals(companyId),
            ))
            .getSingleOrNull();
    if (sale == null) {
      throw const ReturnException('The sale could not be found.');
    }

    final rows =
        await (_db.select(_db.saleLines)
              ..where((l) => l.saleId.equals(saleId))
              ..orderBy([(l) => OrderingTerm.asc(l.nameSnapshot)]))
            .get();

    final returnedQty = <String, int>{};
    final returnedNet = <String, int>{};
    final returnedTax = <String, int>{};
    if (rows.isNotEmpty) {
      final prior = await (_db.select(
        _db.saleReturnLines,
      )..where((l) => l.saleLineId.isIn(rows.map((r) => r.id).toList()))).get();
      for (final p in prior) {
        returnedQty[p.saleLineId] =
            (returnedQty[p.saleLineId] ?? 0) + p.quantityE4;
        returnedNet[p.saleLineId] = (returnedNet[p.saleLineId] ?? 0) + p.netE4;
        returnedTax[p.saleLineId] = (returnedTax[p.saleLineId] ?? 0) + p.taxE4;
      }
    }

    final earlier = await (_db.select(
      _db.saleReturns,
    )..where((r) => r.saleId.equals(saleId))).get();
    var previousValue = 0;
    var previousRefund = 0;
    for (final r in earlier) {
      previousValue += r.totalE4;
      previousRefund += r.refundE4;
    }

    return ReturnableSale(
      saleId: sale.id,
      receiptNumber: sale.receiptNumber,
      status: sale.status,
      paymentMethod: sale.paymentMethod,
      warehouseId: sale.warehouseId,
      createdAt: sale.createdAt,
      totalE4: sale.totalE4,
      paidE4: sale.amountPaidE4,
      previousValueE4: previousValue,
      previousRefundE4: previousRefund,
      lines: [
        for (final l in rows)
          ReturnableLine(
            saleLineId: l.id,
            productId: l.productId,
            sku: l.skuSnapshot,
            name: l.nameSnapshot,
            unitPriceE4: l.unitPriceE4,
            soldQtyE4: l.quantityE4,
            soldNetE4: l.lineTotalE4,
            soldTaxE4: l.taxE4,
            returnedQtyE4: returnedQty[l.id] ?? 0,
            returnedNetE4: returnedNet[l.id] ?? 0,
            returnedTaxE4: returnedTax[l.id] ?? 0,
          ),
      ],
    );
  }

  Future<ReturnReceipt> processReturn(ReturnRequest request) async {
    final requestId = request.clientRequestId.trim();
    final method = request.refundMethod.trim();
    final reason = request.reason.trim();
    final cashier = request.createdBy.trim();

    if (requestId.isEmpty || requestId.length > 80) {
      throw const ReturnException(
        'The return request ID must be 1 to 80 characters.',
      );
    }
    if (method.isEmpty || method.length > 40) {
      throw const ReturnException('Enter a valid refund method.');
    }
    if (reason.isEmpty || reason.length > 120) {
      throw const ReturnException(
        'Enter the reason for the return (up to 120 characters).',
      );
    }
    if (cashier.isEmpty || cashier.length > 120) {
      throw const ReturnException('Enter a valid cashier name.');
    }
    if (request.lines.isEmpty) {
      throw const ReturnException('Choose at least one item to return.');
    }

    final selection = <String, int>{};
    final restockById = <String, bool>{};
    for (final line in request.lines) {
      if (line.saleLineId.trim().isEmpty) {
        throw const ReturnException('A returned line has no sale line.');
      }
      if (line.quantityE4 <= 0) {
        throw const ReturnException(
          'Each returned line needs a positive quantity.',
        );
      }
      if (selection.containsKey(line.saleLineId)) {
        throw const ReturnException(
          'A line appears more than once. Combine its quantity.',
        );
      }
      selection[line.saleLineId] = line.quantityE4;
      restockById[line.saleLineId] = line.restock;
    }

    return _db.transaction(() async {
      final existing =
          await (_db.select(_db.saleReturns)..where(
                (r) =>
                    r.companyId.equals(companyId) &
                    r.clientRequestId.equals(requestId),
              ))
              .getSingleOrNull();
      if (existing != null) {
        await _verifyExisting(
          existing,
          saleId: request.saleId,
          method: method,
          selection: selection,
          restockById: restockById,
        );
        return receiptForReturn(existing.id);
      }

      final sale = await returnable(request.saleId);
      if (sale.status == 'voided') {
        throw const ReturnException('A voided sale cannot be returned.');
      }
      final quote = sale.quote(selection);

      final now = _clock().toUtc();
      final returnId = _newId();
      final returnNumber =
          'RT-${now.microsecondsSinceEpoch}-${returnId.substring(0, 8)}';

      await _db
          .into(_db.saleReturns)
          .insert(
            SaleReturnsCompanion.insert(
              id: returnId,
              companyId: companyId,
              saleId: sale.saleId,
              returnNumber: returnNumber,
              clientRequestId: requestId,
              netE4: quote.netE4,
              taxE4: quote.taxE4,
              totalE4: quote.valueE4,
              refundE4: quote.refundE4,
              refundMethod: method,
              reason: reason,
              createdAt: now,
              createdBy: cashier,
              originDeviceId: Value(deviceId),
            ),
          );

      final byId = <String, ReturnableLine>{
        for (final l in sale.lines) l.saleLineId: l,
      };
      for (var i = 0; i < quote.lines.length; i++) {
        final q = quote.lines[i];
        final line = byId[q.saleLineId]!;
        final restock = restockById[q.saleLineId] ?? true;

        String? movementId;
        if (restock) {
          movementId = _newId();
          final issues =
              await (_db.select(_db.stockMovements)..where(
                    (m) =>
                        m.companyId.equals(companyId) &
                        m.sourceType.equals('sale') &
                        m.sourceId.equals(sale.saleId) &
                        m.productId.equals(line.productId),
                  ))
                  .get();
          final int? cost = issues.isEmpty ? null : issues.first.unitCostE4;

          await _db
              .into(_db.stockMovements)
              .insert(
                StockMovementsCompanion.insert(
                  id: movementId,
                  companyId: companyId,
                  productId: line.productId,
                  warehouseId: sale.warehouseId,
                  movementType: 'receipt',
                  qtyE4: q.quantityE4,
                  unitCostE4: Value(cost),
                  reason: const Value('Customer return'),
                  sourceType: const Value('sale_return'),
                  sourceId: Value(returnId),
                  clientRequestId: 'return:$returnId:$i',
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
                        b.productId.equals(line.productId) &
                        b.warehouseId.equals(sale.warehouseId),
                  ))
                  .getSingleOrNull();
          await _db
              .into(_db.stockBalances)
              .insertOnConflictUpdate(
                StockBalancesCompanion.insert(
                  companyId: companyId,
                  productId: line.productId,
                  warehouseId: sale.warehouseId,
                  qtyE4: (balance?.qtyE4 ?? 0) + q.quantityE4,
                  updatedAt: now,
                ),
              );
        }

        await _db
            .into(_db.saleReturnLines)
            .insert(
              SaleReturnLinesCompanion.insert(
                id: _newId(),
                returnId: returnId,
                saleLineId: line.saleLineId,
                productId: line.productId,
                skuSnapshot: line.sku,
                nameSnapshot: line.name,
                quantityE4: q.quantityE4,
                netE4: q.netE4,
                taxE4: q.taxE4,
                disposition: restock ? 'restock' : 'discard',
                stockMovementId: Value(movementId),
              ),
            );
      }

      await (_db.update(
        _db.sales,
      )..where((s) => s.id.equals(sale.saleId))).write(
        SalesCompanion(
          status: Value(
            quote.fullyReturned ? 'returned' : 'partially_returned',
          ),
        ),
      );

      return receiptForReturn(returnId);
    });
  }

  Future<void> _verifyExisting(
    SaleReturnRow existing, {
    required String saleId,
    required String method,
    required Map<String, int> selection,
    required Map<String, bool> restockById,
  }) async {
    const mismatch = ReturnException(
      'This return request ID was already used with different details.',
    );
    if (existing.saleId != saleId || existing.refundMethod != method) {
      throw mismatch;
    }
    final stored = await (_db.select(
      _db.saleReturnLines,
    )..where((l) => l.returnId.equals(existing.id))).get();
    if (stored.length != selection.length) {
      throw mismatch;
    }
    for (final l in stored) {
      final q = selection[l.saleLineId];
      if (q == null || q != l.quantityE4) {
        throw mismatch;
      }
      final restock = restockById[l.saleLineId] ?? true;
      if ((l.disposition == 'restock') != restock) {
        throw mismatch;
      }
    }
  }

  Future<ReturnReceipt> receiptForReturn(String returnId) async {
    final row =
        await (_db.select(_db.saleReturns)..where(
              (r) => r.id.equals(returnId) & r.companyId.equals(companyId),
            ))
            .getSingleOrNull();
    if (row == null) {
      throw const ReturnException('The return could not be found.');
    }
    return (await _toReceipts([row])).single;
  }

  /// Returns made against one sale, oldest first.
  Future<List<ReturnReceipt>> returnsForSale(String saleId) async {
    final rows =
        await (_db.select(_db.saleReturns)
              ..where(
                (r) => r.saleId.equals(saleId) & r.companyId.equals(companyId),
              )
              ..orderBy([(r) => OrderingTerm.asc(r.createdAt)]))
            .get();
    return _toReceipts(rows);
  }

  /// Totals of returns made in a period (by the day the return was made).
  Future<ReturnsSummary> summary({
    DateTime? from,
    DateTime? toExclusive,
    String? refundMethod,
  }) async {
    final start = from;
    final end = toExclusive;
    final method = refundMethod?.trim() ?? '';
    final query = _db.select(_db.saleReturns);
    query.where((r) {
      Expression<bool> e = r.companyId.equals(companyId);
      if (start != null) {
        e = e & r.createdAt.isBiggerOrEqualValue(start);
      }
      if (end != null) {
        e = e & r.createdAt.isSmallerThanValue(end);
      }
      if (method.isNotEmpty) {
        e = e & r.refundMethod.equals(method);
      }
      return e;
    });
    final rows = await query.get();

    var net = 0;
    var tax = 0;
    var value = 0;
    var refund = 0;
    final byMethod = <String, int>{};
    for (final r in rows) {
      net += r.netE4;
      tax += r.taxE4;
      value += r.totalE4;
      refund += r.refundE4;
      if (r.refundE4 > 0) {
        byMethod[r.refundMethod] = (byMethod[r.refundMethod] ?? 0) + r.refundE4;
      }
    }
    return ReturnsSummary(
      count: rows.length,
      netE4: net,
      taxE4: tax,
      valueE4: value,
      refundE4: refund,
      refundsByMethod: byMethod,
    );
  }

  Future<List<ReturnReceipt>> _toReceipts(List<SaleReturnRow> rows) async {
    if (rows.isEmpty) {
      return const <ReturnReceipt>[];
    }
    final ids = rows.map((r) => r.id).toList();
    final lineRows =
        await (_db.select(_db.saleReturnLines)
              ..where((l) => l.returnId.isIn(ids))
              ..orderBy([(l) => OrderingTerm.asc(l.nameSnapshot)]))
            .get();
    final saleIds = rows.map((r) => r.saleId).toSet().toList();
    final sales = await (_db.select(
      _db.sales,
    )..where((s) => s.id.isIn(saleIds))).get();
    final numberBySale = <String, String>{
      for (final s in sales) s.id: s.receiptNumber,
    };
    return [
      for (final r in rows)
        ReturnReceipt(
          id: r.id,
          returnNumber: r.returnNumber,
          saleId: r.saleId,
          saleReceiptNumber: numberBySale[r.saleId] ?? '',
          createdAt: r.createdAt,
          createdBy: r.createdBy,
          reason: r.reason,
          refundMethod: r.refundMethod,
          netE4: r.netE4,
          taxE4: r.taxE4,
          totalE4: r.totalE4,
          refundE4: r.refundE4,
          lines: [
            for (final l in lineRows)
              if (l.returnId == r.id)
                ReturnReceiptLine(
                  saleLineId: l.saleLineId,
                  productId: l.productId,
                  sku: l.skuSnapshot,
                  name: l.nameSnapshot,
                  quantityE4: l.quantityE4,
                  netE4: l.netE4,
                  taxE4: l.taxE4,
                  restocked: l.disposition == 'restock',
                ),
          ],
        ),
    ];
  }
}
