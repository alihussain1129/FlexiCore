import 'dart:io';

import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/profile/data/local_profile_repository.dart';
import 'package:flexicore_client/features/profile/domain/business_profile.dart';
import 'package:flexicore_client/features/profile/presentation/profile_controller.dart';
import 'package:flutter_test/flutter_test.dart';

const _first = ProfileInput(
  businessName: 'Noor Pharmacy',
  industry: 'Pharmacy',
  currency: 'PKR',
  ownerName: 'Ali Hussain',
);

void main() {
  late Directory dir;
  setUp(() => dir = Directory.systemTemp.createTempSync('fc_pupd_'));
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  test('editing an existing profile updates it and keeps createdAt', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    final created = await repo.create(_first);

    final c = ProfileController(repo);
    await c.load();
    final ok = await c.save(
      const ProfileInput(
        businessName: 'Noor Medical',
        industry: 'Retail',
        currency: 'USD',
        ownerName: 'Ali Hussain',
      ),
    );
    expect(ok, isTrue);
    expect(c.profile?.businessName, 'Noor Medical');
    expect(c.profile?.createdAt, created.createdAt);

    final stored = await repo.load();
    expect(stored?.businessName, 'Noor Medical');
    expect(stored?.currency, 'USD');
    await db.close();
  });

  test('a rejected edit leaves the stored profile unchanged', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    await repo.create(_first);

    final c = ProfileController(repo);
    await c.load();
    final ok = await c.save(
      const ProfileInput(
        businessName: '',
        industry: 'Pharmacy',
        currency: 'PKR',
        ownerName: 'Ali Hussain',
      ),
    );
    expect(ok, isFalse);
    expect(c.fieldErrors.containsKey('businessName'), isTrue);
    expect((await repo.load())?.businessName, 'Noor Pharmacy');
    await db.close();
  });
}