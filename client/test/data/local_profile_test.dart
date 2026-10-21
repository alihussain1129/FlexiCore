import 'dart:io';

import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/profile/data/local_profile_repository.dart';
import 'package:flexicore_client/features/profile/domain/business_profile.dart';
import 'package:flutter_test/flutter_test.dart';

const _valid = ProfileInput(
  businessName: ' Noor Pharmacy ',
  industry: 'Pharmacy',
  currency: 'pkr',
  ownerName: 'Ali Hussain',
);

void main() {
  late Directory dir;

  setUp(() => dir = Directory.systemTemp.createTempSync('fc_profile_'));
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  test('creates a valid profile, trimming and upper-casing currency', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    expect(await repo.load(), isNull);

    final p = await repo.create(_valid);
    expect(p.businessName, 'Noor Pharmacy');
    expect(p.currency, 'PKR');
    expect(p.createdAt, isNotNull);
    await db.close();
  });

  test('rejects missing and invalid values with field errors', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    await expectLater(
      repo.create(
        const ProfileInput(
          businessName: '  ',
          industry: '',
          currency: 'RUPEE',
          ownerName: '',
        ),
      ),
      throwsA(
        isA<ProfileException>().having(
          (e) => e.fieldErrors.keys.toSet(),
          'fields',
          {'businessName', 'industry', 'currency', 'ownerName'},
        ),
      ),
    );
    expect(await repo.load(), isNull);
    await db.close();
  });

  test('profile survives closing and reopening the database', () async {
    var db = await openAppDatabase(directory: dir);
    await LocalProfileRepository(db, companyId: 'local-company').create(_valid);
    await db.close();

    db = await openAppDatabase(directory: dir);
    final loaded = await LocalProfileRepository(
      db,
      companyId: 'local-company',
    ).load();
    expect(loaded?.businessName, 'Noor Pharmacy');
    expect(loaded?.ownerName, 'Ali Hussain');
    await db.close();
  });

  test('a second create never replaces the existing profile', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    final first = await repo.create(_valid);

    await expectLater(
      repo.create(
        const ProfileInput(
          businessName: 'Other',
          industry: 'Retail',
          currency: 'USD',
          ownerName: 'Someone',
        ),
      ),
      throwsA(isA<ProfileException>()),
    );
    final after = await repo.load();
    expect(after?.businessName, 'Noor Pharmacy');
    expect(after?.createdAt, first.createdAt);
    await db.close();
  });

  test('update changes fields and keeps createdAt', () async {
    final db = await openAppDatabase(directory: dir);
    var tick = DateTime(2026, 10, 21, 9);
    final repo = LocalProfileRepository(
      db,
      companyId: 'local-company',
      clock: () => tick = tick.add(const Duration(hours: 1)),
    );
    final first = await repo.create(_valid);
    final updated = await repo.update(
      const ProfileInput(
        businessName: 'Noor Medical',
        industry: 'Pharmacy',
        currency: 'USD',
        ownerName: 'Ali Hussain',
      ),
    );
    expect(updated.businessName, 'Noor Medical');
    expect(updated.currency, 'USD');
    expect(updated.createdAt, first.createdAt);
    expect(updated.updatedAt.isAfter(first.updatedAt), isTrue);
    await db.close();
  });

  test('update without a profile fails and creates nothing', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    await expectLater(repo.update(_valid), throwsA(isA<ProfileException>()));
    expect(await repo.load(), isNull);
    await db.close();
  });

  test('a corrupt database file is reported and never deleted', () async {
    final file = File('${dir.path}/flexicore_local.sqlite');
    final garbage = List<int>.generate(2048, (i) => i % 251);
    file.writeAsBytesSync(garbage);

    await expectLater(
      openAppDatabase(directory: dir),
      throwsA(isA<LocalDatabaseException>()),
    );
    expect(file.existsSync(), isTrue);
    expect(file.readAsBytesSync(), garbage);
  });

  test('profile code has no network or API dependency', () {
    final files = Directory('lib/features/profile')
        .listSync(recursive: true)
        .whereType<File>()
        .where((f) => f.path.endsWith('.dart'));
    expect(files, isNotEmpty);
    for (final f in files) {
      final text = f.readAsStringSync();
      expect(text.contains('package:http'), isFalse, reason: f.path);
      expect(text.contains('api_client'), isFalse, reason: f.path);
    }
  });
}