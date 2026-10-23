import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_repository.dart';
import 'package:flexicore_client/features/pos/data/local/local_sales_history_repository.dart';
import 'package:flexicore_client/features/pos/presentation/sales_history_screen.dart';

const _company = 'company-1';

Future<void> _seed(AppDatabase db) async {
  final now = DateTime.utc(2026, 10, 1);
  await db
      .into(db.warehouses)
      .insert(
        WarehousesCompanion.insert(
          id: 'w1',
          companyId: _company,
          code: 'MAIN',
          name: 'Main',
          updatedAt: now,
          isDefault: const Value(true),
        ),
      );
  Future<void> product(String id, String sku, String name, int price) => db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: id,
          companyId: _company,
          sku: sku,
          name: name,
          updatedAt: now,
          salePriceE4: Value(price),
          costPriceE4: const Value(100000),
        ),
      );
  await product('p1', 'SKU1', 'Widget', 1000000);
  await product('p2', 'SKU2', 'Gadget', 250000);
  for (final id in ['p1', 'p2']) {
    await db
        .into(db.stockBalances)
        .insert(
          StockBalancesCompanion.insert(
            companyId: _company,
            productId: id,
            warehouseId: 'w1',
            qtyE4: 1000000,
            updatedAt: now,
          ),
        );
  }
}

