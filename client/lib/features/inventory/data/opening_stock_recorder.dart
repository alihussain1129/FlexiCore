import 'package:flutter/foundation.dart' show VoidCallback;

import '../domain/inventory_domain.dart';
import '../domain/opening_stock.dart';
import 'local_inventory_repository.dart';

/// Builds the recorder that the product form uses. It goes through
/// [LocalInventoryRepository.record], like the Stock tab, so the ledger,
/// the balance, the request id rules and the one-opening-per-product rule
/// are all the same.
OpeningStockRecorder localOpeningStockRecorder(
  LocalInventoryRepository repo, {
  String actor = 'Owner',
  VoidCallback? onRecorded,
}) {
  return ({
    required String productId,
    required String quantityText,
    required String requestId,
  }) async {
    final qty = parseQtyE4(quantityText);
    if (qty == null || qty <= 0) {
      return 'Opening stock must be a number above zero (up to 4 decimals).';
    }
    try {
      final warehouse = await repo.ensureDefaultWarehouse();
      await repo.record(
        MovementRequest(
          productId: productId,
          warehouseId: warehouse.id,
          type: StockMovementType.opening,
          quantityE4: qty,
          requestId: requestId,
          actor: actor,
          reason: 'Entered when the product was created',
        ),
      );
      onRecorded?.call();
      return null;
    } on InventoryException catch (e) {
      return e.message;
    } catch (e) {
      return 'Could not record opening stock: $e';
    }
  };
}