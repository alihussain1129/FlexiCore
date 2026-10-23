import 'package:drift/drift.dart' show Value;
import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_repository.dart';
import 'package:flexicore_client/features/pos/data/local/local_pos_settings_repository.dart';

const _company = 'company-1';

Future<void> _seed(AppDatabase db) async {
  final now = DateTime.utc(2026, 10, 22);
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
          costPriceE4: const Value(400000),
        ),
      );
  await db
      .into(db.stockBalances)
      .insert(
        StockBalancesCompanion.insert(
          companyId: _company,
          productId: 'p1',
          warehouseId: 'w1',
          qtyE4: 100000,
          updatedAt: now,
        ),
      );
}

PosCheckoutRequest _request(
  String id, {
  int qtyE4 = 20000,
  int paid = 2000000,
  String method = 'Cash',
  int orderDiscountE4 = 0,
  int? tendered,
}) => PosCheckoutRequest(
  clientRequestId: id,
  items: [PosCartItem(productId: 'p1', quantityE4: qtyE4)],
  paymentMethod: method,
  amountPaidE4: paid,
  createdBy: 'Ali',
  orderDiscountE4: orderDiscountE4,
  cashTenderedE4: tendered,
);

void main() {
  late AppDatabase db;
  late LocalPosRepository pos;
  late LocalPosSettingsRepository settings;

  setUp(() async {
    db = AppDatabase(NativeDatabase.memory());
    await _seed(db);
    pos = LocalPosRepository(db, companyId: _company);
    settings = LocalPosSettingsRepository(db, companyId: _company);
  });

  tearDown(() => db.close());

  test('without settings the totals are unchanged', () async {
    final r = await pos.checkout(_request('r1'));
    expect(r.subtotalE4, 2000000);
    expect(r.totalE4, 2000000);
    expect(r.taxE4, 0);
    expect(r.discountE4, 0);
    expect(r.changeE4, 0);
    expect(r.status, 'completed');
  });

  test('tax is added on top and stored per line', () async {
    await settings.save(const PosConfig(taxRateBp: 1500));
    final r = await pos.checkout(_request('r1', paid: 2300000));
    expect(r.taxE4, 300000);
    expect(r.totalE4, 2300000);
    expect(r.taxRateBp, 1500);
    expect(r.lines.single.taxE4, 300000);
  });

  test('an order discount lowers the taxable amount', () async {
    await settings.save(const PosConfig(taxRateBp: 1500));
    final r = await pos.checkout(
      _request('r1', paid: 2185000, orderDiscountE4: 100000),
    );
    expect(r.discountE4, 100000);
    expect(r.taxE4, 285000);
    expect(r.totalE4, 2185000);
    expect(r.lines.single.lineTotalE4, 1900000);
  });

  test(
    'cash change is stored and the payment holds the applied amount',
    () async {
      await settings.save(const PosConfig(taxRateBp: 1500));
      final r = await pos.checkout(
        _request('r1', paid: 2300000, tendered: 5000000),
      );
      expect(r.changeE4, 2700000);
      final payment = await db.select(db.salePayments).getSingle();
      expect(payment.amountE4, 2300000);
    },
  );

  test('change is refused for non-cash methods', () async {
    await expectLater(
      () => pos.checkout(_request('r1', method: 'Card', tendered: 3000000)),
      throwsA(isA<PosException>()),
    );
  });

  test('cash received below the amount paid is refused', () async {
    await expectLater(
      () => pos.checkout(_request('r1', tendered: 1000000)),
      throwsA(isA<PosException>()),
    );
  });

  test('a discount above the configured maximum is refused', () async {
    await settings.save(const PosConfig(maxDiscountBp: 1000));
    await expectLater(
      pos.checkout(_request('r1', paid: 1700000, orderDiscountE4: 300000)),
      throwsA(isA<PosException>()),
    );
    expect(await db.select(db.sales).get(), isEmpty);
  });

  test(
    'a retried request deducts stock once and returns the same sale',
    () async {
      final first = await pos.checkout(_request('r1'));
      final second = await pos.checkout(_request('r1'));
      expect(second.id, first.id);
      expect((await db.select(db.sales).get()).length, 1);
      final balance = await db.select(db.stockBalances).getSingle();
      expect(balance.qtyE4, 80000);
    },
  );

  test('a posted sale cannot be edited or deleted', () async {
    await pos.checkout(_request('r1'));
    await expectLater(
      db.customStatement('DELETE FROM sales'),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement('UPDATE sales SET total_e4 = 1'),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement('DELETE FROM sale_lines'),
      throwsA(anything),
    );
    await expectLater(
      db.customStatement('UPDATE sale_payments SET amount_e4 = 1'),
      throwsA(anything),
    );
  });

  test('settings round trip and invalid values are refused', () async {
    final saved = await settings.save(
      const PosConfig(
        taxRateBp: 1250,
        maxDiscountBp: 2000,
        receiptFooter: ' Thanks ',
      ),
    );
    expect(saved.taxRateBp, 1250);
    expect(saved.maxDiscountBp, 2000);
    expect(saved.receiptFooter, 'Thanks');
    await expectLater(
      settings.save(const PosConfig(taxRateBp: 10001)),
      throwsArgumentError,
    );
  });

  test(
    'recentSales lists newest first and receiptForSale rebuilds a receipt',
    () async {
      var tick = 0;
      final timed = LocalPosRepository(
        db,
        companyId: _company,
        clock: () => DateTime.utc(2026, 10, 22, 10, 0, tick++),
      );
      final a = await timed.checkout(
        _request('r1', qtyE4: 10000, paid: 1000000),
      );
      final b = await timed.checkout(
        _request('r2', qtyE4: 10000, paid: 1000000),
      );
      final list = await timed.recentSales();
      expect(list.map((s) => s.id).toList(), [b.id, a.id]);
      final rebuilt = await timed.receiptForSale(a.id);
      expect(rebuilt.receiptNumber, a.receiptNumber);
      expect(rebuilt.lines.length, 1);
    },
  );
}
