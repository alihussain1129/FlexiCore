import 'package:drift/native.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/data/local/app_database.dart';

void main() {
  test('a v4 database upgrades to v5 and keeps its old sale', () async {
    final db = AppDatabase(
      NativeDatabase.memory(
        setup: (raw) {
          raw.execute(
            'CREATE TABLE sales (id TEXT NOT NULL, company_id TEXT NOT NULL, '
            'receipt_number TEXT NOT NULL, client_request_id TEXT NOT NULL, '
            'warehouse_id TEXT NOT NULL, subtotal_e4 INTEGER NOT NULL, '
            'discount_e4 INTEGER NOT NULL DEFAULT 0, '
            'tax_e4 INTEGER NOT NULL DEFAULT 0, total_e4 INTEGER NOT NULL, '
            'amount_paid_e4 INTEGER NOT NULL, payment_method TEXT NOT NULL, '
            'created_at INTEGER NOT NULL, created_by TEXT NOT NULL, '
            'origin_device_id TEXT NULL, PRIMARY KEY (id))',
          );
          raw.execute(
            'CREATE TABLE sale_lines (id TEXT NOT NULL, sale_id TEXT NOT NULL, '
            'product_id TEXT NOT NULL, sku_snapshot TEXT NOT NULL, '
            'name_snapshot TEXT NOT NULL, quantity_e4 INTEGER NOT NULL, '
            'unit_price_e4 INTEGER NOT NULL, '
            'discount_e4 INTEGER NOT NULL DEFAULT 0, '
            'line_total_e4 INTEGER NOT NULL, PRIMARY KEY (id))',
          );
          raw.execute(
            'CREATE TABLE sale_payments (id TEXT NOT NULL, '
            'sale_id TEXT NOT NULL, amount_e4 INTEGER NOT NULL, '
            'method TEXT NOT NULL, received_at INTEGER NOT NULL, '
            'reference TEXT NULL, PRIMARY KEY (id))',
          );
          raw.execute(
            "INSERT INTO sales VALUES ('s1', 'c1', 'R1', 'req1', 'w1', "
            "1000000, 0, 0, 1000000, 1000000, 'Cash', 1760000000, 'Ali', NULL)",
          );
          raw.execute('PRAGMA user_version = 4');
        },
      ),
    );
    addTearDown(db.close);

    final row = await db
        .customSelect(
          'SELECT status, change_e4, tax_rate_bp, total_e4 FROM sales '
          "WHERE id = 's1'",
        )
        .getSingle();
    expect(row.read<String>('status'), 'completed');
    expect(row.read<int>('change_e4'), 0);
    expect(row.read<int>('tax_rate_bp'), 0);
    expect(row.read<int>('total_e4'), 1000000);

    final settingsTable = await db
        .customSelect(
          "SELECT name FROM sqlite_master WHERE name = 'pos_settings'",
        )
        .get();
    expect(settingsTable.length, 1);

    final triggers = await db
        .customSelect(
          "SELECT name FROM sqlite_master WHERE type = 'trigger' "
          "AND name LIKE 'trg_sale%'",
        )
        .get();
    expect(triggers.length, greaterThanOrEqualTo(8));

    await expectLater(
      db.customStatement("DELETE FROM sales WHERE id = 's1'"),
      throwsA(anything),
    );
  });
}
