import 'package:drift/drift.dart';

import '../../../core/ids.dart';

import '../../../data/local/app_database.dart';
import '../domain/inventory_domain.dart';

/// Stock repository on the local drift database. Every write runs in one
/// transaction: the ledger row and the balance change succeed or fail together.
class LocalInventoryRepository implements InventoryRepository {
  LocalInventoryRepository(
    this._db, {
    required this.companyId,
    this.originDeviceId,
    DateTime Function()? clock,
  }) : _clock = clock ?? DateTime.now;

  final AppDatabase _db;
  final String companyId;
  final String? originDeviceId;
  final DateTime Function() _clock;

  Warehouse _warehouse(WarehouseRow r) =>
      Warehouse(id: r.id, code: r.code, name: r.name, isDefault: r.isDefault);

  @override
  Future<Warehouse> ensureDefaultWarehouse() {
    return _db.transaction(() async {
      final existing =
          await (_db.select(_db.warehouses)
                ..where(
                  (w) =>
                      w.companyId.equals(companyId) &
                      w.isDefault.equals(true) &
                      w.deletedAt.isNull(),
                )
                ..limit(1))
              .getSingleOrNull();
      if (existing != null) return _warehouse(existing);

      final first =
          await (_db.select(_db.warehouses)
                ..where(
                  (w) => w.companyId.equals(companyId) & w.deletedAt.isNull(),
                )
                ..orderBy([(w) => OrderingTerm.asc(w.code)])
                ..limit(1))
              .getSingleOrNull();
      final now = _clock();
      if (first != null) {
        await (_db.update(
          _db.warehouses,
        )..where((w) => w.id.equals(first.id))).write(
          WarehousesCompanion(
            isDefault: const Value(true),
            updatedAt: Value(now),
          ),
        );
        return Warehouse(
          id: first.id,
          code: first.code,
          name: first.name,
          isDefault: true,
        );
      }
      final id = generateUuidV4();
      await _db
          .into(_db.warehouses)
          .insert(
            WarehousesCompanion.insert(
              id: id,
              companyId: companyId,
              code: 'MAIN',
              name: 'Main warehouse',
              isDefault: const Value(true),
              updatedAt: now,
              originDeviceId: Value(originDeviceId),
            ),
          );
      return Warehouse(
        id: id,
        code: 'MAIN',
        name: 'Main warehouse',
        isDefault: true,
      );
    });
  }

  @override
  Future<List<Warehouse>> warehouses() async {
    final rows =
        await (_db.select(_db.warehouses)
              ..where(
                (w) => w.companyId.equals(companyId) & w.deletedAt.isNull(),
              )
              ..orderBy([
                (w) => OrderingTerm.desc(w.isDefault),
                (w) => OrderingTerm.asc(w.code),
              ]))
            .get();
    return rows.map(_warehouse).toList();
  }

