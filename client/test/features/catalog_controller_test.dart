import 'package:drift/drift.dart' show driftRuntimeOptions;
import 'package:drift/native.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/features/catalog/data/local_catalog_repository.dart';
import 'package:flexicore_client/features/catalog/presentation/catalog_controller.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  driftRuntimeOptions.dontWarnAboutMultipleDatabases = true;

  late AppDatabase db;
  late CatalogController controller;

  setUp(() {
    db = AppDatabase(NativeDatabase.memory());
    controller = CatalogController(
      LocalCatalogRepository(db, companyId: 'company-1'),
    );
  });

  tearDown(() async => db.close());

  test('add unit and product, then see them after reload', () async {
    expect(await controller.addUnit('PCS', 'Pieces'), isNull);
    final unit = controller.units.single;

    expect(
      await controller.addProduct(
        sku: 'W-1',
        name: 'Widget',
        unitId: unit.id,
        salePrice: '12.50',
        costPrice: '',
      ),
      isNull,
    );

    expect(controller.products.single.salePriceE4, 125000);
    expect(controller.products.single.costPriceE4, 0);
    expect(controller.products.single.unitId, unit.id);
  });

  test('returns readable messages instead of throwing', () async {
    await controller.addUnit('PCS', 'Pieces');
    expect(
      await controller.addUnit('PCS', 'Again'),
      contains('already exists'),
    );
    expect(
      await controller.addProduct(
        sku: 'S1',
        name: 'X',
        salePrice: 'abc',
        costPrice: '0',
      ),
      contains('Sale price'),
    );
    expect(controller.products, isEmpty);
  });

  test('a unit in use cannot be deleted; message is returned', () async {
    await controller.addUnit('BOX', 'Box');
    final unit = controller.units.single;
    await controller.addProduct(
      sku: 'B-1',
      name: 'Boxed',
      unitId: unit.id,
      salePrice: '1',
      costPrice: '1',
    );
    expect(await controller.removeUnit(unit), contains('used by'));
    expect(controller.units.length, 1);
  });

  test(
    'edit uses the loaded version; a stale edit reloads and conflicts',
    () async {
      await controller.addUnit('KG', 'Kilogram');
      final stale = controller.units.single;
      expect(await controller.saveUnit(stale, 'KG', 'Kilogramme'), isNull);
      expect(
        await controller.saveUnit(stale, 'KG', 'Stale'),
        contains('changed elsewhere'),
      );
      expect(controller.units.single.name, 'Kilogramme');
    },
  );

  test('search filters products', () async {
    await controller.addProduct(
      sku: 'A1',
      name: 'Apple',
      salePrice: '1',
      costPrice: '1',
    );
    await controller.addProduct(
      sku: 'B1',
      name: 'Banana',
      salePrice: '1',
      costPrice: '1',
    );
    await controller.search('ban');
    expect(controller.products.map((p) => p.name), ['Banana']);
    await controller.search('');
    expect(controller.products.length, 2);
  });
}
