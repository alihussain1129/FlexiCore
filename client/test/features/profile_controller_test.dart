import 'dart:io';

import 'package:flexicore_client/data/local/database_opener.dart';
import 'package:flexicore_client/features/profile/data/local_profile_repository.dart';
import 'package:flexicore_client/features/profile/domain/business_profile.dart';
import 'package:flexicore_client/features/profile/presentation/profile_controller.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  late Directory dir;
  setUp(() => dir = Directory.systemTemp.createTempSync('fc_ctrl_'));
  tearDown(() {
    try {
      dir.deleteSync(recursive: true);
    } catch (_) {}
  });

  const input = ProfileInput(
    businessName: 'Noor Pharmacy',
    industry: 'Pharmacy',
    currency: 'PKR',
    ownerName: 'Ali Hussain',
  );

  test('double submit saves once and the second call is ignored', () async {
    final db = await openAppDatabase(directory: dir);
    final c = ProfileController(
      LocalProfileRepository(db, companyId: 'local-company'),
    );
    final a = c.save(input);
    final b = c.save(input);
    expect(await b, isFalse);
    expect(await a, isTrue);
    expect(c.profile?.businessName, 'Noor Pharmacy');
    expect(c.saving, isFalse);
    await db.close();
  });

  test('invalid input exposes field errors and leaves no profile', () async {
    final db = await openAppDatabase(directory: dir);
    final c = ProfileController(
      LocalProfileRepository(db, companyId: 'local-company'),
    );
    final ok = await c.save(
      const ProfileInput(
        businessName: '',
        industry: 'Retail',
        currency: 'PKR',
        ownerName: 'Ali',
      ),
    );
    expect(ok, isFalse);
    expect(c.fieldErrors.containsKey('businessName'), isTrue);
    expect(c.profile, isNull);
    await db.close();
  });

  test('an existing profile is loaded, not replaced', () async {
    final db = await openAppDatabase(directory: dir);
    final repo = LocalProfileRepository(db, companyId: 'local-company');
    await repo.create(input);

    final c = ProfileController(repo);
    await c.load();
    expect(c.profile?.ownerName, 'Ali Hussain');
    await db.close();
  });
}