  String _escapeLike(String s) =>
      s.replaceAll(r'\', r'\\').replaceAll('%', r'\%').replaceAll('_', r'\_');

  @override
  Future<List<StockLine>> stockLines({
    required String warehouseId,
    String search = '',
    int limit = 500,
  }) async {
    final term = search.trim();
    final like = '%${_escapeLike(term)}%';
    final rows = await _db
        .customSelect(
          r'''
SELECT p.id AS id, p.sku AS sku, p.name AS name, p.barcode AS barcode,
       u.code AS unit_code, p.cost_price_e4 AS cost,
       COALESCE(b.qty_e4, 0) AS qty
FROM products p
LEFT JOIN units u ON u.id = p.unit_id AND u.deleted_at IS NULL
LEFT JOIN stock_balances b ON b.product_id = p.id
  AND b.company_id = p.company_id AND b.warehouse_id = ?
WHERE p.company_id = ? AND p.deleted_at IS NULL
  AND (? = '' OR p.name LIKE ? ESCAPE '\' OR p.sku LIKE ? ESCAPE '\'
       OR p.barcode = ?)
ORDER BY p.name COLLATE NOCASE, p.sku
LIMIT ?
''',
          variables: [
            Variable.withString(warehouseId),
            Variable.withString(companyId),
            Variable.withString(term),
            Variable.withString(like),
            Variable.withString(like),
            Variable.withString(term),
            Variable.withInt(limit),
          ],
        )
        .get();
    return [
      for (final r in rows)
        StockLine(
          productId: r.read<String>('id'),
          sku: r.read<String>('sku'),
          name: r.read<String>('name'),
          barcode: r.readNullable<String>('barcode'),
          unitCode: r.readNullable<String>('unit_code'),
          onHandE4: r.read<int>('qty'),
          costPriceE4: r.read<int>('cost'),
        ),
    ];
  }

  @override
  Future<StockSummary> summary() async {
    final rows = await _db
        .customSelect(
          r'''
SELECT p.cost_price_e4 AS cost, COALESCE(SUM(b.qty_e4), 0) AS qty
FROM products p
LEFT JOIN stock_balances b ON b.product_id = p.id AND b.company_id = p.company_id
WHERE p.company_id = ? AND p.deleted_at IS NULL
GROUP BY p.id
''',
          variables: [Variable.withString(companyId)],
        )
        .get();
    var stocked = 0;
    var out = 0;
    var value = BigInt.zero;
    for (final r in rows) {
      final qty = r.read<int>('qty');
      final cost = r.read<int>('cost');
      if (qty > 0) {
        stocked++;
        value += BigInt.from(qty) * BigInt.from(cost);
      } else {
        out++;
      }
    }
    final count = _db.stockMovements.id.count();
    final total =
        await (_db.selectOnly(_db.stockMovements)
              ..addColumns([count])
              ..where(_db.stockMovements.companyId.equals(companyId)))
            .map((r) => r.read(count) ?? 0)
            .getSingle();
    final last =
        await (_db.select(_db.stockMovements)
              ..where((m) => m.companyId.equals(companyId))
              ..orderBy([(m) => OrderingTerm.desc(m.occurredAt)])
              ..limit(1))
            .getSingleOrNull();
    return StockSummary(
      productCount: rows.length,
      stockedCount: stocked,
      outOfStockCount: out,
      movementCount: total,
      lastMovementAt: last?.occurredAt,
      valueE4: value ~/ BigInt.from(kE4),
    );
  }

  Future<List<MovementView>> _views({
    String? productId,
    required int limit,
  }) async {
    final m = _db.stockMovements;
    final p = _db.products;
    final w = _db.warehouses;
    final q = _db.select(m).join([
      innerJoin(p, p.id.equalsExp(m.productId)),
      innerJoin(w, w.id.equalsExp(m.warehouseId)),
    ]);
    q.where(
      productId == null
          ? m.companyId.equals(companyId)
          : m.companyId.equals(companyId) & m.productId.equals(productId),
    );
    q.orderBy([
      OrderingTerm.desc(m.occurredAt),
      OrderingTerm.desc(m.createdAt),
    ]);
    q.limit(limit);
    final rows = await q.get();
    final ids = [for (final r in rows) r.readTable(m).id];
    final reversed = <String>{};
    if (ids.isNotEmpty) {
      final rr = await (_db.select(
        m,
      )..where((t) => t.reversesMovementId.isIn(ids))).get();
      for (final t in rr) {
        final target = t.reversesMovementId;
        if (target != null) reversed.add(target);
      }
    }
    return [
      for (final r in rows)
        MovementView(
          id: r.readTable(m).id,
          productId: r.readTable(m).productId,
          productName: r.readTable(p).name,
          sku: r.readTable(p).sku,
          warehouseName: r.readTable(w).name,
          type: StockMovementType.fromDb(r.readTable(m).movementType),
          qtyE4: r.readTable(m).qtyE4,
          unitCostE4: r.readTable(m).unitCostE4,
          reason: r.readTable(m).reason,
          occurredAt: r.readTable(m).occurredAt,
          createdBy: r.readTable(m).createdBy,
          reversesMovementId: r.readTable(m).reversesMovementId,
          isReversed: reversed.contains(r.readTable(m).id),
        ),
    ];
  }

  @override
  Future<List<MovementView>> recentMovements({int limit = 8}) =>
      _views(limit: limit);

  @override
  Future<List<MovementView>> movementsForProduct(
    String productId, {
    int limit = 100,
  }) => _views(productId: productId, limit: limit);

  @override
  Future<void> record(MovementRequest request) async {
    InventoryRules.validate(request);
    await _db.transaction(
      () => _insertMovement(
        productId: request.productId,
        warehouseId: request.warehouseId,
        type: request.type,
        signedQtyE4: InventoryRules.signedQuantity(
          request.type,
          request.quantityE4,
        ),
        unitCostE4: request.unitCostE4,
        reason: _blankToNull(request.reason),
        requestId: request.requestId,
        actor: request.actor.trim(),
        occurredAt: request.occurredAt,
      ),
    );
  }

  @override
  Future<void> reverse({
    required String movementId,
    required String requestId,
    required String actor,
    String? reason,
  }) async {
    if (requestId.isEmpty || requestId.length > 80 || actor.trim().isEmpty) {
      throw const InventoryException('The request is not valid.');
    }
    await _db.transaction(() async {
      final done = await _existingRequest(requestId);
      if (done) return;
      final original =
          await (_db.select(_db.stockMovements)..where(
                (m) => m.id.equals(movementId) & m.companyId.equals(companyId),
              ))
              .getSingleOrNull();
      if (original == null) {
        throw const InventoryException('That stock movement was not found.');
      }
      if (original.movementType == StockMovementType.reversal.dbValue) {
        throw const InventoryException('A reversal cannot be reversed.');
      }
      final already =
          await (_db.select(_db.stockMovements)..where(
                (m) =>
                    m.companyId.equals(companyId) &
                    m.reversesMovementId.equals(movementId),
              ))
              .get();
      if (already.isNotEmpty) {
        throw const InventoryException('This movement was already reversed.');
      }
      final note = _blankToNull(reason);
      var text = note == null ? 'Reversal' : 'Reversal: $note';
      if (text.length > kMaxReasonLength) {
        text = text.substring(0, kMaxReasonLength);
      }
      await _insertMovement(
        productId: original.productId,
        warehouseId: original.warehouseId,
        type: StockMovementType.reversal,
        signedQtyE4: -original.qtyE4,
        unitCostE4: original.unitCostE4,
        reason: text,
        requestId: requestId,
        actor: actor.trim(),
        reversesId: movementId,
      );
    });
  }

  String? _blankToNull(String? s) {
    final t = s?.trim() ?? '';
    return t.isEmpty ? null : t;
  }

  Future<bool> _existingRequest(String requestId) async {
    final dup =
        await (_db.select(_db.stockMovements)..where(
              (m) =>
                  m.companyId.equals(companyId) &
                  m.clientRequestId.equals(requestId),
            ))
            .getSingleOrNull();
    return dup != null;
  }

  Future<void> _insertMovement({
    required String productId,
    required String warehouseId,
    required StockMovementType type,
    required int signedQtyE4,
    required String requestId,
    required String actor,
    int? unitCostE4,
    String? reason,
    String? reversesId,
    DateTime? occurredAt,
  }) async {
    // Repeated request IDs are idempotent only when the stock change matches.
    final existing =
        await (_db.select(_db.stockMovements)..where(
              (m) =>
                  m.companyId.equals(companyId) &
                  m.clientRequestId.equals(requestId),
            ))
            .getSingleOrNull();

    if (existing != null) {
      final sameChange =
          existing.productId == productId &&
          existing.warehouseId == warehouseId &&
          existing.movementType == type.dbValue &&
          existing.qtyE4 == signedQtyE4 &&
          existing.unitCostE4 == unitCostE4 &&
          existing.reason == reason &&
          existing.reversesMovementId == reversesId;

      if (sameChange) return;

      throw InventoryException(
        'Request ID has already been used for a different stock change.',
      );
    }

    final product =
        await (_db.select(_db.products)..where(
              (p) =>
                  p.id.equals(productId) &
                  p.companyId.equals(companyId) &
                  p.deletedAt.isNull(),
            ))
            .getSingleOrNull();
    if (product == null) {
      throw const InventoryException('That product no longer exists.');
    }
    final warehouse =
        await (_db.select(_db.warehouses)..where(
              (w) =>
                  w.id.equals(warehouseId) &
                  w.companyId.equals(companyId) &
                  w.deletedAt.isNull(),
            ))
            .getSingleOrNull();
    if (warehouse == null) {
      throw const InventoryException('That warehouse no longer exists.');
    }
    final balance =
        await (_db.select(_db.stockBalances)..where(
              (b) =>
                  b.companyId.equals(companyId) &
                  b.productId.equals(productId) &
                  b.warehouseId.equals(warehouseId),
            ))
            .getSingleOrNull();
    final current = balance?.qtyE4 ?? 0;
    final next = current + signedQtyE4;
    if (next < 0) {
      throw InventoryException(
        'Not enough stock. On hand: ${formatQtyE4(current)}.',
      );
    }
    final now = _clock();
    await _db
        .into(_db.stockMovements)
        .insert(
          StockMovementsCompanion.insert(
            id: generateUuidV4(),
            companyId: companyId,
            productId: productId,
            warehouseId: warehouseId,
            movementType: type.dbValue,
            qtyE4: signedQtyE4,
            unitCostE4: Value(unitCostE4),
            reason: Value(reason),
            reversesMovementId: Value(reversesId),
            clientRequestId: requestId,
            occurredAt: occurredAt ?? now,
            createdAt: now,
            createdBy: actor,
            originDeviceId: Value(originDeviceId),
          ),
        );
    await _db
        .into(_db.stockBalances)
        .insertOnConflictUpdate(
          StockBalancesCompanion.insert(
            companyId: companyId,
            productId: productId,
            warehouseId: warehouseId,
            qtyE4: next,
            updatedAt: now,
          ),
        );
  }

  @override
  Future<List<String>> reconcile() async {
    final rows = await _db
        .customSelect(
          r'''
SELECT p.sku AS sku, p.name AS name, w.code AS wcode,
       s.total AS total, COALESCE(b.qty_e4, 0) AS bal
FROM (SELECT product_id, warehouse_id, SUM(qty_e4) AS total
      FROM stock_movements WHERE company_id = ?
      GROUP BY product_id, warehouse_id) s
JOIN products p ON p.id = s.product_id
JOIN warehouses w ON w.id = s.warehouse_id
LEFT JOIN stock_balances b ON b.product_id = s.product_id
  AND b.warehouse_id = s.warehouse_id AND b.company_id = ?
WHERE s.total != COALESCE(b.qty_e4, 0)
UNION ALL
SELECT p.sku, p.name, w.code, 0, b.qty_e4
FROM stock_balances b
JOIN products p ON p.id = b.product_id
JOIN warehouses w ON w.id = b.warehouse_id
WHERE b.company_id = ? AND b.qty_e4 != 0
  AND NOT EXISTS (SELECT 1 FROM stock_movements m
    WHERE m.company_id = b.company_id AND m.product_id = b.product_id
      AND m.warehouse_id = b.warehouse_id)
''',
          variables: [
            Variable.withString(companyId),
            Variable.withString(companyId),
            Variable.withString(companyId),
          ],
        )
        .get();
    return [
      for (final r in rows)
        'Stock mismatch for ${r.read<String>('sku')} (${r.read<String>('name')}) '
            'in ${r.read<String>('wcode')}: ledger total '
            '${formatQtyE4(r.read<int>('total'))}, balance '
            '${formatQtyE4(r.read<int>('bal'))}.',
    ];
  }
}
