import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_repository.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_settings_repository.dart';
import 'package:flexicore_client/features/pos/data/local/local_returns_repository.dart';
import 'package:flexicore_client/features/pos/data/local/local_sales_history_repository.dart';
import 'package:flexicore_client/features/pos/presentation/return_receipt_text.dart';

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
  await db
      .into(db.products)
      .insert(
        ProductsCompanion.insert(
          id: 'p1',
          companyId: _company,
          sku: 'SKU1',
          name: 'Widget',
          updatedAt: now,
          salePriceE4: const Value(1000000),
          costPriceE4: const Value(100000),
        ),
      );
  await db
      .into(db.stockBalances)
      .insert(
        StockBalancesCompanion.insert(
          companyId: _company,
          productId: 'p1',
          warehouseId: 'w1',
          qtyE4: 1000000,
          updatedAt: now,
        ),
      );
}

void main() {
  late AppDatabase db;
  late LocalPosRepository pos;
  late LocalReturnsRepository returns;
  late LocalPosSettingsRepository settings;
  late LocalSalesHistoryRepository history;
  late DateTime now;
  var n = 0;

  setUp(() async {
    n = 0;
    now = DateTime.utc(2026, 10, 22, 10);
    db = AppDatabase(NativeDatabase.memory());
    await _seed(db);
    pos = LocalPosRepository(db, companyId: _company, clock: () => now);
    returns = LocalReturnsRepository(db, companyId: _company, clock: () => now);
    settings = LocalPosSettingsRepository(db, companyId: _company);
    history = LocalSalesHistoryRepository(db, companyId: _company);
  });

  tearDown(() => db.close());

  Future<PosReceipt> sell({
    int qtyE4 = 30000,
    required int paidE4,
    int discountE4 = 0,
  }) {
    n++;
    return pos.checkout(
      PosCheckoutRequest(
        clientRequestId: 'sale-$n',
        items: [PosCartItem(productId: 'p1', quantityE4: qtyE4)],
        paymentMethod: 'Cash',
        amountPaidE4: paidE4,
        createdBy: 'Ali',
        orderDiscountE4: discountE4,
      ),
    );
  }

  Future<int> onHand() async => (await (db.select(
    db.stockBalances,
  )..where((b) => b.productId.equals('p1'))).getSingle()).qtyE4;

  Future<String> statusOf(String saleId) async => (await (db.select(
    db.sales,
  )..where((s) => s.id.equals(saleId))).getSingle()).status;

  Future<String> lineId(String saleId) async =>
      (await returns.returnable(saleId)).lines.single.saleLineId;

  ReturnRequest req(
    String id,
    String saleId,
    String saleLineId,
    int qtyE4, {
    bool restock = true,
    String method = 'Cash',
    String reason = 'Faulty',
  }) => ReturnRequest(
    clientRequestId: id,
    saleId: saleId,
    lines: [
      ReturnLineRequest(
        saleLineId: saleLineId,
        quantityE4: qtyE4,
        restock: restock,
      ),
    ],
    refundMethod: method,
    reason: reason,
    createdBy: 'Ali',
  );

  test('a full return refunds everything and puts stock back', () async {
    final sale = await sell(paidE4: 3000000);
    expect(await onHand(), 970000);
    final lid = await lineId(sale.id);

    final r = await returns.processReturn(req('ret-1', sale.id, lid, 30000));
    expect(r.netE4, 3000000);
    expect(r.taxE4, 0);
    expect(r.totalE4, 3000000);
    expect(r.refundE4, 3000000);
    expect(r.creditE4, 0);
    expect(r.lines.single.restocked, isTrue);
    expect(r.saleReceiptNumber, sale.receiptNumber);

    expect(await onHand(), 1000000);
    expect(await statusOf(sale.id), 'returned');

    final saved = await pos.receiptForSale(sale.id);
    expect(saved.totalE4, 3000000);
    expect(saved.amountPaidE4, 3000000);

    final moves = await (db.select(
      db.stockMovements,
    )..where((m) => m.sourceType.equals('sale_return'))).get();
    expect(moves, hasLength(1));
    expect(moves.single.movementType, 'receipt');
    expect(moves.single.qtyE4, 30000);
    expect(moves.single.unitCostE4, 100000);
  });

  test('three partial returns add up exactly to the sold amount', () async {
    final sale = await sell(paidE4: 2899999, discountE4: 100001);
    final lid = await lineId(sale.id);

    final r1 = await returns.processReturn(req('ret-1', sale.id, lid, 10000));
    expect(r1.netE4, 966666);
    expect(await statusOf(sale.id), 'partially_returned');
    final r2 = await returns.processReturn(req('ret-2', sale.id, lid, 10000));
    expect(r2.netE4, 966666);
    final r3 = await returns.processReturn(req('ret-3', sale.id, lid, 10000));
    expect(r3.netE4, 966667);

    expect(r1.netE4 + r2.netE4 + r3.netE4, 2899999);
    expect(r1.refundE4 + r2.refundE4 + r3.refundE4, 2899999);
    expect(await statusOf(sale.id), 'returned');
    expect(await onHand(), 1000000);
  });

  test('tax is returned with the goods', () async {
    await settings.save(const PosConfig(taxRateBp: 1500));
    final sale = await sell(paidE4: 3450000);
    final lid = await lineId(sale.id);

    final one = await returns.processReturn(req('ret-1', sale.id, lid, 10000));
    expect(one.netE4, 1000000);
    expect(one.taxE4, 150000);
    expect(one.totalE4, 1150000);
    expect(one.refundE4, 1150000);

    final rest = await returns.processReturn(req('ret-2', sale.id, lid, 20000));
    expect(rest.netE4, 2000000);
    expect(rest.taxE4, 300000);
    expect(one.refundE4 + rest.refundE4, 3450000);
  });

  test('a part-paid sale offsets the balance before paying out', () async {
    final sale = await sell(qtyE4: 20000, paidE4: 800000);
    final lid = await lineId(sale.id);

    final first = await returns.processReturn(
      req('ret-1', sale.id, lid, 10000),
    );
    expect(first.totalE4, 1000000);
    expect(first.refundE4, 0);
    expect(first.creditE4, 1000000);

    final second = await returns.processReturn(
      req('ret-2', sale.id, lid, 10000),
    );
    expect(second.refundE4, 800000);
    expect(first.refundE4 + second.refundE4, 800000);
  });

  test('returning more than was sold is refused and writes nothing', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);

    await expectLater(
      returns.processReturn(req('ret-1', sale.id, lid, 40000)),
      throwsA(isA<ReturnException>()),
    );
    expect(await db.select(db.saleReturns).get(), isEmpty);
    expect(await onHand(), 970000);
    expect(await statusOf(sale.id), 'completed');
  });

  test('a second return cannot exceed what is left', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);
    await returns.processReturn(req('ret-1', sale.id, lid, 20000));
    await expectLater(
      returns.processReturn(req('ret-2', sale.id, lid, 20000)),
      throwsA(isA<ReturnException>()),
    );
    expect(await db.select(db.saleReturns).get(), hasLength(1));
    expect(await onHand(), 990000);
  });

  test('repeating a request returns the same return once', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);

    final a = await returns.processReturn(req('ret-1', sale.id, lid, 10000));
    final b = await returns.processReturn(req('ret-1', sale.id, lid, 10000));
    expect(b.id, a.id);
    expect(await db.select(db.saleReturns).get(), hasLength(1));
    expect(await onHand(), 980000);

    await expectLater(
      returns.processReturn(req('ret-1', sale.id, lid, 20000)),
      throwsA(isA<ReturnException>()),
    );
    expect(await onHand(), 980000);
  });

  test('discarded goods are refunded but stock is left alone', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);

    final r = await returns.processReturn(
      req('ret-1', sale.id, lid, 10000, restock: false),
    );
    expect(r.refundE4, 1000000);
    expect(r.lines.single.restocked, isFalse);
    expect(await onHand(), 970000);
    final moves = await (db.select(
      db.stockMovements,
    )..where((m) => m.sourceType.equals('sale_return'))).get();
    expect(moves, isEmpty);
  });

  test('a voided sale cannot be returned', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);
    await db.customStatement(
      "UPDATE sales SET status = 'voided' WHERE id = ?",
      [sale.id],
    );
    await expectLater(
      returns.processReturn(req('ret-1', sale.id, lid, 10000)),
      throwsA(isA<ReturnException>()),
    );
  });

  test('bad input is refused', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);

    Future<void> refused(ReturnRequest r) =>
        expectLater(returns.processReturn(r), throwsA(isA<ReturnException>()));

    await refused(req('ret-1', sale.id, lid, 10000, reason: '  '));
    await refused(req('ret-2', sale.id, lid, 10000, method: ''));
    await refused(req('ret-3', sale.id, lid, 0));
    await refused(req('ret-4', sale.id, lid, -10000));
    await refused(req('ret-5', sale.id, 'not-a-line', 10000));
    await refused(
      ReturnRequest(
        clientRequestId: 'ret-6',
        saleId: sale.id,
        lines: const [],
        refundMethod: 'Cash',
        reason: 'Faulty',
        createdBy: 'Ali',
      ),
    );
    expect(await db.select(db.saleReturns).get(), isEmpty);
  });

  test('a failure while restocking rolls the whole return back', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);
    await db.customStatement(
      'CREATE TRIGGER fail_receipt BEFORE INSERT ON stock_movements '
      "WHEN NEW.movement_type = 'receipt' "
      "BEGIN SELECT RAISE(ABORT, 'boom'); END",
    );

    await expectLater(
      returns.processReturn(req('ret-1', sale.id, lid, 10000)),
      throwsA(anything),
    );
    expect(await db.select(db.saleReturns).get(), isEmpty);
    expect(await db.select(db.saleReturnLines).get(), isEmpty);
    expect(await onHand(), 970000);
    expect(await statusOf(sale.id), 'completed');
  });

  test('the database itself refuses over-returns and edits', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);
    final r = await returns.processReturn(req('ret-1', sale.id, lid, 10000));

    await expectLater(
      db.customStatement(
        'INSERT INTO sale_return_lines (id, return_id, sale_line_id, '
        'product_id, sku_snapshot, name_snapshot, quantity_e4, net_e4, '
        "tax_e4, disposition) VALUES ('x1', '${r.id}', '$lid', 'p1', "
        "'SKU1', 'Widget', 30000, 0, 0, 'restock')",
      ),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement(
        'INSERT INTO sale_returns (id, company_id, sale_id, return_number, '
        'client_request_id, net_e4, tax_e4, total_e4, refund_e4, '
        'refund_method, reason, created_at, created_by) VALUES '
        "('x2', '$_company', '${sale.id}', 'RT-X', 'req-x', 4000000, 0, "
        "4000000, 4000000, 'Cash', 'Faulty', 1760000000, 'Ali')",
      ),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement(
        "UPDATE sale_returns SET reason = 'changed' WHERE id = '${r.id}'",
      ),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement("DELETE FROM sale_returns WHERE id = '${r.id}'"),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement('DELETE FROM sale_return_lines'),
      throwsA(anything),
    );
  });

  test(
    'a returned sale stays in history totals and returns are summed',
    () async {
      final sale = await sell(paidE4: 3000000);
      final lid = await lineId(sale.id);
      await returns.processReturn(req('ret-1', sale.id, lid, 30000));

      final s = await history.summary(const SalesFilter());
      expect(s.saleCount, 1);
      expect(s.totalE4, 3000000);
      expect(s.excludedCount, 0);

      final all = await returns.summary();
      expect(all.count, 1);
      expect(all.valueE4, 3000000);
      expect(all.refundE4, 3000000);
      expect(all.refundsByMethod['Cash'], 3000000);

      final today = await returns.summary(
        from: DateTime.utc(2026, 10, 22),
        toExclusive: DateTime.utc(2026, 10, 23),
      );
      expect(today.count, 1);
      final tomorrow = await returns.summary(from: DateTime.utc(2026, 10, 23));
      expect(tomorrow.count, 0);
      final card = await returns.summary(refundMethod: 'Card');
      expect(card.count, 0);
    },
  );

  test('returns for a sale are listed with their lines', () async {
    final sale = await sell(paidE4: 3000000);
    final lid = await lineId(sale.id);
    await returns.processReturn(req('ret-1', sale.id, lid, 10000));
    await returns.processReturn(req('ret-2', sale.id, lid, 10000));
    final list = await returns.returnsForSale(sale.id);
    expect(list, hasLength(2));
    expect(list.first.lines.single.name, 'Widget');
    final state = await returns.returnable(sale.id);
    expect(state.lines.single.returnedQtyE4, 20000);
    expect(state.lines.single.remainingQtyE4, 10000);
    expect(state.canReturn, isTrue);
  });

  test('return receipt text shows the numbers and the stock decision', () {
    final receipt = ReturnReceipt(
      id: 'r1',
      returnNumber: 'RT-1',
      saleId: 's1',
      saleReceiptNumber: 'FC-1',
      createdAt: DateTime.utc(2026, 10, 22, 10),
      createdBy: 'Ali',
      reason: 'Faulty',
      refundMethod: 'Cash',
      netE4: 1000000,
      taxE4: 150000,
      totalE4: 1150000,
      refundE4: 1000000,
      lines: const [
        ReturnReceiptLine(
          saleLineId: 'l1',
          productId: 'p1',
          sku: 'SKU1',
          name: 'Widget',
          quantityE4: 10000,
          netE4: 1000000,
          taxE4: 150000,
          restocked: false,
        ),
      ],
    );
    final text = buildReturnReceiptText(
      receipt,
      businessName: 'Test Shop',
      footer: 'Thank you',
    );
    expect(text, contains('RETURN RECEIPT'));
    expect(text, contains('Return: RT-1'));
    expect(text, contains('Original sale: FC-1'));
    expect(text, contains('Refund (Cash)'));
    expect(text, contains('Credited to balance due'));
    expect(text, contains('Not put back in stock'));
    expect(text, contains('Reason: Faulty'));
    expect(text, contains('Thank you'));
  });

  test('schema is v6 on a new database and a v5 database upgrades', () async {
    expect(AppDatabase.currentSchemaVersion, 6);

    final fresh = await db
        .customSelect(
          "SELECT name FROM sqlite_master WHERE type = 'trigger' "
          "AND name LIKE 'trg_sale_return%'",
        )
        .get();
    expect(fresh.length, 10);

    final old = AppDatabase(
      NativeDatabase.memory(
        setup: (raw) {
          raw.execute(
            'CREATE TABLE sales (id TEXT NOT NULL, company_id TEXT NOT NULL, '
            'receipt_number TEXT NOT NULL, client_request_id TEXT NOT NULL, '
            'warehouse_id TEXT NOT NULL, subtotal_e4 INTEGER NOT NULL, '
            'discount_e4 INTEGER NOT NULL DEFAULT 0, '
            'tax_e4 INTEGER NOT NULL DEFAULT 0, total_e4 INTEGER NOT NULL, '
            'amount_paid_e4 INTEGER NOT NULL, '
            "status TEXT NOT NULL DEFAULT 'completed', "
            'change_e4 INTEGER NOT NULL DEFAULT 0, '
            'tax_rate_bp INTEGER NOT NULL DEFAULT 0, '
            'discount_reason TEXT NULL, payment_method TEXT NOT NULL, '
            'created_at INTEGER NOT NULL, created_by TEXT NOT NULL, '
            'origin_device_id TEXT NULL, PRIMARY KEY (id))',
          );
          raw.execute(
            'CREATE TABLE sale_lines (id TEXT NOT NULL, sale_id TEXT NOT NULL, '
            'product_id TEXT NOT NULL, sku_snapshot TEXT NOT NULL, '
            'name_snapshot TEXT NOT NULL, quantity_e4 INTEGER NOT NULL, '
            'unit_price_e4 INTEGER NOT NULL, '
            'discount_e4 INTEGER NOT NULL DEFAULT 0, '
            'line_total_e4 INTEGER NOT NULL, '
            'tax_e4 INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (id))',
          );
          raw.execute(
            'INSERT INTO sales (id, company_id, receipt_number, '
            'client_request_id, warehouse_id, subtotal_e4, discount_e4, '
            'tax_e4, total_e4, amount_paid_e4, status, change_e4, '
            'tax_rate_bp, discount_reason, payment_method, created_at, '
            "created_by, origin_device_id) VALUES ('s1', 'c1', 'R1', 'req1', "
            "'w1', 1000000, 0, 0, 1000000, 1000000, 'completed', 0, 0, NULL, "
            "'Cash', 1760000000, 'Ali', NULL)",
          );
          raw.execute('PRAGMA user_version = 5');
        },
      ),
    );
    addTearDown(old.close);

    final tables = await old
        .customSelect(
          "SELECT name FROM sqlite_master WHERE type = 'table' "
          "AND name IN ('sale_returns', 'sale_return_lines')",
        )
        .get();
    expect(tables.length, 2);

    final triggers = await old
        .customSelect(
          "SELECT name FROM sqlite_master WHERE type = 'trigger' "
          "AND name LIKE 'trg_sale_return%'",
        )
        .get();
    expect(triggers.length, 10);

    final row = await old
        .customSelect("SELECT total_e4 FROM sales WHERE id = 's1'")
        .getSingle();
    expect(row.read<int>('total_e4'), 1000000);
  });
}
