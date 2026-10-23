/// One row of the balances view: how a product's stock position was built up.
library;

class StockBreakdownLine {
  const StockBreakdownLine({
    required this.productId,
    required this.sku,
    required this.name,
    required this.openingE4,
    required this.receivedE4,
    required this.issuedE4,
    required this.otherE4,
    required this.onHandE4,
    this.unitCode,
  });

  final String productId;
  final String sku;
  final String name;
  final String? unitCode;
  final int openingE4;
  final int receivedE4;

  /// Issues are stored as negative numbers in the ledger.
  final int issuedE4;

  /// Adjustments and reversals together (signed).
  final int otherE4;

  /// The stored balance.
  final int onHandE4;

  /// True when the ledger parts add up to the stored balance.
  bool get matchesLedger =>
      openingE4 + receivedE4 + issuedE4 + otherE4 == onHandE4;
}