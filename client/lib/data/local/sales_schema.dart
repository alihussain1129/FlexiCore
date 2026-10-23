/// Idempotent indexes for sales and payment lookups.
const List<String> kSalesSchemaStatements = [
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_sales_company_request '
      'ON sales (company_id, client_request_id)',
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_sales_company_receipt '
      'ON sales (company_id, receipt_number)',
  'CREATE INDEX IF NOT EXISTS ix_sales_company_created '
      'ON sales (company_id, created_at)',
  'CREATE INDEX IF NOT EXISTS ix_sale_lines_sale ON sale_lines (sale_id)',
  'CREATE INDEX IF NOT EXISTS ix_sale_lines_product '
      'ON sale_lines (product_id)',
  'CREATE INDEX IF NOT EXISTS ix_sale_payments_sale '
      'ON sale_payments (sale_id)',
];

/// Integrity rules added in schema v5. A posted sale is never edited or
/// deleted. A correction is a new return document, not a rewrite.
const List<String> kSalesIntegrityStatements = [
  'CREATE TRIGGER IF NOT EXISTS trg_sales_insert_valid '
      'BEFORE INSERT ON sales '
      'WHEN NEW.subtotal_e4 < 0 OR NEW.discount_e4 < 0 OR NEW.tax_e4 < 0 '
      'OR NEW.total_e4 < 0 OR NEW.amount_paid_e4 < 0 OR NEW.change_e4 < 0 '
      'OR NEW.discount_e4 > NEW.subtotal_e4 '
      'OR NEW.total_e4 <> NEW.subtotal_e4 - NEW.discount_e4 + NEW.tax_e4 '
      'OR NEW.amount_paid_e4 > NEW.total_e4 '
      "OR NEW.status NOT IN ('completed', 'partially_returned', 'returned', 'voided') "
      "BEGIN SELECT RAISE(ABORT, 'invalid sale amounts'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sales_status_valid '
      'BEFORE UPDATE OF status ON sales '
      "WHEN NEW.status NOT IN ('completed', 'partially_returned', 'returned', 'voided') "
      "BEGIN SELECT RAISE(ABORT, 'invalid sale status'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sales_no_financial_update '
      'BEFORE UPDATE OF id, company_id, receipt_number, client_request_id, '
      'warehouse_id, subtotal_e4, discount_e4, tax_e4, total_e4, '
      'amount_paid_e4, payment_method, created_at, created_by, change_e4, '
      'tax_rate_bp, discount_reason ON sales '
      "BEGIN SELECT RAISE(ABORT, 'posted sales cannot be edited'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sales_no_delete BEFORE DELETE ON sales '
      "BEGIN SELECT RAISE(ABORT, 'posted sales cannot be deleted'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_lines_insert_valid '
      'BEFORE INSERT ON sale_lines '
      'WHEN NEW.quantity_e4 <= 0 OR NEW.unit_price_e4 < 0 '
      'OR NEW.discount_e4 < 0 OR NEW.tax_e4 < 0 OR NEW.line_total_e4 < 0 '
      "BEGIN SELECT RAISE(ABORT, 'invalid sale line amounts'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_lines_no_update '
      'BEFORE UPDATE ON sale_lines '
      "BEGIN SELECT RAISE(ABORT, 'sale lines cannot be edited'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_lines_no_delete '
      'BEFORE DELETE ON sale_lines '
      "BEGIN SELECT RAISE(ABORT, 'sale lines cannot be deleted'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_payments_insert_valid '
      'BEFORE INSERT ON sale_payments WHEN NEW.amount_e4 < 0 '
      "BEGIN SELECT RAISE(ABORT, 'invalid payment amount'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_payments_no_update '
      'BEFORE UPDATE ON sale_payments '
      "BEGIN SELECT RAISE(ABORT, 'payments cannot be edited'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_payments_no_delete '
      'BEFORE DELETE ON sale_payments '
      "BEGIN SELECT RAISE(ABORT, 'payments cannot be deleted'); END",
];
