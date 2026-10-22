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
