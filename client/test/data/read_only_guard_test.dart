import 'dart:io';

import 'package:drift/drift.dart' show Value;
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/data/local/app_database.dart';
import 'package:flexicore_client/data/local/database_opener.dart';

final _readOnlyError = predicate<Object>(
  (e) =>
      e is ReadOnlyLicenseException ||
      e.toString().contains('FlexiCore is read-only'),
  'a read-only license error',
);

UnitsCompanion _unit(String id) => UnitsCompanion.insert(
  id: id,
  companyId: 'c1',
  code: 'U$id',
  name: 'Unit $id',
  updatedAt: DateTime.utc(2026, 1, 1),
);

void main() {
  late Directory dir;
  setUp(() => dir = Directory.systemTemp.createTempSync('fc_guard_'));
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  Future<int> unitCount(AppDatabase db) async =>
      (await db.select(db.units).get()).length;

  test(
    'read-only: insert, update, delete and raw writes throw; reads work',
    () async {
      var readOnly = false;
      final db = await openAppDatabase(
        directory: dir,
        isReadOnly: () => readOnly,
      );
      addTearDown(db.close);
      await db.into(db.units).insert(_unit('u1'));

      readOnly = true;
      await expectLater(
        () => db.into(db.units).insert(_unit('u2')),
        throwsA(_readOnlyError),
      );
      await expectLater(
        () => (db.update(db.units)..where((t) => t.id.equals('u1'))).write(
          const UnitsCompanion(name: Value('Changed')),
        ),
        throwsA(_readOnlyError),
      );
      await expectLater(
        () => db.delete(db.units).go(),
        throwsA(_readOnlyError),
      );
      await expectLater(
        () => db.customStatement(
          "INSERT INTO units (id, company_id, code, name, updated_at) "
          "VALUES ('x', 'c1', 'Ux', 'X', 0)",
        ),
        throwsA(_readOnlyError),
      );

      expect(await unitCount(db), 1);
      final row = await db.select(db.units).getSingle();
      expect(row.name, 'Unit u1');
      final n = await db
          .customSelect('SELECT COUNT(*) AS n FROM units')
          .getSingle();
      expect(n.read<int>('n'), 1);
    },
  );

  test('renewing the license unlocks writes without reopening', () async {
    var readOnly = true;
    final db = await openAppDatabase(
      directory: dir,
      isReadOnly: () => readOnly,
    );
    addTearDown(db.close);
    await expectLater(
      () => db.into(db.units).insert(_unit('u1')),
      throwsA(_readOnlyError),
    );

    readOnly = false;
    await db.into(db.units).insert(_unit('u1'));
    expect(await unitCount(db), 1);
  });

  test(
    'a blocked write inside a transaction rolls the whole thing back',
    () async {
      var readOnly = false;
      final db = await openAppDatabase(
        directory: dir,
        isReadOnly: () => readOnly,
      );
      addTearDown(db.close);

      await expectLater(
        db.transaction(() async {
          await db.into(db.units).insert(_unit('u1'));
          readOnly = true;
          await db.into(db.units).insert(_unit('u2'));
        }),
        throwsA(_readOnlyError),
      );

      readOnly = false;
      expect(await unitCount(db), 0);
    },
  );

  test('opening and migrating works even when already read-only', () async {
    final db = await openAppDatabase(directory: dir, isReadOnly: () => true);
    addTearDown(db.close);
    expect(await unitCount(db), 0);
    await expectLater(
      () => db.into(db.units).insert(_unit('u1')),
      throwsA(_readOnlyError),
    );
  });

  test('without a callback nothing is blocked', () async {
    final db = await openAppDatabase(directory: dir);
    addTearDown(db.close);
    await db.into(db.units).insert(_unit('u1'));
    expect(await unitCount(db), 1);
  });
}