void main() {
  late AppDatabase db;
  late LocalPosRepository pos;
  late LocalSalesHistoryRepository history;
  late DateTime now;
  var n = 0;

  Future<PosReceipt> sell(
    DateTime at, {
    String product = 'p1',
    int qtyE4 = 10000,
    required int paidE4,
    String method = 'Cash',
    String cashier = 'Ali',
  }) {
    now = at;
    n++;
    return pos.checkout(
      PosCheckoutRequest(
        clientRequestId: 'req-$n',
        items: [PosCartItem(productId: product, quantityE4: qtyE4)],
        paymentMethod: method,
        amountPaidE4: paidE4,
        createdBy: cashier,
      ),
    );
  }

  late PosReceipt a, b, c, d;

  setUp(() async {
    n = 0;
    now = DateTime(2026, 10, 20, 9, 0);
    db = AppDatabase(NativeDatabase.memory());
    await _seed(db);
    pos = LocalPosRepository(db, companyId: _company, clock: () => now);
    history = LocalSalesHistoryRepository(db, companyId: _company);
    a = await sell(DateTime(2026, 10, 20, 9, 5), paidE4: 1000000);
    b = await sell(
      DateTime(2026, 10, 21, 10, 5),
      product: 'p2',
      qtyE4: 20000,
      paidE4: 500000,
      method: 'Card',
      cashier: 'Sara',
    );
    c = await sell(
      DateTime(2026, 10, 21, 15, 5),
      product: 'p2',
      paidE4: 100000,
    );
    d = await sell(
      DateTime(2026, 10, 22, 11, 5),
      paidE4: 1000000,
      cashier: 'Sara',
    );
  });

  tearDown(() => db.close());

  List<String> ids(List<SaleRow> rows) => rows.map((r) => r.id).toList();

  test('lists newest first and pages', () async {
    final all = await history.search(const SalesFilter());
    expect(ids(all), [d.id, c.id, b.id, a.id]);
    final page = await history.search(const SalesFilter(), limit: 2, offset: 2);
    expect(ids(page), [b.id, a.id]);
  });

  test('date range includes the start and excludes the end', () async {
    final rows = await history.search(
      SalesFilter(
        from: DateTime(2026, 10, 21),
        toExclusive: DateTime(2026, 10, 22),
      ),
    );
    expect(ids(rows), [c.id, b.id]);
  });

  test('text search finds cashier, receipt number and product', () async {
    expect(ids(await history.search(const SalesFilter(text: 'sara'))), [
      d.id,
      b.id,
    ]);
    expect(ids(await history.search(SalesFilter(text: a.receiptNumber))), [
      a.id,
    ]);
    expect(ids(await history.search(const SalesFilter(text: 'gadget'))), [
      c.id,
      b.id,
    ]);
    expect(ids(await history.search(const SalesFilter(text: 'WIDGET'))), [
      d.id,
      a.id,
    ]);
    expect(
      await history.search(const SalesFilter(text: 'nothing-here')),
      isEmpty,
    );
  });

  test('text search pages after matching', () async {
    final first = await history.search(
      const SalesFilter(text: 'widget'),
      limit: 1,
    );
    final second = await history.search(
      const SalesFilter(text: 'widget'),
      limit: 1,
      offset: 1,
    );
    expect(ids(first), [d.id]);
    expect(ids(second), [a.id]);
  });

  test('percent and underscore in the search are literal text', () async {
    expect(await history.search(const SalesFilter(text: '%')), isEmpty);
    expect(await history.search(const SalesFilter(text: '_')), isEmpty);
  });

  test('payment method filter', () async {
    final rows = await history.search(const SalesFilter(paymentMethod: 'Card'));
    expect(ids(rows), [b.id]);
  });

  test('summary adds up exactly and shows what is still due', () async {
    final s = await history.summary(const SalesFilter());
    expect(s.saleCount, 4);
    expect(s.totalE4, 2750000);
    expect(s.paidE4, 2600000);
    expect(s.outstandingE4, 150000);
    expect(s.excludedCount, 0);
    final cash = s.byMethod.firstWhere((m) => m.method == 'Cash');
    final card = s.byMethod.firstWhere((m) => m.method == 'Card');
    expect(cash.paymentCount, 3);
    expect(cash.amountE4, 2100000);
    expect(card.paymentCount, 1);
    expect(card.amountE4, 500000);
  });

  test('summary respects the date range', () async {
    final s = await history.summary(
      SalesFilter(
        from: DateTime(2026, 10, 21),
        toExclusive: DateTime(2026, 10, 22),
      ),
    );
    expect(s.saleCount, 2);
    expect(s.totalE4, 750000);
    expect(s.paidE4, 600000);
    expect(s.outstandingE4, 150000);
  });

  test('detail keeps the saved snapshot after product changes', () async {
    await db
        .update(db.products)
        .write(
          const ProductsCompanion(
            name: Value('Renamed'),
            salePriceE4: Value(9999999),
          ),
        );
    final detail = await history.detail(a.id);
    expect(detail.receipt.lines.single.name, 'Widget');
    expect(detail.receipt.lines.single.unitPriceE4, 1000000);
    expect(detail.receipt.totalE4, 1000000);
    expect(detail.createdBy, 'Ali');
  });

  test('detail lists the payments', () async {
    final detail = await history.detail(b.id);
    expect(detail.payments, hasLength(1));
    expect(detail.payments.single.method, 'Card');
    expect(detail.payments.single.amountE4, 500000);
  });

  test('voided sales stay in history but leave the totals', () async {
    await db.customStatement(
      "UPDATE sales SET status = 'voided' WHERE id = ?",
      [d.id],
    );
    final s = await history.summary(const SalesFilter());
    expect(s.saleCount, 3);
    expect(s.totalE4, 1750000);
    expect(s.excludedCount, 1);
    final voided = await history.search(const SalesFilter(status: 'voided'));
    expect(ids(voided), [d.id]);
    final all = await history.search(const SalesFilter());
    expect(all, hasLength(4));
  });

  test('daily totals group by local day, newest first', () async {
    final days = await history.dailyTotals(const SalesFilter());
    expect(days.map((x) => x.day).toList(), [
      DateTime(2026, 10, 22),
      DateTime(2026, 10, 21),
      DateTime(2026, 10, 20),
    ]);
    expect(days[1].saleCount, 2);
    expect(days[1].totalE4, 750000);
    expect(days[1].paidE4, 600000);
  });

  test('history never changes the saved sale', () async {
    await history.search(const SalesFilter());
    await history.summary(const SalesFilter());
    await history.detail(c.id);
    final again = await history.detail(c.id);
    expect(again.receipt.totalE4, 250000);
    expect(again.receipt.amountPaidE4, 100000);
  });

  test('reprint text is marked as a duplicate and keeps the numbers', () async {
    final detail = await history.detail(c.id);
    final text = buildReprintText(
      detail,
      businessName: 'Test Shop',
      footer: 'Thank you',
    );
    expect(text, contains('REPRINT'));
    expect(text, contains(c.receiptNumber));
    expect(text, contains('Remaining due'));
    expect(text, contains('Thank you'));
  });
}
