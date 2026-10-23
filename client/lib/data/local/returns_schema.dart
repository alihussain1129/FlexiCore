/// Integrity rules for returns (schema v6). A return is a new document linked
/// to the original sale. The sale itself is never rewritten, and a return is
/// never edited or deleted afterwards.
const List<String> kReturnsSchemaStatements = [
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_sale_returns_company_request '
      'ON sale_returns (company_id, client_request_id)',
  'CREATE UNIQUE INDEX IF NOT EXISTS uq_sale_returns_company_number '
      'ON sale_returns (company_id, return_number)',
  'CREATE INDEX IF NOT EXISTS ix_sale_returns_sale ON sale_returns (sale_id)',
  'CREATE INDEX IF NOT EXISTS ix_sale_returns_company_created '
      'ON sale_returns (company_id, created_at)',
  'CREATE INDEX IF NOT EXISTS ix_sale_return_lines_return '
      'ON sale_return_lines (return_id)',
  'CREATE INDEX IF NOT EXISTS ix_sale_return_lines_sale_line '
      'ON sale_return_lines (sale_line_id)',
  'CREATE TRIGGER IF NOT EXISTS trg_sale_returns_insert_valid '
      'BEFORE INSERT ON sale_returns '
      'WHEN NEW.net_e4 < 0 OR NEW.tax_e4 < 0 OR NEW.refund_e4 < 0 '
      'OR NEW.total_e4 <> NEW.net_e4 + NEW.tax_e4 '
      'OR NEW.refund_e4 > NEW.total_e4 '
      'OR length(trim(NEW.reason)) = 0 OR length(trim(NEW.refund_method)) = 0 '
      "BEGIN SELECT RAISE(ABORT, 'invalid return amounts'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_returns_refund_cap '
      'BEFORE INSERT ON sale_returns '
      'WHEN (SELECT COALESCE(SUM(refund_e4), 0) FROM sale_returns '
      'WHERE sale_id = NEW.sale_id) + NEW.refund_e4 > '
      'COALESCE((SELECT amount_paid_e4 FROM sales WHERE id = NEW.sale_id), 0) '
      "BEGIN SELECT RAISE(ABORT, 'refund exceeds the amount paid'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_returns_sale_open '
      'BEFORE INSERT ON sale_returns '
      "WHEN COALESCE((SELECT status FROM sales WHERE id = NEW.sale_id), 'voided') = 'voided' "
      "BEGIN SELECT RAISE(ABORT, 'this sale cannot be returned'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_returns_no_update '
      'BEFORE UPDATE ON sale_returns '
      "BEGIN SELECT RAISE(ABORT, 'returns cannot be edited'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_returns_no_delete '
      'BEFORE DELETE ON sale_returns '
      "BEGIN SELECT RAISE(ABORT, 'returns cannot be deleted'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_return_lines_insert_valid '
      'BEFORE INSERT ON sale_return_lines '
      'WHEN NEW.quantity_e4 <= 0 OR NEW.net_e4 < 0 OR NEW.tax_e4 < 0 '
      "OR NEW.disposition NOT IN ('restock', 'discard') "
      "BEGIN SELECT RAISE(ABORT, 'invalid return line'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_return_lines_same_sale '
      'BEFORE INSERT ON sale_return_lines '
      'WHEN (SELECT sale_id FROM sale_lines WHERE id = NEW.sale_line_id) IS NOT '
      '(SELECT sale_id FROM sale_returns WHERE id = NEW.return_id) '
      "BEGIN SELECT RAISE(ABORT, 'line is from another sale'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_return_lines_not_over '
      'BEFORE INSERT ON sale_return_lines '
      'WHEN (SELECT COALESCE(SUM(quantity_e4), 0) FROM sale_return_lines '
      'WHERE sale_line_id = NEW.sale_line_id) + NEW.quantity_e4 > '
      '(SELECT quantity_e4 FROM sale_lines WHERE id = NEW.sale_line_id) '
      'OR (SELECT COALESCE(SUM(net_e4), 0) FROM sale_return_lines '
      'WHERE sale_line_id = NEW.sale_line_id) + NEW.net_e4 > '
      '(SELECT line_total_e4 FROM sale_lines WHERE id = NEW.sale_line_id) '
      'OR (SELECT COALESCE(SUM(tax_e4), 0) FROM sale_return_lines '
      'WHERE sale_line_id = NEW.sale_line_id) + NEW.tax_e4 > '
      '(SELECT tax_e4 FROM sale_lines WHERE id = NEW.sale_line_id) '
      "BEGIN SELECT RAISE(ABORT, 'return exceeds what was sold'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_return_lines_no_update '
      'BEFORE UPDATE ON sale_return_lines '
      "BEGIN SELECT RAISE(ABORT, 'return lines cannot be edited'); END",
  'CREATE TRIGGER IF NOT EXISTS trg_sale_return_lines_no_delete '
      'BEFORE DELETE ON sale_return_lines '
      "BEGIN SELECT RAISE(ABORT, 'return lines cannot be deleted'); END",
];
