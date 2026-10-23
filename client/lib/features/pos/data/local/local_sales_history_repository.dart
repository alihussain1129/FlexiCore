import 'dart:math' as math;

import 'package:drift/drift.dart';

import '../../../../data/local/app_database.dart';
import 'local_pos_repository.dart';

/// What to look for in the sales history. Every field is optional.
class SalesFilter {
  const SalesFilter({
    this.text,
    this.from,
    this.toExclusive,
    this.paymentMethod,
    this.status,
  });

  /// Matches the receipt number, the cashier, or a product name or SKU on
  /// any line of the sale. Not case sensitive. Every character is literal.
  final String? text;

  /// Sales created at or after this time.
  final DateTime? from;

  /// Sales created strictly before this time.
  final DateTime? toExclusive;
  final String? paymentMethod;
  final String? status;
}

class MethodTotal {
  const MethodTotal({
    required this.method,
    required this.paymentCount,
    required this.amountE4,
  });

  final String method;
  final int paymentCount;
  final int amountE4;
}

class SalesSummary {
  const SalesSummary({
    required this.saleCount,
    required this.subtotalE4,
    required this.discountE4,
    required this.taxE4,
    required this.totalE4,
    required this.paidE4,
    required this.outstandingE4,
    required this.excludedCount,
    required this.byMethod,
  });

  static const SalesSummary empty = SalesSummary(
    saleCount: 0,
    subtotalE4: 0,
    discountE4: 0,
    taxE4: 0,
    totalE4: 0,
    paidE4: 0,
    outstandingE4: 0,
    excludedCount: 0,
    byMethod: <MethodTotal>[],
  );

  /// Sales counted in the totals (not voided).
  final int saleCount;
  final int subtotalE4;
  final int discountE4;
  final int taxE4;
  final int totalE4;
  final int paidE4;

  /// Total minus paid, never negative.
  final int outstandingE4;

  /// Voided or fully returned sales that match the filter but are not in
  /// the totals.
  final int excludedCount;
  final List<MethodTotal> byMethod;
}

class DailyTotal {
  const DailyTotal({
    required this.day,
    required this.saleCount,
    required this.totalE4,
    required this.paidE4,
  });

  final DateTime day;
  final int saleCount;
  final int totalE4;
  final int paidE4;
}

class SalePaymentInfo {
  const SalePaymentInfo({
    required this.method,
    required this.amountE4,
    required this.receivedAt,
  });

  final String method;
  final int amountE4;
  final DateTime receivedAt;
}

class SaleDetail {
  const SaleDetail({
    required this.receipt,
    required this.payments,
    required this.createdBy,
    this.discountReason,
  });

  final PosReceipt receipt;
  final List<SalePaymentInfo> payments;
  final String createdBy;
  final String? discountReason;
}

/// Read-only queries over saved sales. It never writes, so history can never
/// change a posted sale, its lines, or its payments.
class LocalSalesHistoryRepository {
  LocalSalesHistoryRepository(AppDatabase db, {required String companyId})
    : _db = db,
      companyId = companyId,
      _pos = LocalPosRepository(db, companyId: companyId);

  final AppDatabase _db;
  final String companyId;
  final LocalPosRepository _pos;

  static bool _excluded(String status) =>
      status == 'voided';

  /// Company, date, method and status filters run in SQL. Text matching runs
  /// in Dart so it is case-insensitive and every character is literal.
  Expression<bool> _predicate(Sales s, SalesFilter f) {
    Expression<bool> e = s.companyId.equals(companyId);
    if (f.from != null) e = e & s.createdAt.isBiggerOrEqualValue(f.from!);
    if (f.toExclusive != null) {
      e = e & s.createdAt.isSmallerThanValue(f.toExclusive!);
    }
    final method = f.paymentMethod?.trim() ?? '';
    if (method.isNotEmpty) e = e & s.paymentMethod.equals(method);
    final status = f.status?.trim() ?? '';
    if (status.isNotEmpty) e = e & s.status.equals(status);
    return e;
  }

  SimpleSelectStatement<$SalesTable, SaleRow> _ordered(SalesFilter f) {
    return _db.select(_db.sales)
      ..where((s) => _predicate(s, f))
      ..orderBy([
        (s) => OrderingTerm.desc(s.createdAt),
        (s) => OrderingTerm.desc(s.receiptNumber),
      ]);
  }

