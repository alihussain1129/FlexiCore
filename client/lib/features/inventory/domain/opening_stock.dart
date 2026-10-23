/// Hands the opening stock of a newly created product to the inventory ledger.
/// Returns null on success, or a message the user can read.
typedef OpeningStockRecorder = Future<String?> Function({
  required String productId,
  required String quantityText,
  required String requestId,
});