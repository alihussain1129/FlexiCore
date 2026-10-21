/// SQL that drift cannot express: partial unique indexes and the triggers
/// that keep the stock ledger append-only.
///
/// Every statement is idempotent. They run when a database is created and
/// when it is upgraded to schema v3.
const List<String> kInventorySchemaStatements = [
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouses_company_code '
      'ON warehouses (company_id, code) WHERE deleted_at IS NULL',
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_warehouses_company_default '
      'ON warehouses (company_id) '
      'WHERE is_default = 1 AND deleted_at IS NULL',
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_stock_movements_request '
      'ON stock_movements (company_id, client_request_id)',
  'CREATE INDEX IF NOT EXISTS ix_stock_movements_product '
      'ON stock_movements (company_id, product_id, warehouse_id, occurred_at)',
  'CREATE INDEX IF NOT EXISTS ix_stock_movements_occurred '
      'ON stock_movements (company_id, occurred_at)',
  'CREATE INDEX IF NOT EXISTS ix_stock_movements_reverses '
      'ON stock_movements (reverses_movement_id) '
      'WHERE reverses_movement_id IS NOT NULL',
  'CREATE INDEX IF NOT EXISTS ix_stock_balances_product '
      'ON stock_balances (company_id, product_id)',
  'CREATE TRIGGER IF NOT EXISTS trg_stock_movements_no_update '
      'BEFORE UPDATE ON stock_movements '
      "BEGIN SELECT RAISE(ABORT, 'stock_movements is append-only'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_stock_movements_no_delete '
      'BEFORE DELETE ON stock_movements '
      "BEGIN SELECT RAISE(ABORT, 'stock_movements is append-only'); END",
];