  /// Every sale that matches the filter, newest first.
  Future<List<SaleRow>> _allRows(SalesFilter filter) async {
    final rows = await _ordered(filter).get();
    final t = filter.text?.trim().toLowerCase() ?? '';
    if (t.isEmpty) return rows;

    final matched = <String>{};
    final rest = <SaleRow>[];
    for (final s in rows) {
      if (s.receiptNumber.toLowerCase().contains(t) ||
          s.createdBy.toLowerCase().contains(t)) {
        matched.add(s.id);
      } else {
        rest.add(s);
      }
    }
    for (var i = 0; i < rest.length; i += 500) {
      final chunk = rest
          .sublist(i, math.min(i + 500, rest.length))
          .map((s) => s.id)
          .toList();
      final lines = await (_db.select(
        _db.saleLines,
      )..where((l) => l.saleId.isIn(chunk))).get();
      for (final l in lines) {
        if (l.nameSnapshot.toLowerCase().contains(t) ||
            l.skuSnapshot.toLowerCase().contains(t)) {
          matched.add(l.saleId);
        }
      }
    }
    return rows.where((s) => matched.contains(s.id)).toList();
  }

  /// Newest first. Use [limit] and [offset] to page.
  Future<List<SaleRow>> search(
    SalesFilter filter, {
    int limit = 50,
    int offset = 0,
  }) async {
    final t = filter.text?.trim() ?? '';
    if (t.isEmpty) {
      final query = _ordered(filter)..limit(limit, offset: offset);
      return query.get();
    }
    final rows = await _allRows(filter);
    return rows.skip(offset).take(limit).toList();
  }

  Future<SalesSummary> summary(SalesFilter filter) async {
    final rows = await _allRows(filter);
    var count = 0;
    var subtotal = 0;
    var discount = 0;
    var tax = 0;
    var total = 0;
    var paid = 0;
    var excluded = 0;
    final ids = <String>[];
    for (final s in rows) {
      if (_excluded(s.status)) {
        excluded++;
        continue;
      }
      count++;
      subtotal += s.subtotalE4;
      discount += s.discountE4;
      tax += s.taxE4;
      total += s.totalE4;
      paid += s.amountPaidE4;
      ids.add(s.id);
    }

    final counts = <String, int>{};
    final amounts = <String, int>{};
    for (var i = 0; i < ids.length; i += 500) {
      final chunk = ids.sublist(i, math.min(i + 500, ids.length));
      final payments = await (_db.select(
        _db.salePayments,
      )..where((p) => p.saleId.isIn(chunk))).get();
      for (final p in payments) {
        counts[p.method] = (counts[p.method] ?? 0) + 1;
        amounts[p.method] = (amounts[p.method] ?? 0) + p.amountE4;
      }
    }
    final byMethod = [
      for (final m in amounts.keys)
        MethodTotal(
          method: m,
          paymentCount: counts[m] ?? 0,
          amountE4: amounts[m] ?? 0,
        ),
    ]..sort((a, b) => b.amountE4.compareTo(a.amountE4));

    return SalesSummary(
      saleCount: count,
      subtotalE4: subtotal,
      discountE4: discount,
      taxE4: tax,
      totalE4: total,
      paidE4: paid,
      outstandingE4: total > paid ? total - paid : 0,
      excludedCount: excluded,
      byMethod: byMethod,
    );
  }

  /// One entry per local calendar day, newest day first.
  Future<List<DailyTotal>> dailyTotals(SalesFilter filter) async {
    final rows = await _allRows(filter);
    final days = <DateTime, List<int>>{};
    for (final s in rows) {
      if (_excluded(s.status)) continue;
      final t = s.createdAt.toLocal();
      final key = DateTime(t.year, t.month, t.day);
      final acc = days.putIfAbsent(key, () => <int>[0, 0, 0]);
      acc[0] += 1;
      acc[1] += s.totalE4;
      acc[2] += s.amountPaidE4;
    }
    final keys = days.keys.toList()..sort((a, b) => b.compareTo(a));
    return [
      for (final k in keys)
        DailyTotal(
          day: k,
          saleCount: days[k]![0],
          totalE4: days[k]![1],
          paidE4: days[k]![2],
        ),
    ];
  }

  /// The sale exactly as it was saved, plus its payments.
  Future<SaleDetail> detail(String saleId) async {
    final receipt = await _pos.receiptForSale(saleId);
    final sale =
        await (_db.select(_db.sales)..where(
              (s) => s.id.equals(saleId) & s.companyId.equals(companyId),
            ))
            .getSingle();
    final payments =
        await (_db.select(_db.salePayments)
              ..where((p) => p.saleId.equals(saleId))
              ..orderBy([(p) => OrderingTerm.asc(p.receivedAt)]))
            .get();
    return SaleDetail(
      receipt: receipt,
      createdBy: sale.createdBy,
      discountReason: sale.discountReason,
      payments: [
        for (final p in payments)
          SalePaymentInfo(
            method: p.method,
            amountE4: p.amountE4,
            receivedAt: p.receivedAt,
          ),
      ],
    );
  }
}
