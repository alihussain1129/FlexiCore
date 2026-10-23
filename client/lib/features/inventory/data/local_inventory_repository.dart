import 'package:drift/drift.dart';

import '../../../core/ids.dart';

import '../../../data/local/app_database.dart';
import '../domain/inventory_domain.dart';
import '../domain/stock_breakdown.dart';

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
    String? warehouseId,
    StockMovementType? type,
    DateTime? from,
    DateTime? to,
    required int limit,
  }) async {
    final m = _db.stockMovements;
    final p = _db.products;
    final w = _db.warehouses;
    final q = _db.select(m).join([
      innerJoin(p, p.id.equalsExp(m.productId)),
      innerJoin(w, w.id.equalsExp(m.warehouseId)),
    ]);
    Expression<bool> cond = m.companyId.equals(companyId);
    if (productId != null) cond = cond & m.productId.equals(productId);
    if (warehouseId != null) cond = cond & m.warehouseId.equals(warehouseId);
    if (type != null) cond = cond & m.movementType.equals(type.dbValue);
    if (from != null) cond = cond & m.occurredAt.isBiggerOrEqualValue(from);
    if (to != null) cond = cond & m.occurredAt.isSmallerThanValue(to);
    q.where(cond);
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

  /// Movements with optional filters, newest first. [to] is exclusive.
  Future<List<MovementView>> ledger({
    String? productId,
    String? warehouseId,
    StockMovementType? type,
    DateTime? from,
    DateTime? to,
    int limit = 200,
  }) => _views(
    productId: productId,
    warehouseId: warehouseId,
    type: type,
    from: from,
    to: to,
    limit: limit,
  );

  /// Per product in one warehouse: how the balance was built up.
  Future<List<StockBreakdownLine>> breakdown({
    required String warehouseId,
    int limit = 1000,
  }) async {
    final rows = await _db
        .customSelect(
          r'''
SELECT p.id AS id, p.sku AS sku, p.name AS name, u.code AS unit_code,
       COALESCE(s.opening, 0) AS opening, COALESCE(s.received, 0) AS received,
       COALESCE(s.issued, 0) AS issued, COALESCE(s.other, 0) AS other,
       COALESCE(b.qty_e4, 0) AS on_hand
FROM products p
LEFT JOIN units u ON u.id = p.unit_id AND u.deleted_at IS NULL
LEFT JOIN (
  SELECT product_id,
    SUM(CASE WHEN movement_type = 'opening' THEN qty_e4 ELSE 0 END) AS opening,
    SUM(CASE WHEN movement_type = 'receipt' THEN qty_e4 ELSE 0 END) AS received,
    SUM(CASE WHEN movement_type = 'issue' THEN qty_e4 ELSE 0 END) AS issued,
    SUM(CASE WHEN movement_type IN ('adjustment', 'reversal')
        THEN qty_e4 ELSE 0 END) AS other
  FROM stock_movements
  WHERE company_id = ? AND warehouse_id = ?
  GROUP BY product_id
) s ON s.product_id = p.id
LEFT JOIN stock_balances b ON b.product_id = p.id
  AND b.company_id = p.company_id AND b.warehouse_id = ?
WHERE p.company_id = ? AND p.deleted_at IS NULL
  AND (s.product_id IS NOT NULL OR COALESCE(b.qty_e4, 0) != 0)
ORDER BY p.name COLLATE NOCASE, p.sku
LIMIT ?
''',
          variables: [
            Variable.withString(companyId),
            Variable.withString(warehouseId),
            Variable.withString(warehouseId),
            Variable.withString(companyId),
            Variable.withInt(limit),
          ],
        )
        .get();
    return [
      for (final r in rows)
        StockBreakdownLine(
          productId: r.read<String>('id'),
          sku: r.read<String>('sku'),
          name: r.read<String>('name'),
          unitCode: r.readNullable<String>('unit_code'),
          openingE4: r.read<int>('opening'),
          receivedE4: r.read<int>('received'),
          issuedE4: r.read<int>('issued'),
          otherE4: r.read<int>('other'),
          onHandE4: r.read<int>('on_hand'),
        ),
    ];
  }

  /// Adds a warehouse. The code is upper-cased and must be unique.
  Future<Warehouse> createWarehouse({
    required String code,
    required String name,
  }) async {
    final c = code.trim().toUpperCase();
    final n = name.trim();
    if (!RegExp(r'^[A-Z0-9_-]{1,20}$').hasMatch(c)) {
      throw const InventoryException(
        'The code must be 1 to 20 letters or numbers.',
      );
    }
    if (n.isEmpty || n.length > 80) {
      throw const InventoryException(
        'Enter a warehouse name (up to 80 characters).',
      );
    }
    return _db.transaction(() async {
      final dup =
          await (_db.select(_db.warehouses)..where(
                (w) =>
                    w.companyId.equals(companyId) &
                    w.code.equals(c) &
                    w.deletedAt.isNull(),
              ))
              .getSingleOrNull();
      if (dup != null) {
        throw const InventoryException(
          'A warehouse with that code already exists.',
        );
      }
      final id = generateUuidV4();
      await _db
          .into(_db.warehouses)
          .insert(
            WarehousesCompanion.insert(
              id: id,
              companyId: companyId,
              code: c,
              name: n,
              updatedAt: _clock(),
              originDeviceId: Value(originDeviceId),
            ),
          );
      return Warehouse(id: id, code: c, name: n, isDefault: false);
    });
  }

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
      final prior =
          await (_db.select(_db.stockMovements)..where(
                (m) =>
                    m.companyId.equals(companyId) &
                    m.clientRequestId.equals(requestId),
              ))
              .getSingleOrNull();
      if (prior != null) {
        // A retry of the same reversal is fine. Anything else is a conflict.
        if (prior.movementType == StockMovementType.reversal.dbValue &&
            prior.reversesMovementId == movementId) {
          return;
        }
        throw const InventoryException(
          'Request ID has already been used for a different stock change.',
        );
      }
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
    if (type == StockMovementType.opening) {
      final open = await _db
          .customSelect(
            r'''
SELECT COUNT(*) AS c FROM stock_movements m
WHERE m.company_id = ? AND m.product_id = ? AND m.warehouse_id = ?
  AND m.movement_type = 'opening'
  AND NOT EXISTS (SELECT 1 FROM stock_movements r
                  WHERE r.reverses_movement_id = m.id)
''',
            variables: [
              Variable.withString(companyId),
              Variable.withString(productId),
              Variable.withString(warehouseId),
            ],
          )
          .getSingle();
      if (open.read<int>('c') > 0) {
        throw const InventoryException(
          'Opening stock was already entered for this product here. '
          'Use Receive stock or Adjust stock instead.',
        );
      }
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
