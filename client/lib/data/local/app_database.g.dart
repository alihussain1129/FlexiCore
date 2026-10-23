// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app_database.dart';

// ignore_for_file: type=lint
class $UnitsTable extends Units with TableInfo<$UnitsTable, UnitRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $UnitsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _codeMeta = const VerificationMeta('code');
  @override
  late final GeneratedColumn<String> code = GeneratedColumn<String>(
    'code',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
    'name',
    aliasedName,
    false,
    check: () => ComparableExpr(name.length).isBetweenValues(1, 160),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _versionMeta = const VerificationMeta(
    'version',
  );
  @override
  late final GeneratedColumn<int> version = GeneratedColumn<int>(
    'version',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(1),
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _originDeviceIdMeta = const VerificationMeta(
    'originDeviceId',
  );
  @override
  late final GeneratedColumn<String> originDeviceId = GeneratedColumn<String>(
    'origin_device_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _deletedAtMeta = const VerificationMeta(
    'deletedAt',
  );
  @override
  late final GeneratedColumn<DateTime> deletedAt = GeneratedColumn<DateTime>(
    'deleted_at',
    aliasedName,
    true,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    code,
    name,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'units';
  @override
  VerificationContext validateIntegrity(
    Insertable<UnitRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('code')) {
      context.handle(
        _codeMeta,
        code.isAcceptableOrUnknown(data['code']!, _codeMeta),
      );
    } else if (isInserting) {
      context.missing(_codeMeta);
    }
    if (data.containsKey('name')) {
      context.handle(
        _nameMeta,
        name.isAcceptableOrUnknown(data['name']!, _nameMeta),
      );
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('version')) {
      context.handle(
        _versionMeta,
        version.isAcceptableOrUnknown(data['version']!, _versionMeta),
      );
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    if (data.containsKey('origin_device_id')) {
      context.handle(
        _originDeviceIdMeta,
        originDeviceId.isAcceptableOrUnknown(
          data['origin_device_id']!,
          _originDeviceIdMeta,
        ),
      );
    }
    if (data.containsKey('deleted_at')) {
      context.handle(
        _deletedAtMeta,
        deletedAt.isAcceptableOrUnknown(data['deleted_at']!, _deletedAtMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  UnitRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return UnitRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      code: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}code'],
      )!,
      name: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}name'],
      )!,
      version: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}version'],
      )!,
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
      originDeviceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}origin_device_id'],
      ),
      deletedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}deleted_at'],
      ),
    );
  }

  @override
  $UnitsTable createAlias(String alias) {
    return $UnitsTable(attachedDatabase, alias);
  }
}

class UnitRow extends DataClass implements Insertable<UnitRow> {
  final String id;
  final String companyId;
  final String code;
  final String name;
  final int version;
  final DateTime updatedAt;
  final String? originDeviceId;
  final DateTime? deletedAt;
  const UnitRow({
    required this.id,
    required this.companyId,
    required this.code,
    required this.name,
    required this.version,
    required this.updatedAt,
    this.originDeviceId,
    this.deletedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['code'] = Variable<String>(code);
    map['name'] = Variable<String>(name);
    map['version'] = Variable<int>(version);
    map['updated_at'] = Variable<DateTime>(updatedAt);
    if (!nullToAbsent || originDeviceId != null) {
      map['origin_device_id'] = Variable<String>(originDeviceId);
    }
    if (!nullToAbsent || deletedAt != null) {
      map['deleted_at'] = Variable<DateTime>(deletedAt);
    }
    return map;
  }

  UnitsCompanion toCompanion(bool nullToAbsent) {
    return UnitsCompanion(
      id: Value(id),
      companyId: Value(companyId),
      code: Value(code),
      name: Value(name),
      version: Value(version),
      updatedAt: Value(updatedAt),
      originDeviceId: originDeviceId == null && nullToAbsent
          ? const Value.absent()
          : Value(originDeviceId),
      deletedAt: deletedAt == null && nullToAbsent
          ? const Value.absent()
          : Value(deletedAt),
    );
  }

  factory UnitRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return UnitRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      code: serializer.fromJson<String>(json['code']),
      name: serializer.fromJson<String>(json['name']),
      version: serializer.fromJson<int>(json['version']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
      originDeviceId: serializer.fromJson<String?>(json['originDeviceId']),
      deletedAt: serializer.fromJson<DateTime?>(json['deletedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'code': serializer.toJson<String>(code),
      'name': serializer.toJson<String>(name),
      'version': serializer.toJson<int>(version),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
      'originDeviceId': serializer.toJson<String?>(originDeviceId),
      'deletedAt': serializer.toJson<DateTime?>(deletedAt),
    };
  }

  UnitRow copyWith({
    String? id,
    String? companyId,
    String? code,
    String? name,
    int? version,
    DateTime? updatedAt,
    Value<String?> originDeviceId = const Value.absent(),
    Value<DateTime?> deletedAt = const Value.absent(),
  }) => UnitRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    code: code ?? this.code,
    name: name ?? this.name,
    version: version ?? this.version,
    updatedAt: updatedAt ?? this.updatedAt,
    originDeviceId: originDeviceId.present
        ? originDeviceId.value
        : this.originDeviceId,
    deletedAt: deletedAt.present ? deletedAt.value : this.deletedAt,
  );
  UnitRow copyWithCompanion(UnitsCompanion data) {
    return UnitRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      code: data.code.present ? data.code.value : this.code,
      name: data.name.present ? data.name.value : this.name,
      version: data.version.present ? data.version.value : this.version,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
      originDeviceId: data.originDeviceId.present
          ? data.originDeviceId.value
          : this.originDeviceId,
      deletedAt: data.deletedAt.present ? data.deletedAt.value : this.deletedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('UnitRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('code: $code, ')
          ..write('name: $name, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    code,
    name,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is UnitRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.code == this.code &&
          other.name == this.name &&
          other.version == this.version &&
          other.updatedAt == this.updatedAt &&
          other.originDeviceId == this.originDeviceId &&
          other.deletedAt == this.deletedAt);
}

class UnitsCompanion extends UpdateCompanion<UnitRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> code;
  final Value<String> name;
  final Value<int> version;
  final Value<DateTime> updatedAt;
  final Value<String?> originDeviceId;
  final Value<DateTime?> deletedAt;
  final Value<int> rowid;
  const UnitsCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.code = const Value.absent(),
    this.name = const Value.absent(),
    this.version = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  UnitsCompanion.insert({
    required String id,
    required String companyId,
    required String code,
    required String name,
    this.version = const Value.absent(),
    required DateTime updatedAt,
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       code = Value(code),
       name = Value(name),
       updatedAt = Value(updatedAt);
  static Insertable<UnitRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? code,
    Expression<String>? name,
    Expression<int>? version,
    Expression<DateTime>? updatedAt,
    Expression<String>? originDeviceId,
    Expression<DateTime>? deletedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (code != null) 'code': code,
      if (name != null) 'name': name,
      if (version != null) 'version': version,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (originDeviceId != null) 'origin_device_id': originDeviceId,
      if (deletedAt != null) 'deleted_at': deletedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  UnitsCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? code,
    Value<String>? name,
    Value<int>? version,
    Value<DateTime>? updatedAt,
    Value<String?>? originDeviceId,
    Value<DateTime?>? deletedAt,
    Value<int>? rowid,
  }) {
    return UnitsCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      code: code ?? this.code,
      name: name ?? this.name,
      version: version ?? this.version,
      updatedAt: updatedAt ?? this.updatedAt,
      originDeviceId: originDeviceId ?? this.originDeviceId,
      deletedAt: deletedAt ?? this.deletedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (code.present) {
      map['code'] = Variable<String>(code.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (version.present) {
      map['version'] = Variable<int>(version.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (originDeviceId.present) {
      map['origin_device_id'] = Variable<String>(originDeviceId.value);
    }
    if (deletedAt.present) {
      map['deleted_at'] = Variable<DateTime>(deletedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('UnitsCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('code: $code, ')
          ..write('name: $name, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $ProductsTable extends Products
    with TableInfo<$ProductsTable, ProductRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $ProductsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _skuMeta = const VerificationMeta('sku');
  @override
  late final GeneratedColumn<String> sku = GeneratedColumn<String>(
    'sku',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _barcodeMeta = const VerificationMeta(
    'barcode',
  );
  @override
  late final GeneratedColumn<String> barcode = GeneratedColumn<String>(
    'barcode',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
    'name',
    aliasedName,
    false,
    check: () => ComparableExpr(name.length).isBetweenValues(1, 160),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _unitIdMeta = const VerificationMeta('unitId');
  @override
  late final GeneratedColumn<String> unitId = GeneratedColumn<String>(
    'unit_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES units (id)',
    ),
  );
  static const VerificationMeta _salePriceE4Meta = const VerificationMeta(
    'salePriceE4',
  );
  @override
  late final GeneratedColumn<int> salePriceE4 = GeneratedColumn<int>(
    'sale_price_e4',
    aliasedName,
    false,
    check: () => ComparableExpr(salePriceE4).isBiggerOrEqualValue(0),
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _costPriceE4Meta = const VerificationMeta(
    'costPriceE4',
  );
  @override
  late final GeneratedColumn<int> costPriceE4 = GeneratedColumn<int>(
    'cost_price_e4',
    aliasedName,
    false,
    check: () => ComparableExpr(costPriceE4).isBiggerOrEqualValue(0),
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _versionMeta = const VerificationMeta(
    'version',
  );
  @override
  late final GeneratedColumn<int> version = GeneratedColumn<int>(
    'version',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(1),
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _originDeviceIdMeta = const VerificationMeta(
    'originDeviceId',
  );
  @override
  late final GeneratedColumn<String> originDeviceId = GeneratedColumn<String>(
    'origin_device_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _deletedAtMeta = const VerificationMeta(
    'deletedAt',
  );
  @override
  late final GeneratedColumn<DateTime> deletedAt = GeneratedColumn<DateTime>(
    'deleted_at',
    aliasedName,
    true,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    sku,
    barcode,
    name,
    unitId,
    salePriceE4,
    costPriceE4,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'products';
  @override
  VerificationContext validateIntegrity(
    Insertable<ProductRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('sku')) {
      context.handle(
        _skuMeta,
        sku.isAcceptableOrUnknown(data['sku']!, _skuMeta),
      );
    } else if (isInserting) {
      context.missing(_skuMeta);
    }
    if (data.containsKey('barcode')) {
      context.handle(
        _barcodeMeta,
        barcode.isAcceptableOrUnknown(data['barcode']!, _barcodeMeta),
      );
    }
    if (data.containsKey('name')) {
      context.handle(
        _nameMeta,
        name.isAcceptableOrUnknown(data['name']!, _nameMeta),
      );
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('unit_id')) {
      context.handle(
        _unitIdMeta,
        unitId.isAcceptableOrUnknown(data['unit_id']!, _unitIdMeta),
      );
    }
    if (data.containsKey('sale_price_e4')) {
      context.handle(
        _salePriceE4Meta,
        salePriceE4.isAcceptableOrUnknown(
          data['sale_price_e4']!,
          _salePriceE4Meta,
        ),
      );
    }
    if (data.containsKey('cost_price_e4')) {
      context.handle(
        _costPriceE4Meta,
        costPriceE4.isAcceptableOrUnknown(
          data['cost_price_e4']!,
          _costPriceE4Meta,
        ),
      );
    }
    if (data.containsKey('version')) {
      context.handle(
        _versionMeta,
        version.isAcceptableOrUnknown(data['version']!, _versionMeta),
      );
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    if (data.containsKey('origin_device_id')) {
      context.handle(
        _originDeviceIdMeta,
        originDeviceId.isAcceptableOrUnknown(
          data['origin_device_id']!,
          _originDeviceIdMeta,
        ),
      );
    }
    if (data.containsKey('deleted_at')) {
      context.handle(
        _deletedAtMeta,
        deletedAt.isAcceptableOrUnknown(data['deleted_at']!, _deletedAtMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  ProductRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return ProductRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      sku: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}sku'],
      )!,
      barcode: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}barcode'],
      ),
      name: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}name'],
      )!,
      unitId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}unit_id'],
      ),
      salePriceE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}sale_price_e4'],
      )!,
      costPriceE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}cost_price_e4'],
      )!,
      version: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}version'],
      )!,
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
      originDeviceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}origin_device_id'],
      ),
      deletedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}deleted_at'],
      ),
    );
  }

  @override
  $ProductsTable createAlias(String alias) {
    return $ProductsTable(attachedDatabase, alias);
  }
}

class ProductRow extends DataClass implements Insertable<ProductRow> {
  final String id;
  final String companyId;
  final String sku;
  final String? barcode;
  final String name;
  final String? unitId;
  final int salePriceE4;
  final int costPriceE4;
  final int version;
  final DateTime updatedAt;
  final String? originDeviceId;
  final DateTime? deletedAt;
  const ProductRow({
    required this.id,
    required this.companyId,
    required this.sku,
    this.barcode,
    required this.name,
    this.unitId,
    required this.salePriceE4,
    required this.costPriceE4,
    required this.version,
    required this.updatedAt,
    this.originDeviceId,
    this.deletedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['sku'] = Variable<String>(sku);
    if (!nullToAbsent || barcode != null) {
      map['barcode'] = Variable<String>(barcode);
    }
    map['name'] = Variable<String>(name);
    if (!nullToAbsent || unitId != null) {
      map['unit_id'] = Variable<String>(unitId);
    }
    map['sale_price_e4'] = Variable<int>(salePriceE4);
    map['cost_price_e4'] = Variable<int>(costPriceE4);
    map['version'] = Variable<int>(version);
    map['updated_at'] = Variable<DateTime>(updatedAt);
    if (!nullToAbsent || originDeviceId != null) {
      map['origin_device_id'] = Variable<String>(originDeviceId);
    }
    if (!nullToAbsent || deletedAt != null) {
      map['deleted_at'] = Variable<DateTime>(deletedAt);
    }
    return map;
  }

  ProductsCompanion toCompanion(bool nullToAbsent) {
    return ProductsCompanion(
      id: Value(id),
      companyId: Value(companyId),
      sku: Value(sku),
      barcode: barcode == null && nullToAbsent
          ? const Value.absent()
          : Value(barcode),
      name: Value(name),
      unitId: unitId == null && nullToAbsent
          ? const Value.absent()
          : Value(unitId),
      salePriceE4: Value(salePriceE4),
      costPriceE4: Value(costPriceE4),
      version: Value(version),
      updatedAt: Value(updatedAt),
      originDeviceId: originDeviceId == null && nullToAbsent
          ? const Value.absent()
          : Value(originDeviceId),
      deletedAt: deletedAt == null && nullToAbsent
          ? const Value.absent()
          : Value(deletedAt),
    );
  }

  factory ProductRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return ProductRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      sku: serializer.fromJson<String>(json['sku']),
      barcode: serializer.fromJson<String?>(json['barcode']),
      name: serializer.fromJson<String>(json['name']),
      unitId: serializer.fromJson<String?>(json['unitId']),
      salePriceE4: serializer.fromJson<int>(json['salePriceE4']),
      costPriceE4: serializer.fromJson<int>(json['costPriceE4']),
      version: serializer.fromJson<int>(json['version']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
      originDeviceId: serializer.fromJson<String?>(json['originDeviceId']),
      deletedAt: serializer.fromJson<DateTime?>(json['deletedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'sku': serializer.toJson<String>(sku),
      'barcode': serializer.toJson<String?>(barcode),
      'name': serializer.toJson<String>(name),
      'unitId': serializer.toJson<String?>(unitId),
      'salePriceE4': serializer.toJson<int>(salePriceE4),
      'costPriceE4': serializer.toJson<int>(costPriceE4),
      'version': serializer.toJson<int>(version),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
      'originDeviceId': serializer.toJson<String?>(originDeviceId),
      'deletedAt': serializer.toJson<DateTime?>(deletedAt),
    };
  }

  ProductRow copyWith({
    String? id,
    String? companyId,
    String? sku,
    Value<String?> barcode = const Value.absent(),
    String? name,
    Value<String?> unitId = const Value.absent(),
    int? salePriceE4,
    int? costPriceE4,
    int? version,
    DateTime? updatedAt,
    Value<String?> originDeviceId = const Value.absent(),
    Value<DateTime?> deletedAt = const Value.absent(),
  }) => ProductRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    sku: sku ?? this.sku,
    barcode: barcode.present ? barcode.value : this.barcode,
    name: name ?? this.name,
    unitId: unitId.present ? unitId.value : this.unitId,
    salePriceE4: salePriceE4 ?? this.salePriceE4,
    costPriceE4: costPriceE4 ?? this.costPriceE4,
    version: version ?? this.version,
    updatedAt: updatedAt ?? this.updatedAt,
    originDeviceId: originDeviceId.present
        ? originDeviceId.value
        : this.originDeviceId,
    deletedAt: deletedAt.present ? deletedAt.value : this.deletedAt,
  );
  ProductRow copyWithCompanion(ProductsCompanion data) {
    return ProductRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      sku: data.sku.present ? data.sku.value : this.sku,
      barcode: data.barcode.present ? data.barcode.value : this.barcode,
      name: data.name.present ? data.name.value : this.name,
      unitId: data.unitId.present ? data.unitId.value : this.unitId,
      salePriceE4: data.salePriceE4.present
          ? data.salePriceE4.value
          : this.salePriceE4,
      costPriceE4: data.costPriceE4.present
          ? data.costPriceE4.value
          : this.costPriceE4,
      version: data.version.present ? data.version.value : this.version,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
      originDeviceId: data.originDeviceId.present
          ? data.originDeviceId.value
          : this.originDeviceId,
      deletedAt: data.deletedAt.present ? data.deletedAt.value : this.deletedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('ProductRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('sku: $sku, ')
          ..write('barcode: $barcode, ')
          ..write('name: $name, ')
          ..write('unitId: $unitId, ')
          ..write('salePriceE4: $salePriceE4, ')
          ..write('costPriceE4: $costPriceE4, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    sku,
    barcode,
    name,
    unitId,
    salePriceE4,
    costPriceE4,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is ProductRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.sku == this.sku &&
          other.barcode == this.barcode &&
          other.name == this.name &&
          other.unitId == this.unitId &&
          other.salePriceE4 == this.salePriceE4 &&
          other.costPriceE4 == this.costPriceE4 &&
          other.version == this.version &&
          other.updatedAt == this.updatedAt &&
          other.originDeviceId == this.originDeviceId &&
          other.deletedAt == this.deletedAt);
}

class ProductsCompanion extends UpdateCompanion<ProductRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> sku;
  final Value<String?> barcode;
  final Value<String> name;
  final Value<String?> unitId;
  final Value<int> salePriceE4;
  final Value<int> costPriceE4;
  final Value<int> version;
  final Value<DateTime> updatedAt;
  final Value<String?> originDeviceId;
  final Value<DateTime?> deletedAt;
  final Value<int> rowid;
  const ProductsCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.sku = const Value.absent(),
    this.barcode = const Value.absent(),
    this.name = const Value.absent(),
    this.unitId = const Value.absent(),
    this.salePriceE4 = const Value.absent(),
    this.costPriceE4 = const Value.absent(),
    this.version = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  ProductsCompanion.insert({
    required String id,
    required String companyId,
    required String sku,
    this.barcode = const Value.absent(),
    required String name,
    this.unitId = const Value.absent(),
    this.salePriceE4 = const Value.absent(),
    this.costPriceE4 = const Value.absent(),
    this.version = const Value.absent(),
    required DateTime updatedAt,
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       sku = Value(sku),
       name = Value(name),
       updatedAt = Value(updatedAt);
  static Insertable<ProductRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? sku,
    Expression<String>? barcode,
    Expression<String>? name,
    Expression<String>? unitId,
    Expression<int>? salePriceE4,
    Expression<int>? costPriceE4,
    Expression<int>? version,
    Expression<DateTime>? updatedAt,
    Expression<String>? originDeviceId,
    Expression<DateTime>? deletedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (sku != null) 'sku': sku,
      if (barcode != null) 'barcode': barcode,
      if (name != null) 'name': name,
      if (unitId != null) 'unit_id': unitId,
      if (salePriceE4 != null) 'sale_price_e4': salePriceE4,
      if (costPriceE4 != null) 'cost_price_e4': costPriceE4,
      if (version != null) 'version': version,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (originDeviceId != null) 'origin_device_id': originDeviceId,
      if (deletedAt != null) 'deleted_at': deletedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  ProductsCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? sku,
    Value<String?>? barcode,
    Value<String>? name,
    Value<String?>? unitId,
    Value<int>? salePriceE4,
    Value<int>? costPriceE4,
    Value<int>? version,
    Value<DateTime>? updatedAt,
    Value<String?>? originDeviceId,
    Value<DateTime?>? deletedAt,
    Value<int>? rowid,
  }) {
    return ProductsCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      sku: sku ?? this.sku,
      barcode: barcode ?? this.barcode,
      name: name ?? this.name,
      unitId: unitId ?? this.unitId,
      salePriceE4: salePriceE4 ?? this.salePriceE4,
      costPriceE4: costPriceE4 ?? this.costPriceE4,
      version: version ?? this.version,
      updatedAt: updatedAt ?? this.updatedAt,
      originDeviceId: originDeviceId ?? this.originDeviceId,
      deletedAt: deletedAt ?? this.deletedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (sku.present) {
      map['sku'] = Variable<String>(sku.value);
    }
    if (barcode.present) {
      map['barcode'] = Variable<String>(barcode.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (unitId.present) {
      map['unit_id'] = Variable<String>(unitId.value);
    }
    if (salePriceE4.present) {
      map['sale_price_e4'] = Variable<int>(salePriceE4.value);
    }
    if (costPriceE4.present) {
      map['cost_price_e4'] = Variable<int>(costPriceE4.value);
    }
    if (version.present) {
      map['version'] = Variable<int>(version.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (originDeviceId.present) {
      map['origin_device_id'] = Variable<String>(originDeviceId.value);
    }
    if (deletedAt.present) {
      map['deleted_at'] = Variable<DateTime>(deletedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('ProductsCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('sku: $sku, ')
          ..write('barcode: $barcode, ')
          ..write('name: $name, ')
          ..write('unitId: $unitId, ')
          ..write('salePriceE4: $salePriceE4, ')
          ..write('costPriceE4: $costPriceE4, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $BusinessProfilesTable extends BusinessProfiles
    with TableInfo<$BusinessProfilesTable, BusinessProfileRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $BusinessProfilesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    check: () => id.equals('business-profile'),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _businessNameMeta = const VerificationMeta(
    'businessName',
  );
  @override
  late final GeneratedColumn<String> businessName = GeneratedColumn<String>(
    'business_name',
    aliasedName,
    false,
    check: () => ComparableExpr(businessName.length).isBetweenValues(1, 160),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _industryMeta = const VerificationMeta(
    'industry',
  );
  @override
  late final GeneratedColumn<String> industry = GeneratedColumn<String>(
    'industry',
    aliasedName,
    false,
    check: () => ComparableExpr(industry.length).isBetweenValues(1, 80),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _currencyMeta = const VerificationMeta(
    'currency',
  );
  @override
  late final GeneratedColumn<String> currency = GeneratedColumn<String>(
    'currency',
    aliasedName,
    false,
    check: () => currency.length.equals(3),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _ownerNameMeta = const VerificationMeta(
    'ownerName',
  );
  @override
  late final GeneratedColumn<String> ownerName = GeneratedColumn<String>(
    'owner_name',
    aliasedName,
    false,
    check: () => ComparableExpr(ownerName.length).isBetweenValues(1, 120),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _createdAtMeta = const VerificationMeta(
    'createdAt',
  );
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
    'created_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    businessName,
    industry,
    currency,
    ownerName,
    createdAt,
    updatedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'business_profiles';
  @override
  VerificationContext validateIntegrity(
    Insertable<BusinessProfileRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('business_name')) {
      context.handle(
        _businessNameMeta,
        businessName.isAcceptableOrUnknown(
          data['business_name']!,
          _businessNameMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_businessNameMeta);
    }
    if (data.containsKey('industry')) {
      context.handle(
        _industryMeta,
        industry.isAcceptableOrUnknown(data['industry']!, _industryMeta),
      );
    } else if (isInserting) {
      context.missing(_industryMeta);
    }
    if (data.containsKey('currency')) {
      context.handle(
        _currencyMeta,
        currency.isAcceptableOrUnknown(data['currency']!, _currencyMeta),
      );
    } else if (isInserting) {
      context.missing(_currencyMeta);
    }
    if (data.containsKey('owner_name')) {
      context.handle(
        _ownerNameMeta,
        ownerName.isAcceptableOrUnknown(data['owner_name']!, _ownerNameMeta),
      );
    } else if (isInserting) {
      context.missing(_ownerNameMeta);
    }
    if (data.containsKey('created_at')) {
      context.handle(
        _createdAtMeta,
        createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta),
      );
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  BusinessProfileRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return BusinessProfileRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      businessName: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}business_name'],
      )!,
      industry: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}industry'],
      )!,
      currency: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}currency'],
      )!,
      ownerName: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}owner_name'],
      )!,
      createdAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}created_at'],
      )!,
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
    );
  }

  @override
  $BusinessProfilesTable createAlias(String alias) {
    return $BusinessProfilesTable(attachedDatabase, alias);
  }
}

class BusinessProfileRow extends DataClass
    implements Insertable<BusinessProfileRow> {
  final String id;
  final String companyId;
  final String businessName;
  final String industry;
  final String currency;
  final String ownerName;
  final DateTime createdAt;
  final DateTime updatedAt;
  const BusinessProfileRow({
    required this.id,
    required this.companyId,
    required this.businessName,
    required this.industry,
    required this.currency,
    required this.ownerName,
    required this.createdAt,
    required this.updatedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['business_name'] = Variable<String>(businessName);
    map['industry'] = Variable<String>(industry);
    map['currency'] = Variable<String>(currency);
    map['owner_name'] = Variable<String>(ownerName);
    map['created_at'] = Variable<DateTime>(createdAt);
    map['updated_at'] = Variable<DateTime>(updatedAt);
    return map;
  }

  BusinessProfilesCompanion toCompanion(bool nullToAbsent) {
    return BusinessProfilesCompanion(
      id: Value(id),
      companyId: Value(companyId),
      businessName: Value(businessName),
      industry: Value(industry),
      currency: Value(currency),
      ownerName: Value(ownerName),
      createdAt: Value(createdAt),
      updatedAt: Value(updatedAt),
    );
  }

  factory BusinessProfileRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return BusinessProfileRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      businessName: serializer.fromJson<String>(json['businessName']),
      industry: serializer.fromJson<String>(json['industry']),
      currency: serializer.fromJson<String>(json['currency']),
      ownerName: serializer.fromJson<String>(json['ownerName']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'businessName': serializer.toJson<String>(businessName),
      'industry': serializer.toJson<String>(industry),
      'currency': serializer.toJson<String>(currency),
      'ownerName': serializer.toJson<String>(ownerName),
      'createdAt': serializer.toJson<DateTime>(createdAt),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
    };
  }

  BusinessProfileRow copyWith({
    String? id,
    String? companyId,
    String? businessName,
    String? industry,
    String? currency,
    String? ownerName,
    DateTime? createdAt,
    DateTime? updatedAt,
  }) => BusinessProfileRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    businessName: businessName ?? this.businessName,
    industry: industry ?? this.industry,
    currency: currency ?? this.currency,
    ownerName: ownerName ?? this.ownerName,
    createdAt: createdAt ?? this.createdAt,
    updatedAt: updatedAt ?? this.updatedAt,
  );
  BusinessProfileRow copyWithCompanion(BusinessProfilesCompanion data) {
    return BusinessProfileRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      businessName: data.businessName.present
          ? data.businessName.value
          : this.businessName,
      industry: data.industry.present ? data.industry.value : this.industry,
      currency: data.currency.present ? data.currency.value : this.currency,
      ownerName: data.ownerName.present ? data.ownerName.value : this.ownerName,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('BusinessProfileRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('businessName: $businessName, ')
          ..write('industry: $industry, ')
          ..write('currency: $currency, ')
          ..write('ownerName: $ownerName, ')
          ..write('createdAt: $createdAt, ')
          ..write('updatedAt: $updatedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    businessName,
    industry,
    currency,
    ownerName,
    createdAt,
    updatedAt,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is BusinessProfileRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.businessName == this.businessName &&
          other.industry == this.industry &&
          other.currency == this.currency &&
          other.ownerName == this.ownerName &&
          other.createdAt == this.createdAt &&
          other.updatedAt == this.updatedAt);
}

class BusinessProfilesCompanion extends UpdateCompanion<BusinessProfileRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> businessName;
  final Value<String> industry;
  final Value<String> currency;
  final Value<String> ownerName;
  final Value<DateTime> createdAt;
  final Value<DateTime> updatedAt;
  final Value<int> rowid;
  const BusinessProfilesCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.businessName = const Value.absent(),
    this.industry = const Value.absent(),
    this.currency = const Value.absent(),
    this.ownerName = const Value.absent(),
    this.createdAt = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  BusinessProfilesCompanion.insert({
    required String id,
    required String companyId,
    required String businessName,
    required String industry,
    required String currency,
    required String ownerName,
    required DateTime createdAt,
    required DateTime updatedAt,
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       businessName = Value(businessName),
       industry = Value(industry),
       currency = Value(currency),
       ownerName = Value(ownerName),
       createdAt = Value(createdAt),
       updatedAt = Value(updatedAt);
  static Insertable<BusinessProfileRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? businessName,
    Expression<String>? industry,
    Expression<String>? currency,
    Expression<String>? ownerName,
    Expression<DateTime>? createdAt,
    Expression<DateTime>? updatedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (businessName != null) 'business_name': businessName,
      if (industry != null) 'industry': industry,
      if (currency != null) 'currency': currency,
      if (ownerName != null) 'owner_name': ownerName,
      if (createdAt != null) 'created_at': createdAt,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  BusinessProfilesCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? businessName,
    Value<String>? industry,
    Value<String>? currency,
    Value<String>? ownerName,
    Value<DateTime>? createdAt,
    Value<DateTime>? updatedAt,
    Value<int>? rowid,
  }) {
    return BusinessProfilesCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      businessName: businessName ?? this.businessName,
      industry: industry ?? this.industry,
      currency: currency ?? this.currency,
      ownerName: ownerName ?? this.ownerName,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (businessName.present) {
      map['business_name'] = Variable<String>(businessName.value);
    }
    if (industry.present) {
      map['industry'] = Variable<String>(industry.value);
    }
    if (currency.present) {
      map['currency'] = Variable<String>(currency.value);
    }
    if (ownerName.present) {
      map['owner_name'] = Variable<String>(ownerName.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('BusinessProfilesCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('businessName: $businessName, ')
          ..write('industry: $industry, ')
          ..write('currency: $currency, ')
          ..write('ownerName: $ownerName, ')
          ..write('createdAt: $createdAt, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $WarehousesTable extends Warehouses
    with TableInfo<$WarehousesTable, WarehouseRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $WarehousesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _codeMeta = const VerificationMeta('code');
  @override
  late final GeneratedColumn<String> code = GeneratedColumn<String>(
    'code',
    aliasedName,
    false,
    check: () => ComparableExpr(code.length).isBetweenValues(1, 20),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _nameMeta = const VerificationMeta('name');
  @override
  late final GeneratedColumn<String> name = GeneratedColumn<String>(
    'name',
    aliasedName,
    false,
    check: () => ComparableExpr(name.length).isBetweenValues(1, 160),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _isDefaultMeta = const VerificationMeta(
    'isDefault',
  );
  @override
  late final GeneratedColumn<bool> isDefault = GeneratedColumn<bool>(
    'is_default',
    aliasedName,
    false,
    type: DriftSqlType.bool,
    requiredDuringInsert: false,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'CHECK ("is_default" IN (0, 1))',
    ),
    defaultValue: const Constant(false),
  );
  static const VerificationMeta _versionMeta = const VerificationMeta(
    'version',
  );
  @override
  late final GeneratedColumn<int> version = GeneratedColumn<int>(
    'version',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(1),
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _originDeviceIdMeta = const VerificationMeta(
    'originDeviceId',
  );
  @override
  late final GeneratedColumn<String> originDeviceId = GeneratedColumn<String>(
    'origin_device_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _deletedAtMeta = const VerificationMeta(
    'deletedAt',
  );
  @override
  late final GeneratedColumn<DateTime> deletedAt = GeneratedColumn<DateTime>(
    'deleted_at',
    aliasedName,
    true,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    code,
    name,
    isDefault,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'warehouses';
  @override
  VerificationContext validateIntegrity(
    Insertable<WarehouseRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('code')) {
      context.handle(
        _codeMeta,
        code.isAcceptableOrUnknown(data['code']!, _codeMeta),
      );
    } else if (isInserting) {
      context.missing(_codeMeta);
    }
    if (data.containsKey('name')) {
      context.handle(
        _nameMeta,
        name.isAcceptableOrUnknown(data['name']!, _nameMeta),
      );
    } else if (isInserting) {
      context.missing(_nameMeta);
    }
    if (data.containsKey('is_default')) {
      context.handle(
        _isDefaultMeta,
        isDefault.isAcceptableOrUnknown(data['is_default']!, _isDefaultMeta),
      );
    }
    if (data.containsKey('version')) {
      context.handle(
        _versionMeta,
        version.isAcceptableOrUnknown(data['version']!, _versionMeta),
      );
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    if (data.containsKey('origin_device_id')) {
      context.handle(
        _originDeviceIdMeta,
        originDeviceId.isAcceptableOrUnknown(
          data['origin_device_id']!,
          _originDeviceIdMeta,
        ),
      );
    }
    if (data.containsKey('deleted_at')) {
      context.handle(
        _deletedAtMeta,
        deletedAt.isAcceptableOrUnknown(data['deleted_at']!, _deletedAtMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  WarehouseRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return WarehouseRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      code: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}code'],
      )!,
      name: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}name'],
      )!,
      isDefault: attachedDatabase.typeMapping.read(
        DriftSqlType.bool,
        data['${effectivePrefix}is_default'],
      )!,
      version: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}version'],
      )!,
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
      originDeviceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}origin_device_id'],
      ),
      deletedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}deleted_at'],
      ),
    );
  }

  @override
  $WarehousesTable createAlias(String alias) {
    return $WarehousesTable(attachedDatabase, alias);
  }
}

class WarehouseRow extends DataClass implements Insertable<WarehouseRow> {
  final String id;
  final String companyId;
  final String code;
  final String name;
  final bool isDefault;
  final int version;
  final DateTime updatedAt;
  final String? originDeviceId;
  final DateTime? deletedAt;
  const WarehouseRow({
    required this.id,
    required this.companyId,
    required this.code,
    required this.name,
    required this.isDefault,
    required this.version,
    required this.updatedAt,
    this.originDeviceId,
    this.deletedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['code'] = Variable<String>(code);
    map['name'] = Variable<String>(name);
    map['is_default'] = Variable<bool>(isDefault);
    map['version'] = Variable<int>(version);
    map['updated_at'] = Variable<DateTime>(updatedAt);
    if (!nullToAbsent || originDeviceId != null) {
      map['origin_device_id'] = Variable<String>(originDeviceId);
    }
    if (!nullToAbsent || deletedAt != null) {
      map['deleted_at'] = Variable<DateTime>(deletedAt);
    }
    return map;
  }

  WarehousesCompanion toCompanion(bool nullToAbsent) {
    return WarehousesCompanion(
      id: Value(id),
      companyId: Value(companyId),
      code: Value(code),
      name: Value(name),
      isDefault: Value(isDefault),
      version: Value(version),
      updatedAt: Value(updatedAt),
      originDeviceId: originDeviceId == null && nullToAbsent
          ? const Value.absent()
          : Value(originDeviceId),
      deletedAt: deletedAt == null && nullToAbsent
          ? const Value.absent()
          : Value(deletedAt),
    );
  }

  factory WarehouseRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return WarehouseRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      code: serializer.fromJson<String>(json['code']),
      name: serializer.fromJson<String>(json['name']),
      isDefault: serializer.fromJson<bool>(json['isDefault']),
      version: serializer.fromJson<int>(json['version']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
      originDeviceId: serializer.fromJson<String?>(json['originDeviceId']),
      deletedAt: serializer.fromJson<DateTime?>(json['deletedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'code': serializer.toJson<String>(code),
      'name': serializer.toJson<String>(name),
      'isDefault': serializer.toJson<bool>(isDefault),
      'version': serializer.toJson<int>(version),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
      'originDeviceId': serializer.toJson<String?>(originDeviceId),
      'deletedAt': serializer.toJson<DateTime?>(deletedAt),
    };
  }

  WarehouseRow copyWith({
    String? id,
    String? companyId,
    String? code,
    String? name,
    bool? isDefault,
    int? version,
    DateTime? updatedAt,
    Value<String?> originDeviceId = const Value.absent(),
    Value<DateTime?> deletedAt = const Value.absent(),
  }) => WarehouseRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    code: code ?? this.code,
    name: name ?? this.name,
    isDefault: isDefault ?? this.isDefault,
    version: version ?? this.version,
    updatedAt: updatedAt ?? this.updatedAt,
    originDeviceId: originDeviceId.present
        ? originDeviceId.value
        : this.originDeviceId,
    deletedAt: deletedAt.present ? deletedAt.value : this.deletedAt,
  );
  WarehouseRow copyWithCompanion(WarehousesCompanion data) {
    return WarehouseRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      code: data.code.present ? data.code.value : this.code,
      name: data.name.present ? data.name.value : this.name,
      isDefault: data.isDefault.present ? data.isDefault.value : this.isDefault,
      version: data.version.present ? data.version.value : this.version,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
      originDeviceId: data.originDeviceId.present
          ? data.originDeviceId.value
          : this.originDeviceId,
      deletedAt: data.deletedAt.present ? data.deletedAt.value : this.deletedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('WarehouseRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('code: $code, ')
          ..write('name: $name, ')
          ..write('isDefault: $isDefault, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    code,
    name,
    isDefault,
    version,
    updatedAt,
    originDeviceId,
    deletedAt,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is WarehouseRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.code == this.code &&
          other.name == this.name &&
          other.isDefault == this.isDefault &&
          other.version == this.version &&
          other.updatedAt == this.updatedAt &&
          other.originDeviceId == this.originDeviceId &&
          other.deletedAt == this.deletedAt);
}

class WarehousesCompanion extends UpdateCompanion<WarehouseRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> code;
  final Value<String> name;
  final Value<bool> isDefault;
  final Value<int> version;
  final Value<DateTime> updatedAt;
  final Value<String?> originDeviceId;
  final Value<DateTime?> deletedAt;
  final Value<int> rowid;
  const WarehousesCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.code = const Value.absent(),
    this.name = const Value.absent(),
    this.isDefault = const Value.absent(),
    this.version = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  WarehousesCompanion.insert({
    required String id,
    required String companyId,
    required String code,
    required String name,
    this.isDefault = const Value.absent(),
    this.version = const Value.absent(),
    required DateTime updatedAt,
    this.originDeviceId = const Value.absent(),
    this.deletedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       code = Value(code),
       name = Value(name),
       updatedAt = Value(updatedAt);
  static Insertable<WarehouseRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? code,
    Expression<String>? name,
    Expression<bool>? isDefault,
    Expression<int>? version,
    Expression<DateTime>? updatedAt,
    Expression<String>? originDeviceId,
    Expression<DateTime>? deletedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (code != null) 'code': code,
      if (name != null) 'name': name,
      if (isDefault != null) 'is_default': isDefault,
      if (version != null) 'version': version,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (originDeviceId != null) 'origin_device_id': originDeviceId,
      if (deletedAt != null) 'deleted_at': deletedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  WarehousesCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? code,
    Value<String>? name,
    Value<bool>? isDefault,
    Value<int>? version,
    Value<DateTime>? updatedAt,
    Value<String?>? originDeviceId,
    Value<DateTime?>? deletedAt,
    Value<int>? rowid,
  }) {
    return WarehousesCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      code: code ?? this.code,
      name: name ?? this.name,
      isDefault: isDefault ?? this.isDefault,
      version: version ?? this.version,
      updatedAt: updatedAt ?? this.updatedAt,
      originDeviceId: originDeviceId ?? this.originDeviceId,
      deletedAt: deletedAt ?? this.deletedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (code.present) {
      map['code'] = Variable<String>(code.value);
    }
    if (name.present) {
      map['name'] = Variable<String>(name.value);
    }
    if (isDefault.present) {
      map['is_default'] = Variable<bool>(isDefault.value);
    }
    if (version.present) {
      map['version'] = Variable<int>(version.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (originDeviceId.present) {
      map['origin_device_id'] = Variable<String>(originDeviceId.value);
    }
    if (deletedAt.present) {
      map['deleted_at'] = Variable<DateTime>(deletedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('WarehousesCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('code: $code, ')
          ..write('name: $name, ')
          ..write('isDefault: $isDefault, ')
          ..write('version: $version, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('deletedAt: $deletedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $StockMovementsTable extends StockMovements
    with TableInfo<$StockMovementsTable, StockMovementRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $StockMovementsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _productIdMeta = const VerificationMeta(
    'productId',
  );
  @override
  late final GeneratedColumn<String> productId = GeneratedColumn<String>(
    'product_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES products (id)',
    ),
  );
  static const VerificationMeta _warehouseIdMeta = const VerificationMeta(
    'warehouseId',
  );
  @override
  late final GeneratedColumn<String> warehouseId = GeneratedColumn<String>(
    'warehouse_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES warehouses (id)',
    ),
  );
  static const VerificationMeta _movementTypeMeta = const VerificationMeta(
    'movementType',
  );
  @override
  late final GeneratedColumn<String> movementType = GeneratedColumn<String>(
    'movement_type',
    aliasedName,
    false,
    check: () => movementType.isIn(const [
      'opening',
      'receipt',
      'issue',
      'adjustment',
      'reversal',
    ]),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _qtyE4Meta = const VerificationMeta('qtyE4');
  @override
  late final GeneratedColumn<int> qtyE4 = GeneratedColumn<int>(
    'qty_e4',
    aliasedName,
    false,
    check: () => BooleanExpressionOperators(qtyE4.equals(0)).not(),
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _unitCostE4Meta = const VerificationMeta(
    'unitCostE4',
  );
  @override
  late final GeneratedColumn<int> unitCostE4 = GeneratedColumn<int>(
    'unit_cost_e4',
    aliasedName,
    true,
    check: () => ComparableExpr(unitCostE4).isBiggerOrEqualValue(0),
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _reasonMeta = const VerificationMeta('reason');
  @override
  late final GeneratedColumn<String> reason = GeneratedColumn<String>(
    'reason',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _sourceTypeMeta = const VerificationMeta(
    'sourceType',
  );
  @override
  late final GeneratedColumn<String> sourceType = GeneratedColumn<String>(
    'source_type',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _sourceIdMeta = const VerificationMeta(
    'sourceId',
  );
  @override
  late final GeneratedColumn<String> sourceId = GeneratedColumn<String>(
    'source_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _reversesMovementIdMeta =
      const VerificationMeta('reversesMovementId');
  @override
  late final GeneratedColumn<String> reversesMovementId =
      GeneratedColumn<String>(
        'reverses_movement_id',
        aliasedName,
        true,
        type: DriftSqlType.string,
        requiredDuringInsert: false,
      );
  static const VerificationMeta _clientRequestIdMeta = const VerificationMeta(
    'clientRequestId',
  );
  @override
  late final GeneratedColumn<String> clientRequestId = GeneratedColumn<String>(
    'client_request_id',
    aliasedName,
    false,
    check: () => ComparableExpr(clientRequestId.length).isBetweenValues(1, 80),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _occurredAtMeta = const VerificationMeta(
    'occurredAt',
  );
  @override
  late final GeneratedColumn<DateTime> occurredAt = GeneratedColumn<DateTime>(
    'occurred_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _createdAtMeta = const VerificationMeta(
    'createdAt',
  );
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
    'created_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _createdByMeta = const VerificationMeta(
    'createdBy',
  );
  @override
  late final GeneratedColumn<String> createdBy = GeneratedColumn<String>(
    'created_by',
    aliasedName,
    false,
    check: () => ComparableExpr(createdBy.length).isBetweenValues(1, 120),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _originDeviceIdMeta = const VerificationMeta(
    'originDeviceId',
  );
  @override
  late final GeneratedColumn<String> originDeviceId = GeneratedColumn<String>(
    'origin_device_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    productId,
    warehouseId,
    movementType,
    qtyE4,
    unitCostE4,
    reason,
    sourceType,
    sourceId,
    reversesMovementId,
    clientRequestId,
    occurredAt,
    createdAt,
    createdBy,
    originDeviceId,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'stock_movements';
  @override
  VerificationContext validateIntegrity(
    Insertable<StockMovementRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('product_id')) {
      context.handle(
        _productIdMeta,
        productId.isAcceptableOrUnknown(data['product_id']!, _productIdMeta),
      );
    } else if (isInserting) {
      context.missing(_productIdMeta);
    }
    if (data.containsKey('warehouse_id')) {
      context.handle(
        _warehouseIdMeta,
        warehouseId.isAcceptableOrUnknown(
          data['warehouse_id']!,
          _warehouseIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_warehouseIdMeta);
    }
    if (data.containsKey('movement_type')) {
      context.handle(
        _movementTypeMeta,
        movementType.isAcceptableOrUnknown(
          data['movement_type']!,
          _movementTypeMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_movementTypeMeta);
    }
    if (data.containsKey('qty_e4')) {
      context.handle(
        _qtyE4Meta,
        qtyE4.isAcceptableOrUnknown(data['qty_e4']!, _qtyE4Meta),
      );
    } else if (isInserting) {
      context.missing(_qtyE4Meta);
    }
    if (data.containsKey('unit_cost_e4')) {
      context.handle(
        _unitCostE4Meta,
        unitCostE4.isAcceptableOrUnknown(
          data['unit_cost_e4']!,
          _unitCostE4Meta,
        ),
      );
    }
    if (data.containsKey('reason')) {
      context.handle(
        _reasonMeta,
        reason.isAcceptableOrUnknown(data['reason']!, _reasonMeta),
      );
    }
    if (data.containsKey('source_type')) {
      context.handle(
        _sourceTypeMeta,
        sourceType.isAcceptableOrUnknown(data['source_type']!, _sourceTypeMeta),
      );
    }
    if (data.containsKey('source_id')) {
      context.handle(
        _sourceIdMeta,
        sourceId.isAcceptableOrUnknown(data['source_id']!, _sourceIdMeta),
      );
    }
    if (data.containsKey('reverses_movement_id')) {
      context.handle(
        _reversesMovementIdMeta,
        reversesMovementId.isAcceptableOrUnknown(
          data['reverses_movement_id']!,
          _reversesMovementIdMeta,
        ),
      );
    }
    if (data.containsKey('client_request_id')) {
      context.handle(
        _clientRequestIdMeta,
        clientRequestId.isAcceptableOrUnknown(
          data['client_request_id']!,
          _clientRequestIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_clientRequestIdMeta);
    }
    if (data.containsKey('occurred_at')) {
      context.handle(
        _occurredAtMeta,
        occurredAt.isAcceptableOrUnknown(data['occurred_at']!, _occurredAtMeta),
      );
    } else if (isInserting) {
      context.missing(_occurredAtMeta);
    }
    if (data.containsKey('created_at')) {
      context.handle(
        _createdAtMeta,
        createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta),
      );
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    if (data.containsKey('created_by')) {
      context.handle(
        _createdByMeta,
        createdBy.isAcceptableOrUnknown(data['created_by']!, _createdByMeta),
      );
    } else if (isInserting) {
      context.missing(_createdByMeta);
    }
    if (data.containsKey('origin_device_id')) {
      context.handle(
        _originDeviceIdMeta,
        originDeviceId.isAcceptableOrUnknown(
          data['origin_device_id']!,
          _originDeviceIdMeta,
        ),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  StockMovementRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return StockMovementRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      productId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}product_id'],
      )!,
      warehouseId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}warehouse_id'],
      )!,
      movementType: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}movement_type'],
      )!,
      qtyE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}qty_e4'],
      )!,
      unitCostE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}unit_cost_e4'],
      ),
      reason: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}reason'],
      ),
      sourceType: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}source_type'],
      ),
      sourceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}source_id'],
      ),
      reversesMovementId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}reverses_movement_id'],
      ),
      clientRequestId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}client_request_id'],
      )!,
      occurredAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}occurred_at'],
      )!,
      createdAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}created_at'],
      )!,
      createdBy: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}created_by'],
      )!,
      originDeviceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}origin_device_id'],
      ),
    );
  }

  @override
  $StockMovementsTable createAlias(String alias) {
    return $StockMovementsTable(attachedDatabase, alias);
  }
}

class StockMovementRow extends DataClass
    implements Insertable<StockMovementRow> {
  final String id;
  final String companyId;
  final String productId;
  final String warehouseId;
  final String movementType;
  final int qtyE4;
  final int? unitCostE4;
  final String? reason;
  final String? sourceType;
  final String? sourceId;
  final String? reversesMovementId;
  final String clientRequestId;
  final DateTime occurredAt;
  final DateTime createdAt;
  final String createdBy;
  final String? originDeviceId;
  const StockMovementRow({
    required this.id,
    required this.companyId,
    required this.productId,
    required this.warehouseId,
    required this.movementType,
    required this.qtyE4,
    this.unitCostE4,
    this.reason,
    this.sourceType,
    this.sourceId,
    this.reversesMovementId,
    required this.clientRequestId,
    required this.occurredAt,
    required this.createdAt,
    required this.createdBy,
    this.originDeviceId,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['product_id'] = Variable<String>(productId);
    map['warehouse_id'] = Variable<String>(warehouseId);
    map['movement_type'] = Variable<String>(movementType);
    map['qty_e4'] = Variable<int>(qtyE4);
    if (!nullToAbsent || unitCostE4 != null) {
      map['unit_cost_e4'] = Variable<int>(unitCostE4);
    }
    if (!nullToAbsent || reason != null) {
      map['reason'] = Variable<String>(reason);
    }
    if (!nullToAbsent || sourceType != null) {
      map['source_type'] = Variable<String>(sourceType);
    }
    if (!nullToAbsent || sourceId != null) {
      map['source_id'] = Variable<String>(sourceId);
    }
    if (!nullToAbsent || reversesMovementId != null) {
      map['reverses_movement_id'] = Variable<String>(reversesMovementId);
    }
    map['client_request_id'] = Variable<String>(clientRequestId);
    map['occurred_at'] = Variable<DateTime>(occurredAt);
    map['created_at'] = Variable<DateTime>(createdAt);
    map['created_by'] = Variable<String>(createdBy);
    if (!nullToAbsent || originDeviceId != null) {
      map['origin_device_id'] = Variable<String>(originDeviceId);
    }
    return map;
  }

  StockMovementsCompanion toCompanion(bool nullToAbsent) {
    return StockMovementsCompanion(
      id: Value(id),
      companyId: Value(companyId),
      productId: Value(productId),
      warehouseId: Value(warehouseId),
      movementType: Value(movementType),
      qtyE4: Value(qtyE4),
      unitCostE4: unitCostE4 == null && nullToAbsent
          ? const Value.absent()
          : Value(unitCostE4),
      reason: reason == null && nullToAbsent
          ? const Value.absent()
          : Value(reason),
      sourceType: sourceType == null && nullToAbsent
          ? const Value.absent()
          : Value(sourceType),
      sourceId: sourceId == null && nullToAbsent
          ? const Value.absent()
          : Value(sourceId),
      reversesMovementId: reversesMovementId == null && nullToAbsent
          ? const Value.absent()
          : Value(reversesMovementId),
      clientRequestId: Value(clientRequestId),
      occurredAt: Value(occurredAt),
      createdAt: Value(createdAt),
      createdBy: Value(createdBy),
      originDeviceId: originDeviceId == null && nullToAbsent
          ? const Value.absent()
          : Value(originDeviceId),
    );
  }

  factory StockMovementRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return StockMovementRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      productId: serializer.fromJson<String>(json['productId']),
      warehouseId: serializer.fromJson<String>(json['warehouseId']),
      movementType: serializer.fromJson<String>(json['movementType']),
      qtyE4: serializer.fromJson<int>(json['qtyE4']),
      unitCostE4: serializer.fromJson<int?>(json['unitCostE4']),
      reason: serializer.fromJson<String?>(json['reason']),
      sourceType: serializer.fromJson<String?>(json['sourceType']),
      sourceId: serializer.fromJson<String?>(json['sourceId']),
      reversesMovementId: serializer.fromJson<String?>(
        json['reversesMovementId'],
      ),
      clientRequestId: serializer.fromJson<String>(json['clientRequestId']),
      occurredAt: serializer.fromJson<DateTime>(json['occurredAt']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
      createdBy: serializer.fromJson<String>(json['createdBy']),
      originDeviceId: serializer.fromJson<String?>(json['originDeviceId']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'productId': serializer.toJson<String>(productId),
      'warehouseId': serializer.toJson<String>(warehouseId),
      'movementType': serializer.toJson<String>(movementType),
      'qtyE4': serializer.toJson<int>(qtyE4),
      'unitCostE4': serializer.toJson<int?>(unitCostE4),
      'reason': serializer.toJson<String?>(reason),
      'sourceType': serializer.toJson<String?>(sourceType),
      'sourceId': serializer.toJson<String?>(sourceId),
      'reversesMovementId': serializer.toJson<String?>(reversesMovementId),
      'clientRequestId': serializer.toJson<String>(clientRequestId),
      'occurredAt': serializer.toJson<DateTime>(occurredAt),
      'createdAt': serializer.toJson<DateTime>(createdAt),
      'createdBy': serializer.toJson<String>(createdBy),
      'originDeviceId': serializer.toJson<String?>(originDeviceId),
    };
  }

  StockMovementRow copyWith({
    String? id,
    String? companyId,
    String? productId,
    String? warehouseId,
    String? movementType,
    int? qtyE4,
    Value<int?> unitCostE4 = const Value.absent(),
    Value<String?> reason = const Value.absent(),
    Value<String?> sourceType = const Value.absent(),
    Value<String?> sourceId = const Value.absent(),
    Value<String?> reversesMovementId = const Value.absent(),
    String? clientRequestId,
    DateTime? occurredAt,
    DateTime? createdAt,
    String? createdBy,
    Value<String?> originDeviceId = const Value.absent(),
  }) => StockMovementRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    productId: productId ?? this.productId,
    warehouseId: warehouseId ?? this.warehouseId,
    movementType: movementType ?? this.movementType,
    qtyE4: qtyE4 ?? this.qtyE4,
    unitCostE4: unitCostE4.present ? unitCostE4.value : this.unitCostE4,
    reason: reason.present ? reason.value : this.reason,
    sourceType: sourceType.present ? sourceType.value : this.sourceType,
    sourceId: sourceId.present ? sourceId.value : this.sourceId,
    reversesMovementId: reversesMovementId.present
        ? reversesMovementId.value
        : this.reversesMovementId,
    clientRequestId: clientRequestId ?? this.clientRequestId,
    occurredAt: occurredAt ?? this.occurredAt,
    createdAt: createdAt ?? this.createdAt,
    createdBy: createdBy ?? this.createdBy,
    originDeviceId: originDeviceId.present
        ? originDeviceId.value
        : this.originDeviceId,
  );
  StockMovementRow copyWithCompanion(StockMovementsCompanion data) {
    return StockMovementRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      productId: data.productId.present ? data.productId.value : this.productId,
      warehouseId: data.warehouseId.present
          ? data.warehouseId.value
          : this.warehouseId,
      movementType: data.movementType.present
          ? data.movementType.value
          : this.movementType,
      qtyE4: data.qtyE4.present ? data.qtyE4.value : this.qtyE4,
      unitCostE4: data.unitCostE4.present
          ? data.unitCostE4.value
          : this.unitCostE4,
      reason: data.reason.present ? data.reason.value : this.reason,
      sourceType: data.sourceType.present
          ? data.sourceType.value
          : this.sourceType,
      sourceId: data.sourceId.present ? data.sourceId.value : this.sourceId,
      reversesMovementId: data.reversesMovementId.present
          ? data.reversesMovementId.value
          : this.reversesMovementId,
      clientRequestId: data.clientRequestId.present
          ? data.clientRequestId.value
          : this.clientRequestId,
      occurredAt: data.occurredAt.present
          ? data.occurredAt.value
          : this.occurredAt,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
      createdBy: data.createdBy.present ? data.createdBy.value : this.createdBy,
      originDeviceId: data.originDeviceId.present
          ? data.originDeviceId.value
          : this.originDeviceId,
    );
  }

  @override
  String toString() {
    return (StringBuffer('StockMovementRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('productId: $productId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('movementType: $movementType, ')
          ..write('qtyE4: $qtyE4, ')
          ..write('unitCostE4: $unitCostE4, ')
          ..write('reason: $reason, ')
          ..write('sourceType: $sourceType, ')
          ..write('sourceId: $sourceId, ')
          ..write('reversesMovementId: $reversesMovementId, ')
          ..write('clientRequestId: $clientRequestId, ')
          ..write('occurredAt: $occurredAt, ')
          ..write('createdAt: $createdAt, ')
          ..write('createdBy: $createdBy, ')
          ..write('originDeviceId: $originDeviceId')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    productId,
    warehouseId,
    movementType,
    qtyE4,
    unitCostE4,
    reason,
    sourceType,
    sourceId,
    reversesMovementId,
    clientRequestId,
    occurredAt,
    createdAt,
    createdBy,
    originDeviceId,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is StockMovementRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.productId == this.productId &&
          other.warehouseId == this.warehouseId &&
          other.movementType == this.movementType &&
          other.qtyE4 == this.qtyE4 &&
          other.unitCostE4 == this.unitCostE4 &&
          other.reason == this.reason &&
          other.sourceType == this.sourceType &&
          other.sourceId == this.sourceId &&
          other.reversesMovementId == this.reversesMovementId &&
          other.clientRequestId == this.clientRequestId &&
          other.occurredAt == this.occurredAt &&
          other.createdAt == this.createdAt &&
          other.createdBy == this.createdBy &&
          other.originDeviceId == this.originDeviceId);
}

class StockMovementsCompanion extends UpdateCompanion<StockMovementRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> productId;
  final Value<String> warehouseId;
  final Value<String> movementType;
  final Value<int> qtyE4;
  final Value<int?> unitCostE4;
  final Value<String?> reason;
  final Value<String?> sourceType;
  final Value<String?> sourceId;
  final Value<String?> reversesMovementId;
  final Value<String> clientRequestId;
  final Value<DateTime> occurredAt;
  final Value<DateTime> createdAt;
  final Value<String> createdBy;
  final Value<String?> originDeviceId;
  final Value<int> rowid;
  const StockMovementsCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.productId = const Value.absent(),
    this.warehouseId = const Value.absent(),
    this.movementType = const Value.absent(),
    this.qtyE4 = const Value.absent(),
    this.unitCostE4 = const Value.absent(),
    this.reason = const Value.absent(),
    this.sourceType = const Value.absent(),
    this.sourceId = const Value.absent(),
    this.reversesMovementId = const Value.absent(),
    this.clientRequestId = const Value.absent(),
    this.occurredAt = const Value.absent(),
    this.createdAt = const Value.absent(),
    this.createdBy = const Value.absent(),
    this.originDeviceId = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  StockMovementsCompanion.insert({
    required String id,
    required String companyId,
    required String productId,
    required String warehouseId,
    required String movementType,
    required int qtyE4,
    this.unitCostE4 = const Value.absent(),
    this.reason = const Value.absent(),
    this.sourceType = const Value.absent(),
    this.sourceId = const Value.absent(),
    this.reversesMovementId = const Value.absent(),
    required String clientRequestId,
    required DateTime occurredAt,
    required DateTime createdAt,
    required String createdBy,
    this.originDeviceId = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       productId = Value(productId),
       warehouseId = Value(warehouseId),
       movementType = Value(movementType),
       qtyE4 = Value(qtyE4),
       clientRequestId = Value(clientRequestId),
       occurredAt = Value(occurredAt),
       createdAt = Value(createdAt),
       createdBy = Value(createdBy);
  static Insertable<StockMovementRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? productId,
    Expression<String>? warehouseId,
    Expression<String>? movementType,
    Expression<int>? qtyE4,
    Expression<int>? unitCostE4,
    Expression<String>? reason,
    Expression<String>? sourceType,
    Expression<String>? sourceId,
    Expression<String>? reversesMovementId,
    Expression<String>? clientRequestId,
    Expression<DateTime>? occurredAt,
    Expression<DateTime>? createdAt,
    Expression<String>? createdBy,
    Expression<String>? originDeviceId,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (productId != null) 'product_id': productId,
      if (warehouseId != null) 'warehouse_id': warehouseId,
      if (movementType != null) 'movement_type': movementType,
      if (qtyE4 != null) 'qty_e4': qtyE4,
      if (unitCostE4 != null) 'unit_cost_e4': unitCostE4,
      if (reason != null) 'reason': reason,
      if (sourceType != null) 'source_type': sourceType,
      if (sourceId != null) 'source_id': sourceId,
      if (reversesMovementId != null)
        'reverses_movement_id': reversesMovementId,
      if (clientRequestId != null) 'client_request_id': clientRequestId,
      if (occurredAt != null) 'occurred_at': occurredAt,
      if (createdAt != null) 'created_at': createdAt,
      if (createdBy != null) 'created_by': createdBy,
      if (originDeviceId != null) 'origin_device_id': originDeviceId,
      if (rowid != null) 'rowid': rowid,
    });
  }

  StockMovementsCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? productId,
    Value<String>? warehouseId,
    Value<String>? movementType,
    Value<int>? qtyE4,
    Value<int?>? unitCostE4,
    Value<String?>? reason,
    Value<String?>? sourceType,
    Value<String?>? sourceId,
    Value<String?>? reversesMovementId,
    Value<String>? clientRequestId,
    Value<DateTime>? occurredAt,
    Value<DateTime>? createdAt,
    Value<String>? createdBy,
    Value<String?>? originDeviceId,
    Value<int>? rowid,
  }) {
    return StockMovementsCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      productId: productId ?? this.productId,
      warehouseId: warehouseId ?? this.warehouseId,
      movementType: movementType ?? this.movementType,
      qtyE4: qtyE4 ?? this.qtyE4,
      unitCostE4: unitCostE4 ?? this.unitCostE4,
      reason: reason ?? this.reason,
      sourceType: sourceType ?? this.sourceType,
      sourceId: sourceId ?? this.sourceId,
      reversesMovementId: reversesMovementId ?? this.reversesMovementId,
      clientRequestId: clientRequestId ?? this.clientRequestId,
      occurredAt: occurredAt ?? this.occurredAt,
      createdAt: createdAt ?? this.createdAt,
      createdBy: createdBy ?? this.createdBy,
      originDeviceId: originDeviceId ?? this.originDeviceId,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (productId.present) {
      map['product_id'] = Variable<String>(productId.value);
    }
    if (warehouseId.present) {
      map['warehouse_id'] = Variable<String>(warehouseId.value);
    }
    if (movementType.present) {
      map['movement_type'] = Variable<String>(movementType.value);
    }
    if (qtyE4.present) {
      map['qty_e4'] = Variable<int>(qtyE4.value);
    }
    if (unitCostE4.present) {
      map['unit_cost_e4'] = Variable<int>(unitCostE4.value);
    }
    if (reason.present) {
      map['reason'] = Variable<String>(reason.value);
    }
    if (sourceType.present) {
      map['source_type'] = Variable<String>(sourceType.value);
    }
    if (sourceId.present) {
      map['source_id'] = Variable<String>(sourceId.value);
    }
    if (reversesMovementId.present) {
      map['reverses_movement_id'] = Variable<String>(reversesMovementId.value);
    }
    if (clientRequestId.present) {
      map['client_request_id'] = Variable<String>(clientRequestId.value);
    }
    if (occurredAt.present) {
      map['occurred_at'] = Variable<DateTime>(occurredAt.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    if (createdBy.present) {
      map['created_by'] = Variable<String>(createdBy.value);
    }
    if (originDeviceId.present) {
      map['origin_device_id'] = Variable<String>(originDeviceId.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('StockMovementsCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('productId: $productId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('movementType: $movementType, ')
          ..write('qtyE4: $qtyE4, ')
          ..write('unitCostE4: $unitCostE4, ')
          ..write('reason: $reason, ')
          ..write('sourceType: $sourceType, ')
          ..write('sourceId: $sourceId, ')
          ..write('reversesMovementId: $reversesMovementId, ')
          ..write('clientRequestId: $clientRequestId, ')
          ..write('occurredAt: $occurredAt, ')
          ..write('createdAt: $createdAt, ')
          ..write('createdBy: $createdBy, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $StockBalancesTable extends StockBalances
    with TableInfo<$StockBalancesTable, StockBalanceRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $StockBalancesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _productIdMeta = const VerificationMeta(
    'productId',
  );
  @override
  late final GeneratedColumn<String> productId = GeneratedColumn<String>(
    'product_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES products (id)',
    ),
  );
  static const VerificationMeta _warehouseIdMeta = const VerificationMeta(
    'warehouseId',
  );
  @override
  late final GeneratedColumn<String> warehouseId = GeneratedColumn<String>(
    'warehouse_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES warehouses (id)',
    ),
  );
  static const VerificationMeta _qtyE4Meta = const VerificationMeta('qtyE4');
  @override
  late final GeneratedColumn<int> qtyE4 = GeneratedColumn<int>(
    'qty_e4',
    aliasedName,
    false,
    check: () => ComparableExpr(qtyE4).isBiggerOrEqualValue(0),
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [
    companyId,
    productId,
    warehouseId,
    qtyE4,
    updatedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'stock_balances';
  @override
  VerificationContext validateIntegrity(
    Insertable<StockBalanceRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('product_id')) {
      context.handle(
        _productIdMeta,
        productId.isAcceptableOrUnknown(data['product_id']!, _productIdMeta),
      );
    } else if (isInserting) {
      context.missing(_productIdMeta);
    }
    if (data.containsKey('warehouse_id')) {
      context.handle(
        _warehouseIdMeta,
        warehouseId.isAcceptableOrUnknown(
          data['warehouse_id']!,
          _warehouseIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_warehouseIdMeta);
    }
    if (data.containsKey('qty_e4')) {
      context.handle(
        _qtyE4Meta,
        qtyE4.isAcceptableOrUnknown(data['qty_e4']!, _qtyE4Meta),
      );
    } else if (isInserting) {
      context.missing(_qtyE4Meta);
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {companyId, productId, warehouseId};
  @override
  StockBalanceRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return StockBalanceRow(
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      productId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}product_id'],
      )!,
      warehouseId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}warehouse_id'],
      )!,
      qtyE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}qty_e4'],
      )!,
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
    );
  }

  @override
  $StockBalancesTable createAlias(String alias) {
    return $StockBalancesTable(attachedDatabase, alias);
  }
}

class StockBalanceRow extends DataClass implements Insertable<StockBalanceRow> {
  final String companyId;
  final String productId;
  final String warehouseId;
  final int qtyE4;
  final DateTime updatedAt;
  const StockBalanceRow({
    required this.companyId,
    required this.productId,
    required this.warehouseId,
    required this.qtyE4,
    required this.updatedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['company_id'] = Variable<String>(companyId);
    map['product_id'] = Variable<String>(productId);
    map['warehouse_id'] = Variable<String>(warehouseId);
    map['qty_e4'] = Variable<int>(qtyE4);
    map['updated_at'] = Variable<DateTime>(updatedAt);
    return map;
  }

  StockBalancesCompanion toCompanion(bool nullToAbsent) {
    return StockBalancesCompanion(
      companyId: Value(companyId),
      productId: Value(productId),
      warehouseId: Value(warehouseId),
      qtyE4: Value(qtyE4),
      updatedAt: Value(updatedAt),
    );
  }

  factory StockBalanceRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return StockBalanceRow(
      companyId: serializer.fromJson<String>(json['companyId']),
      productId: serializer.fromJson<String>(json['productId']),
      warehouseId: serializer.fromJson<String>(json['warehouseId']),
      qtyE4: serializer.fromJson<int>(json['qtyE4']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'companyId': serializer.toJson<String>(companyId),
      'productId': serializer.toJson<String>(productId),
      'warehouseId': serializer.toJson<String>(warehouseId),
      'qtyE4': serializer.toJson<int>(qtyE4),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
    };
  }

  StockBalanceRow copyWith({
    String? companyId,
    String? productId,
    String? warehouseId,
    int? qtyE4,
    DateTime? updatedAt,
  }) => StockBalanceRow(
    companyId: companyId ?? this.companyId,
    productId: productId ?? this.productId,
    warehouseId: warehouseId ?? this.warehouseId,
    qtyE4: qtyE4 ?? this.qtyE4,
    updatedAt: updatedAt ?? this.updatedAt,
  );
  StockBalanceRow copyWithCompanion(StockBalancesCompanion data) {
    return StockBalanceRow(
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      productId: data.productId.present ? data.productId.value : this.productId,
      warehouseId: data.warehouseId.present
          ? data.warehouseId.value
          : this.warehouseId,
      qtyE4: data.qtyE4.present ? data.qtyE4.value : this.qtyE4,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('StockBalanceRow(')
          ..write('companyId: $companyId, ')
          ..write('productId: $productId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('qtyE4: $qtyE4, ')
          ..write('updatedAt: $updatedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode =>
      Object.hash(companyId, productId, warehouseId, qtyE4, updatedAt);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is StockBalanceRow &&
          other.companyId == this.companyId &&
          other.productId == this.productId &&
          other.warehouseId == this.warehouseId &&
          other.qtyE4 == this.qtyE4 &&
          other.updatedAt == this.updatedAt);
}

class StockBalancesCompanion extends UpdateCompanion<StockBalanceRow> {
  final Value<String> companyId;
  final Value<String> productId;
  final Value<String> warehouseId;
  final Value<int> qtyE4;
  final Value<DateTime> updatedAt;
  final Value<int> rowid;
  const StockBalancesCompanion({
    this.companyId = const Value.absent(),
    this.productId = const Value.absent(),
    this.warehouseId = const Value.absent(),
    this.qtyE4 = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  StockBalancesCompanion.insert({
    required String companyId,
    required String productId,
    required String warehouseId,
    required int qtyE4,
    required DateTime updatedAt,
    this.rowid = const Value.absent(),
  }) : companyId = Value(companyId),
       productId = Value(productId),
       warehouseId = Value(warehouseId),
       qtyE4 = Value(qtyE4),
       updatedAt = Value(updatedAt);
  static Insertable<StockBalanceRow> custom({
    Expression<String>? companyId,
    Expression<String>? productId,
    Expression<String>? warehouseId,
    Expression<int>? qtyE4,
    Expression<DateTime>? updatedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (companyId != null) 'company_id': companyId,
      if (productId != null) 'product_id': productId,
      if (warehouseId != null) 'warehouse_id': warehouseId,
      if (qtyE4 != null) 'qty_e4': qtyE4,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  StockBalancesCompanion copyWith({
    Value<String>? companyId,
    Value<String>? productId,
    Value<String>? warehouseId,
    Value<int>? qtyE4,
    Value<DateTime>? updatedAt,
    Value<int>? rowid,
  }) {
    return StockBalancesCompanion(
      companyId: companyId ?? this.companyId,
      productId: productId ?? this.productId,
      warehouseId: warehouseId ?? this.warehouseId,
      qtyE4: qtyE4 ?? this.qtyE4,
      updatedAt: updatedAt ?? this.updatedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (productId.present) {
      map['product_id'] = Variable<String>(productId.value);
    }
    if (warehouseId.present) {
      map['warehouse_id'] = Variable<String>(warehouseId.value);
    }
    if (qtyE4.present) {
      map['qty_e4'] = Variable<int>(qtyE4.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('StockBalancesCompanion(')
          ..write('companyId: $companyId, ')
          ..write('productId: $productId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('qtyE4: $qtyE4, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $SalesTable extends Sales with TableInfo<$SalesTable, SaleRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $SalesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _receiptNumberMeta = const VerificationMeta(
    'receiptNumber',
  );
  @override
  late final GeneratedColumn<String> receiptNumber = GeneratedColumn<String>(
    'receipt_number',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _clientRequestIdMeta = const VerificationMeta(
    'clientRequestId',
  );
  @override
  late final GeneratedColumn<String> clientRequestId = GeneratedColumn<String>(
    'client_request_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _warehouseIdMeta = const VerificationMeta(
    'warehouseId',
  );
  @override
  late final GeneratedColumn<String> warehouseId = GeneratedColumn<String>(
    'warehouse_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES warehouses (id)',
    ),
  );
  static const VerificationMeta _subtotalE4Meta = const VerificationMeta(
    'subtotalE4',
  );
  @override
  late final GeneratedColumn<int> subtotalE4 = GeneratedColumn<int>(
    'subtotal_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _discountE4Meta = const VerificationMeta(
    'discountE4',
  );
  @override
  late final GeneratedColumn<int> discountE4 = GeneratedColumn<int>(
    'discount_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _taxE4Meta = const VerificationMeta('taxE4');
  @override
  late final GeneratedColumn<int> taxE4 = GeneratedColumn<int>(
    'tax_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _totalE4Meta = const VerificationMeta(
    'totalE4',
  );
  @override
  late final GeneratedColumn<int> totalE4 = GeneratedColumn<int>(
    'total_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _amountPaidE4Meta = const VerificationMeta(
    'amountPaidE4',
  );
  @override
  late final GeneratedColumn<int> amountPaidE4 = GeneratedColumn<int>(
    'amount_paid_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _statusMeta = const VerificationMeta('status');
  @override
  late final GeneratedColumn<String> status = GeneratedColumn<String>(
    'status',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
    defaultValue: const Constant('completed'),
  );
  static const VerificationMeta _changeE4Meta = const VerificationMeta(
    'changeE4',
  );
  @override
  late final GeneratedColumn<int> changeE4 = GeneratedColumn<int>(
    'change_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _taxRateBpMeta = const VerificationMeta(
    'taxRateBp',
  );
  @override
  late final GeneratedColumn<int> taxRateBp = GeneratedColumn<int>(
    'tax_rate_bp',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _discountReasonMeta = const VerificationMeta(
    'discountReason',
  );
  @override
  late final GeneratedColumn<String> discountReason = GeneratedColumn<String>(
    'discount_reason',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _paymentMethodMeta = const VerificationMeta(
    'paymentMethod',
  );
  @override
  late final GeneratedColumn<String> paymentMethod = GeneratedColumn<String>(
    'payment_method',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _createdAtMeta = const VerificationMeta(
    'createdAt',
  );
  @override
  late final GeneratedColumn<DateTime> createdAt = GeneratedColumn<DateTime>(
    'created_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _createdByMeta = const VerificationMeta(
    'createdBy',
  );
  @override
  late final GeneratedColumn<String> createdBy = GeneratedColumn<String>(
    'created_by',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _originDeviceIdMeta = const VerificationMeta(
    'originDeviceId',
  );
  @override
  late final GeneratedColumn<String> originDeviceId = GeneratedColumn<String>(
    'origin_device_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    receiptNumber,
    clientRequestId,
    warehouseId,
    subtotalE4,
    discountE4,
    taxE4,
    totalE4,
    amountPaidE4,
    status,
    changeE4,
    taxRateBp,
    discountReason,
    paymentMethod,
    createdAt,
    createdBy,
    originDeviceId,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'sales';
  @override
  VerificationContext validateIntegrity(
    Insertable<SaleRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('receipt_number')) {
      context.handle(
        _receiptNumberMeta,
        receiptNumber.isAcceptableOrUnknown(
          data['receipt_number']!,
          _receiptNumberMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_receiptNumberMeta);
    }
    if (data.containsKey('client_request_id')) {
      context.handle(
        _clientRequestIdMeta,
        clientRequestId.isAcceptableOrUnknown(
          data['client_request_id']!,
          _clientRequestIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_clientRequestIdMeta);
    }
    if (data.containsKey('warehouse_id')) {
      context.handle(
        _warehouseIdMeta,
        warehouseId.isAcceptableOrUnknown(
          data['warehouse_id']!,
          _warehouseIdMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_warehouseIdMeta);
    }
    if (data.containsKey('subtotal_e4')) {
      context.handle(
        _subtotalE4Meta,
        subtotalE4.isAcceptableOrUnknown(data['subtotal_e4']!, _subtotalE4Meta),
      );
    } else if (isInserting) {
      context.missing(_subtotalE4Meta);
    }
    if (data.containsKey('discount_e4')) {
      context.handle(
        _discountE4Meta,
        discountE4.isAcceptableOrUnknown(data['discount_e4']!, _discountE4Meta),
      );
    }
    if (data.containsKey('tax_e4')) {
      context.handle(
        _taxE4Meta,
        taxE4.isAcceptableOrUnknown(data['tax_e4']!, _taxE4Meta),
      );
    }
    if (data.containsKey('total_e4')) {
      context.handle(
        _totalE4Meta,
        totalE4.isAcceptableOrUnknown(data['total_e4']!, _totalE4Meta),
      );
    } else if (isInserting) {
      context.missing(_totalE4Meta);
    }
    if (data.containsKey('amount_paid_e4')) {
      context.handle(
        _amountPaidE4Meta,
        amountPaidE4.isAcceptableOrUnknown(
          data['amount_paid_e4']!,
          _amountPaidE4Meta,
        ),
      );
    } else if (isInserting) {
      context.missing(_amountPaidE4Meta);
    }
    if (data.containsKey('status')) {
      context.handle(
        _statusMeta,
        status.isAcceptableOrUnknown(data['status']!, _statusMeta),
      );
    }
    if (data.containsKey('change_e4')) {
      context.handle(
        _changeE4Meta,
        changeE4.isAcceptableOrUnknown(data['change_e4']!, _changeE4Meta),
      );
    }
    if (data.containsKey('tax_rate_bp')) {
      context.handle(
        _taxRateBpMeta,
        taxRateBp.isAcceptableOrUnknown(data['tax_rate_bp']!, _taxRateBpMeta),
      );
    }
    if (data.containsKey('discount_reason')) {
      context.handle(
        _discountReasonMeta,
        discountReason.isAcceptableOrUnknown(
          data['discount_reason']!,
          _discountReasonMeta,
        ),
      );
    }
    if (data.containsKey('payment_method')) {
      context.handle(
        _paymentMethodMeta,
        paymentMethod.isAcceptableOrUnknown(
          data['payment_method']!,
          _paymentMethodMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_paymentMethodMeta);
    }
    if (data.containsKey('created_at')) {
      context.handle(
        _createdAtMeta,
        createdAt.isAcceptableOrUnknown(data['created_at']!, _createdAtMeta),
      );
    } else if (isInserting) {
      context.missing(_createdAtMeta);
    }
    if (data.containsKey('created_by')) {
      context.handle(
        _createdByMeta,
        createdBy.isAcceptableOrUnknown(data['created_by']!, _createdByMeta),
      );
    } else if (isInserting) {
      context.missing(_createdByMeta);
    }
    if (data.containsKey('origin_device_id')) {
      context.handle(
        _originDeviceIdMeta,
        originDeviceId.isAcceptableOrUnknown(
          data['origin_device_id']!,
          _originDeviceIdMeta,
        ),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  SaleRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return SaleRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      receiptNumber: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}receipt_number'],
      )!,
      clientRequestId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}client_request_id'],
      )!,
      warehouseId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}warehouse_id'],
      )!,
      subtotalE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}subtotal_e4'],
      )!,
      discountE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}discount_e4'],
      )!,
      taxE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}tax_e4'],
      )!,
      totalE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}total_e4'],
      )!,
      amountPaidE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}amount_paid_e4'],
      )!,
      status: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}status'],
      )!,
      changeE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}change_e4'],
      )!,
      taxRateBp: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}tax_rate_bp'],
      )!,
      discountReason: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}discount_reason'],
      ),
      paymentMethod: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}payment_method'],
      )!,
      createdAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}created_at'],
      )!,
      createdBy: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}created_by'],
      )!,
      originDeviceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}origin_device_id'],
      ),
    );
  }

  @override
  $SalesTable createAlias(String alias) {
    return $SalesTable(attachedDatabase, alias);
  }
}

class SaleRow extends DataClass implements Insertable<SaleRow> {
  final String id;
  final String companyId;
  final String receiptNumber;
  final String clientRequestId;
  final String warehouseId;
  final int subtotalE4;
  final int discountE4;
  final int taxE4;
  final int totalE4;
  final int amountPaidE4;
  final String status;
  final int changeE4;
  final int taxRateBp;
  final String? discountReason;
  final String paymentMethod;
  final DateTime createdAt;
  final String createdBy;
  final String? originDeviceId;
  const SaleRow({
    required this.id,
    required this.companyId,
    required this.receiptNumber,
    required this.clientRequestId,
    required this.warehouseId,
    required this.subtotalE4,
    required this.discountE4,
    required this.taxE4,
    required this.totalE4,
    required this.amountPaidE4,
    required this.status,
    required this.changeE4,
    required this.taxRateBp,
    this.discountReason,
    required this.paymentMethod,
    required this.createdAt,
    required this.createdBy,
    this.originDeviceId,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['receipt_number'] = Variable<String>(receiptNumber);
    map['client_request_id'] = Variable<String>(clientRequestId);
    map['warehouse_id'] = Variable<String>(warehouseId);
    map['subtotal_e4'] = Variable<int>(subtotalE4);
    map['discount_e4'] = Variable<int>(discountE4);
    map['tax_e4'] = Variable<int>(taxE4);
    map['total_e4'] = Variable<int>(totalE4);
    map['amount_paid_e4'] = Variable<int>(amountPaidE4);
    map['status'] = Variable<String>(status);
    map['change_e4'] = Variable<int>(changeE4);
    map['tax_rate_bp'] = Variable<int>(taxRateBp);
    if (!nullToAbsent || discountReason != null) {
      map['discount_reason'] = Variable<String>(discountReason);
    }
    map['payment_method'] = Variable<String>(paymentMethod);
    map['created_at'] = Variable<DateTime>(createdAt);
    map['created_by'] = Variable<String>(createdBy);
    if (!nullToAbsent || originDeviceId != null) {
      map['origin_device_id'] = Variable<String>(originDeviceId);
    }
    return map;
  }

  SalesCompanion toCompanion(bool nullToAbsent) {
    return SalesCompanion(
      id: Value(id),
      companyId: Value(companyId),
      receiptNumber: Value(receiptNumber),
      clientRequestId: Value(clientRequestId),
      warehouseId: Value(warehouseId),
      subtotalE4: Value(subtotalE4),
      discountE4: Value(discountE4),
      taxE4: Value(taxE4),
      totalE4: Value(totalE4),
      amountPaidE4: Value(amountPaidE4),
      status: Value(status),
      changeE4: Value(changeE4),
      taxRateBp: Value(taxRateBp),
      discountReason: discountReason == null && nullToAbsent
          ? const Value.absent()
          : Value(discountReason),
      paymentMethod: Value(paymentMethod),
      createdAt: Value(createdAt),
      createdBy: Value(createdBy),
      originDeviceId: originDeviceId == null && nullToAbsent
          ? const Value.absent()
          : Value(originDeviceId),
    );
  }

  factory SaleRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return SaleRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      receiptNumber: serializer.fromJson<String>(json['receiptNumber']),
      clientRequestId: serializer.fromJson<String>(json['clientRequestId']),
      warehouseId: serializer.fromJson<String>(json['warehouseId']),
      subtotalE4: serializer.fromJson<int>(json['subtotalE4']),
      discountE4: serializer.fromJson<int>(json['discountE4']),
      taxE4: serializer.fromJson<int>(json['taxE4']),
      totalE4: serializer.fromJson<int>(json['totalE4']),
      amountPaidE4: serializer.fromJson<int>(json['amountPaidE4']),
      status: serializer.fromJson<String>(json['status']),
      changeE4: serializer.fromJson<int>(json['changeE4']),
      taxRateBp: serializer.fromJson<int>(json['taxRateBp']),
      discountReason: serializer.fromJson<String?>(json['discountReason']),
      paymentMethod: serializer.fromJson<String>(json['paymentMethod']),
      createdAt: serializer.fromJson<DateTime>(json['createdAt']),
      createdBy: serializer.fromJson<String>(json['createdBy']),
      originDeviceId: serializer.fromJson<String?>(json['originDeviceId']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'receiptNumber': serializer.toJson<String>(receiptNumber),
      'clientRequestId': serializer.toJson<String>(clientRequestId),
      'warehouseId': serializer.toJson<String>(warehouseId),
      'subtotalE4': serializer.toJson<int>(subtotalE4),
      'discountE4': serializer.toJson<int>(discountE4),
      'taxE4': serializer.toJson<int>(taxE4),
      'totalE4': serializer.toJson<int>(totalE4),
      'amountPaidE4': serializer.toJson<int>(amountPaidE4),
      'status': serializer.toJson<String>(status),
      'changeE4': serializer.toJson<int>(changeE4),
      'taxRateBp': serializer.toJson<int>(taxRateBp),
      'discountReason': serializer.toJson<String?>(discountReason),
      'paymentMethod': serializer.toJson<String>(paymentMethod),
      'createdAt': serializer.toJson<DateTime>(createdAt),
      'createdBy': serializer.toJson<String>(createdBy),
      'originDeviceId': serializer.toJson<String?>(originDeviceId),
    };
  }

  SaleRow copyWith({
    String? id,
    String? companyId,
    String? receiptNumber,
    String? clientRequestId,
    String? warehouseId,
    int? subtotalE4,
    int? discountE4,
    int? taxE4,
    int? totalE4,
    int? amountPaidE4,
    String? status,
    int? changeE4,
    int? taxRateBp,
    Value<String?> discountReason = const Value.absent(),
    String? paymentMethod,
    DateTime? createdAt,
    String? createdBy,
    Value<String?> originDeviceId = const Value.absent(),
  }) => SaleRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    receiptNumber: receiptNumber ?? this.receiptNumber,
    clientRequestId: clientRequestId ?? this.clientRequestId,
    warehouseId: warehouseId ?? this.warehouseId,
    subtotalE4: subtotalE4 ?? this.subtotalE4,
    discountE4: discountE4 ?? this.discountE4,
    taxE4: taxE4 ?? this.taxE4,
    totalE4: totalE4 ?? this.totalE4,
    amountPaidE4: amountPaidE4 ?? this.amountPaidE4,
    status: status ?? this.status,
    changeE4: changeE4 ?? this.changeE4,
    taxRateBp: taxRateBp ?? this.taxRateBp,
    discountReason: discountReason.present
        ? discountReason.value
        : this.discountReason,
    paymentMethod: paymentMethod ?? this.paymentMethod,
    createdAt: createdAt ?? this.createdAt,
    createdBy: createdBy ?? this.createdBy,
    originDeviceId: originDeviceId.present
        ? originDeviceId.value
        : this.originDeviceId,
  );
  SaleRow copyWithCompanion(SalesCompanion data) {
    return SaleRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      receiptNumber: data.receiptNumber.present
          ? data.receiptNumber.value
          : this.receiptNumber,
      clientRequestId: data.clientRequestId.present
          ? data.clientRequestId.value
          : this.clientRequestId,
      warehouseId: data.warehouseId.present
          ? data.warehouseId.value
          : this.warehouseId,
      subtotalE4: data.subtotalE4.present
          ? data.subtotalE4.value
          : this.subtotalE4,
      discountE4: data.discountE4.present
          ? data.discountE4.value
          : this.discountE4,
      taxE4: data.taxE4.present ? data.taxE4.value : this.taxE4,
      totalE4: data.totalE4.present ? data.totalE4.value : this.totalE4,
      amountPaidE4: data.amountPaidE4.present
          ? data.amountPaidE4.value
          : this.amountPaidE4,
      status: data.status.present ? data.status.value : this.status,
      changeE4: data.changeE4.present ? data.changeE4.value : this.changeE4,
      taxRateBp: data.taxRateBp.present ? data.taxRateBp.value : this.taxRateBp,
      discountReason: data.discountReason.present
          ? data.discountReason.value
          : this.discountReason,
      paymentMethod: data.paymentMethod.present
          ? data.paymentMethod.value
          : this.paymentMethod,
      createdAt: data.createdAt.present ? data.createdAt.value : this.createdAt,
      createdBy: data.createdBy.present ? data.createdBy.value : this.createdBy,
      originDeviceId: data.originDeviceId.present
          ? data.originDeviceId.value
          : this.originDeviceId,
    );
  }

  @override
  String toString() {
    return (StringBuffer('SaleRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('receiptNumber: $receiptNumber, ')
          ..write('clientRequestId: $clientRequestId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('subtotalE4: $subtotalE4, ')
          ..write('discountE4: $discountE4, ')
          ..write('taxE4: $taxE4, ')
          ..write('totalE4: $totalE4, ')
          ..write('amountPaidE4: $amountPaidE4, ')
          ..write('status: $status, ')
          ..write('changeE4: $changeE4, ')
          ..write('taxRateBp: $taxRateBp, ')
          ..write('discountReason: $discountReason, ')
          ..write('paymentMethod: $paymentMethod, ')
          ..write('createdAt: $createdAt, ')
          ..write('createdBy: $createdBy, ')
          ..write('originDeviceId: $originDeviceId')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    receiptNumber,
    clientRequestId,
    warehouseId,
    subtotalE4,
    discountE4,
    taxE4,
    totalE4,
    amountPaidE4,
    status,
    changeE4,
    taxRateBp,
    discountReason,
    paymentMethod,
    createdAt,
    createdBy,
    originDeviceId,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SaleRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.receiptNumber == this.receiptNumber &&
          other.clientRequestId == this.clientRequestId &&
          other.warehouseId == this.warehouseId &&
          other.subtotalE4 == this.subtotalE4 &&
          other.discountE4 == this.discountE4 &&
          other.taxE4 == this.taxE4 &&
          other.totalE4 == this.totalE4 &&
          other.amountPaidE4 == this.amountPaidE4 &&
          other.status == this.status &&
          other.changeE4 == this.changeE4 &&
          other.taxRateBp == this.taxRateBp &&
          other.discountReason == this.discountReason &&
          other.paymentMethod == this.paymentMethod &&
          other.createdAt == this.createdAt &&
          other.createdBy == this.createdBy &&
          other.originDeviceId == this.originDeviceId);
}

class SalesCompanion extends UpdateCompanion<SaleRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<String> receiptNumber;
  final Value<String> clientRequestId;
  final Value<String> warehouseId;
  final Value<int> subtotalE4;
  final Value<int> discountE4;
  final Value<int> taxE4;
  final Value<int> totalE4;
  final Value<int> amountPaidE4;
  final Value<String> status;
  final Value<int> changeE4;
  final Value<int> taxRateBp;
  final Value<String?> discountReason;
  final Value<String> paymentMethod;
  final Value<DateTime> createdAt;
  final Value<String> createdBy;
  final Value<String?> originDeviceId;
  final Value<int> rowid;
  const SalesCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.receiptNumber = const Value.absent(),
    this.clientRequestId = const Value.absent(),
    this.warehouseId = const Value.absent(),
    this.subtotalE4 = const Value.absent(),
    this.discountE4 = const Value.absent(),
    this.taxE4 = const Value.absent(),
    this.totalE4 = const Value.absent(),
    this.amountPaidE4 = const Value.absent(),
    this.status = const Value.absent(),
    this.changeE4 = const Value.absent(),
    this.taxRateBp = const Value.absent(),
    this.discountReason = const Value.absent(),
    this.paymentMethod = const Value.absent(),
    this.createdAt = const Value.absent(),
    this.createdBy = const Value.absent(),
    this.originDeviceId = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  SalesCompanion.insert({
    required String id,
    required String companyId,
    required String receiptNumber,
    required String clientRequestId,
    required String warehouseId,
    required int subtotalE4,
    this.discountE4 = const Value.absent(),
    this.taxE4 = const Value.absent(),
    required int totalE4,
    required int amountPaidE4,
    this.status = const Value.absent(),
    this.changeE4 = const Value.absent(),
    this.taxRateBp = const Value.absent(),
    this.discountReason = const Value.absent(),
    required String paymentMethod,
    required DateTime createdAt,
    required String createdBy,
    this.originDeviceId = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       receiptNumber = Value(receiptNumber),
       clientRequestId = Value(clientRequestId),
       warehouseId = Value(warehouseId),
       subtotalE4 = Value(subtotalE4),
       totalE4 = Value(totalE4),
       amountPaidE4 = Value(amountPaidE4),
       paymentMethod = Value(paymentMethod),
       createdAt = Value(createdAt),
       createdBy = Value(createdBy);
  static Insertable<SaleRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<String>? receiptNumber,
    Expression<String>? clientRequestId,
    Expression<String>? warehouseId,
    Expression<int>? subtotalE4,
    Expression<int>? discountE4,
    Expression<int>? taxE4,
    Expression<int>? totalE4,
    Expression<int>? amountPaidE4,
    Expression<String>? status,
    Expression<int>? changeE4,
    Expression<int>? taxRateBp,
    Expression<String>? discountReason,
    Expression<String>? paymentMethod,
    Expression<DateTime>? createdAt,
    Expression<String>? createdBy,
    Expression<String>? originDeviceId,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (receiptNumber != null) 'receipt_number': receiptNumber,
      if (clientRequestId != null) 'client_request_id': clientRequestId,
      if (warehouseId != null) 'warehouse_id': warehouseId,
      if (subtotalE4 != null) 'subtotal_e4': subtotalE4,
      if (discountE4 != null) 'discount_e4': discountE4,
      if (taxE4 != null) 'tax_e4': taxE4,
      if (totalE4 != null) 'total_e4': totalE4,
      if (amountPaidE4 != null) 'amount_paid_e4': amountPaidE4,
      if (status != null) 'status': status,
      if (changeE4 != null) 'change_e4': changeE4,
      if (taxRateBp != null) 'tax_rate_bp': taxRateBp,
      if (discountReason != null) 'discount_reason': discountReason,
      if (paymentMethod != null) 'payment_method': paymentMethod,
      if (createdAt != null) 'created_at': createdAt,
      if (createdBy != null) 'created_by': createdBy,
      if (originDeviceId != null) 'origin_device_id': originDeviceId,
      if (rowid != null) 'rowid': rowid,
    });
  }

  SalesCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<String>? receiptNumber,
    Value<String>? clientRequestId,
    Value<String>? warehouseId,
    Value<int>? subtotalE4,
    Value<int>? discountE4,
    Value<int>? taxE4,
    Value<int>? totalE4,
    Value<int>? amountPaidE4,
    Value<String>? status,
    Value<int>? changeE4,
    Value<int>? taxRateBp,
    Value<String?>? discountReason,
    Value<String>? paymentMethod,
    Value<DateTime>? createdAt,
    Value<String>? createdBy,
    Value<String?>? originDeviceId,
    Value<int>? rowid,
  }) {
    return SalesCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      receiptNumber: receiptNumber ?? this.receiptNumber,
      clientRequestId: clientRequestId ?? this.clientRequestId,
      warehouseId: warehouseId ?? this.warehouseId,
      subtotalE4: subtotalE4 ?? this.subtotalE4,
      discountE4: discountE4 ?? this.discountE4,
      taxE4: taxE4 ?? this.taxE4,
      totalE4: totalE4 ?? this.totalE4,
      amountPaidE4: amountPaidE4 ?? this.amountPaidE4,
      status: status ?? this.status,
      changeE4: changeE4 ?? this.changeE4,
      taxRateBp: taxRateBp ?? this.taxRateBp,
      discountReason: discountReason ?? this.discountReason,
      paymentMethod: paymentMethod ?? this.paymentMethod,
      createdAt: createdAt ?? this.createdAt,
      createdBy: createdBy ?? this.createdBy,
      originDeviceId: originDeviceId ?? this.originDeviceId,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (receiptNumber.present) {
      map['receipt_number'] = Variable<String>(receiptNumber.value);
    }
    if (clientRequestId.present) {
      map['client_request_id'] = Variable<String>(clientRequestId.value);
    }
    if (warehouseId.present) {
      map['warehouse_id'] = Variable<String>(warehouseId.value);
    }
    if (subtotalE4.present) {
      map['subtotal_e4'] = Variable<int>(subtotalE4.value);
    }
    if (discountE4.present) {
      map['discount_e4'] = Variable<int>(discountE4.value);
    }
    if (taxE4.present) {
      map['tax_e4'] = Variable<int>(taxE4.value);
    }
    if (totalE4.present) {
      map['total_e4'] = Variable<int>(totalE4.value);
    }
    if (amountPaidE4.present) {
      map['amount_paid_e4'] = Variable<int>(amountPaidE4.value);
    }
    if (status.present) {
      map['status'] = Variable<String>(status.value);
    }
    if (changeE4.present) {
      map['change_e4'] = Variable<int>(changeE4.value);
    }
    if (taxRateBp.present) {
      map['tax_rate_bp'] = Variable<int>(taxRateBp.value);
    }
    if (discountReason.present) {
      map['discount_reason'] = Variable<String>(discountReason.value);
    }
    if (paymentMethod.present) {
      map['payment_method'] = Variable<String>(paymentMethod.value);
    }
    if (createdAt.present) {
      map['created_at'] = Variable<DateTime>(createdAt.value);
    }
    if (createdBy.present) {
      map['created_by'] = Variable<String>(createdBy.value);
    }
    if (originDeviceId.present) {
      map['origin_device_id'] = Variable<String>(originDeviceId.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('SalesCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('receiptNumber: $receiptNumber, ')
          ..write('clientRequestId: $clientRequestId, ')
          ..write('warehouseId: $warehouseId, ')
          ..write('subtotalE4: $subtotalE4, ')
          ..write('discountE4: $discountE4, ')
          ..write('taxE4: $taxE4, ')
          ..write('totalE4: $totalE4, ')
          ..write('amountPaidE4: $amountPaidE4, ')
          ..write('status: $status, ')
          ..write('changeE4: $changeE4, ')
          ..write('taxRateBp: $taxRateBp, ')
          ..write('discountReason: $discountReason, ')
          ..write('paymentMethod: $paymentMethod, ')
          ..write('createdAt: $createdAt, ')
          ..write('createdBy: $createdBy, ')
          ..write('originDeviceId: $originDeviceId, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $SaleLinesTable extends SaleLines
    with TableInfo<$SaleLinesTable, SaleLineRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $SaleLinesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _saleIdMeta = const VerificationMeta('saleId');
  @override
  late final GeneratedColumn<String> saleId = GeneratedColumn<String>(
    'sale_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES sales (id)',
    ),
  );
  static const VerificationMeta _productIdMeta = const VerificationMeta(
    'productId',
  );
  @override
  late final GeneratedColumn<String> productId = GeneratedColumn<String>(
    'product_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES products (id)',
    ),
  );
  static const VerificationMeta _skuSnapshotMeta = const VerificationMeta(
    'skuSnapshot',
  );
  @override
  late final GeneratedColumn<String> skuSnapshot = GeneratedColumn<String>(
    'sku_snapshot',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _nameSnapshotMeta = const VerificationMeta(
    'nameSnapshot',
  );
  @override
  late final GeneratedColumn<String> nameSnapshot = GeneratedColumn<String>(
    'name_snapshot',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _quantityE4Meta = const VerificationMeta(
    'quantityE4',
  );
  @override
  late final GeneratedColumn<int> quantityE4 = GeneratedColumn<int>(
    'quantity_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _unitPriceE4Meta = const VerificationMeta(
    'unitPriceE4',
  );
  @override
  late final GeneratedColumn<int> unitPriceE4 = GeneratedColumn<int>(
    'unit_price_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _discountE4Meta = const VerificationMeta(
    'discountE4',
  );
  @override
  late final GeneratedColumn<int> discountE4 = GeneratedColumn<int>(
    'discount_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _lineTotalE4Meta = const VerificationMeta(
    'lineTotalE4',
  );
  @override
  late final GeneratedColumn<int> lineTotalE4 = GeneratedColumn<int>(
    'line_total_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _taxE4Meta = const VerificationMeta('taxE4');
  @override
  late final GeneratedColumn<int> taxE4 = GeneratedColumn<int>(
    'tax_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    saleId,
    productId,
    skuSnapshot,
    nameSnapshot,
    quantityE4,
    unitPriceE4,
    discountE4,
    lineTotalE4,
    taxE4,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'sale_lines';
  @override
  VerificationContext validateIntegrity(
    Insertable<SaleLineRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('sale_id')) {
      context.handle(
        _saleIdMeta,
        saleId.isAcceptableOrUnknown(data['sale_id']!, _saleIdMeta),
      );
    } else if (isInserting) {
      context.missing(_saleIdMeta);
    }
    if (data.containsKey('product_id')) {
      context.handle(
        _productIdMeta,
        productId.isAcceptableOrUnknown(data['product_id']!, _productIdMeta),
      );
    } else if (isInserting) {
      context.missing(_productIdMeta);
    }
    if (data.containsKey('sku_snapshot')) {
      context.handle(
        _skuSnapshotMeta,
        skuSnapshot.isAcceptableOrUnknown(
          data['sku_snapshot']!,
          _skuSnapshotMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_skuSnapshotMeta);
    }
    if (data.containsKey('name_snapshot')) {
      context.handle(
        _nameSnapshotMeta,
        nameSnapshot.isAcceptableOrUnknown(
          data['name_snapshot']!,
          _nameSnapshotMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_nameSnapshotMeta);
    }
    if (data.containsKey('quantity_e4')) {
      context.handle(
        _quantityE4Meta,
        quantityE4.isAcceptableOrUnknown(data['quantity_e4']!, _quantityE4Meta),
      );
    } else if (isInserting) {
      context.missing(_quantityE4Meta);
    }
    if (data.containsKey('unit_price_e4')) {
      context.handle(
        _unitPriceE4Meta,
        unitPriceE4.isAcceptableOrUnknown(
          data['unit_price_e4']!,
          _unitPriceE4Meta,
        ),
      );
    } else if (isInserting) {
      context.missing(_unitPriceE4Meta);
    }
    if (data.containsKey('discount_e4')) {
      context.handle(
        _discountE4Meta,
        discountE4.isAcceptableOrUnknown(data['discount_e4']!, _discountE4Meta),
      );
    }
    if (data.containsKey('line_total_e4')) {
      context.handle(
        _lineTotalE4Meta,
        lineTotalE4.isAcceptableOrUnknown(
          data['line_total_e4']!,
          _lineTotalE4Meta,
        ),
      );
    } else if (isInserting) {
      context.missing(_lineTotalE4Meta);
    }
    if (data.containsKey('tax_e4')) {
      context.handle(
        _taxE4Meta,
        taxE4.isAcceptableOrUnknown(data['tax_e4']!, _taxE4Meta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  SaleLineRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return SaleLineRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      saleId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}sale_id'],
      )!,
      productId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}product_id'],
      )!,
      skuSnapshot: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}sku_snapshot'],
      )!,
      nameSnapshot: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}name_snapshot'],
      )!,
      quantityE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}quantity_e4'],
      )!,
      unitPriceE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}unit_price_e4'],
      )!,
      discountE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}discount_e4'],
      )!,
      lineTotalE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}line_total_e4'],
      )!,
      taxE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}tax_e4'],
      )!,
    );
  }

  @override
  $SaleLinesTable createAlias(String alias) {
    return $SaleLinesTable(attachedDatabase, alias);
  }
}

class SaleLineRow extends DataClass implements Insertable<SaleLineRow> {
  final String id;
  final String saleId;
  final String productId;
  final String skuSnapshot;
  final String nameSnapshot;
  final int quantityE4;
  final int unitPriceE4;
  final int discountE4;
  final int lineTotalE4;
  final int taxE4;
  const SaleLineRow({
    required this.id,
    required this.saleId,
    required this.productId,
    required this.skuSnapshot,
    required this.nameSnapshot,
    required this.quantityE4,
    required this.unitPriceE4,
    required this.discountE4,
    required this.lineTotalE4,
    required this.taxE4,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['sale_id'] = Variable<String>(saleId);
    map['product_id'] = Variable<String>(productId);
    map['sku_snapshot'] = Variable<String>(skuSnapshot);
    map['name_snapshot'] = Variable<String>(nameSnapshot);
    map['quantity_e4'] = Variable<int>(quantityE4);
    map['unit_price_e4'] = Variable<int>(unitPriceE4);
    map['discount_e4'] = Variable<int>(discountE4);
    map['line_total_e4'] = Variable<int>(lineTotalE4);
    map['tax_e4'] = Variable<int>(taxE4);
    return map;
  }

  SaleLinesCompanion toCompanion(bool nullToAbsent) {
    return SaleLinesCompanion(
      id: Value(id),
      saleId: Value(saleId),
      productId: Value(productId),
      skuSnapshot: Value(skuSnapshot),
      nameSnapshot: Value(nameSnapshot),
      quantityE4: Value(quantityE4),
      unitPriceE4: Value(unitPriceE4),
      discountE4: Value(discountE4),
      lineTotalE4: Value(lineTotalE4),
      taxE4: Value(taxE4),
    );
  }

  factory SaleLineRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return SaleLineRow(
      id: serializer.fromJson<String>(json['id']),
      saleId: serializer.fromJson<String>(json['saleId']),
      productId: serializer.fromJson<String>(json['productId']),
      skuSnapshot: serializer.fromJson<String>(json['skuSnapshot']),
      nameSnapshot: serializer.fromJson<String>(json['nameSnapshot']),
      quantityE4: serializer.fromJson<int>(json['quantityE4']),
      unitPriceE4: serializer.fromJson<int>(json['unitPriceE4']),
      discountE4: serializer.fromJson<int>(json['discountE4']),
      lineTotalE4: serializer.fromJson<int>(json['lineTotalE4']),
      taxE4: serializer.fromJson<int>(json['taxE4']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'saleId': serializer.toJson<String>(saleId),
      'productId': serializer.toJson<String>(productId),
      'skuSnapshot': serializer.toJson<String>(skuSnapshot),
      'nameSnapshot': serializer.toJson<String>(nameSnapshot),
      'quantityE4': serializer.toJson<int>(quantityE4),
      'unitPriceE4': serializer.toJson<int>(unitPriceE4),
      'discountE4': serializer.toJson<int>(discountE4),
      'lineTotalE4': serializer.toJson<int>(lineTotalE4),
      'taxE4': serializer.toJson<int>(taxE4),
    };
  }

  SaleLineRow copyWith({
    String? id,
    String? saleId,
    String? productId,
    String? skuSnapshot,
    String? nameSnapshot,
    int? quantityE4,
    int? unitPriceE4,
    int? discountE4,
    int? lineTotalE4,
    int? taxE4,
  }) => SaleLineRow(
    id: id ?? this.id,
    saleId: saleId ?? this.saleId,
    productId: productId ?? this.productId,
    skuSnapshot: skuSnapshot ?? this.skuSnapshot,
    nameSnapshot: nameSnapshot ?? this.nameSnapshot,
    quantityE4: quantityE4 ?? this.quantityE4,
    unitPriceE4: unitPriceE4 ?? this.unitPriceE4,
    discountE4: discountE4 ?? this.discountE4,
    lineTotalE4: lineTotalE4 ?? this.lineTotalE4,
    taxE4: taxE4 ?? this.taxE4,
  );
  SaleLineRow copyWithCompanion(SaleLinesCompanion data) {
    return SaleLineRow(
      id: data.id.present ? data.id.value : this.id,
      saleId: data.saleId.present ? data.saleId.value : this.saleId,
      productId: data.productId.present ? data.productId.value : this.productId,
      skuSnapshot: data.skuSnapshot.present
          ? data.skuSnapshot.value
          : this.skuSnapshot,
      nameSnapshot: data.nameSnapshot.present
          ? data.nameSnapshot.value
          : this.nameSnapshot,
      quantityE4: data.quantityE4.present
          ? data.quantityE4.value
          : this.quantityE4,
      unitPriceE4: data.unitPriceE4.present
          ? data.unitPriceE4.value
          : this.unitPriceE4,
      discountE4: data.discountE4.present
          ? data.discountE4.value
          : this.discountE4,
      lineTotalE4: data.lineTotalE4.present
          ? data.lineTotalE4.value
          : this.lineTotalE4,
      taxE4: data.taxE4.present ? data.taxE4.value : this.taxE4,
    );
  }

  @override
  String toString() {
    return (StringBuffer('SaleLineRow(')
          ..write('id: $id, ')
          ..write('saleId: $saleId, ')
          ..write('productId: $productId, ')
          ..write('skuSnapshot: $skuSnapshot, ')
          ..write('nameSnapshot: $nameSnapshot, ')
          ..write('quantityE4: $quantityE4, ')
          ..write('unitPriceE4: $unitPriceE4, ')
          ..write('discountE4: $discountE4, ')
          ..write('lineTotalE4: $lineTotalE4, ')
          ..write('taxE4: $taxE4')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    saleId,
    productId,
    skuSnapshot,
    nameSnapshot,
    quantityE4,
    unitPriceE4,
    discountE4,
    lineTotalE4,
    taxE4,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SaleLineRow &&
          other.id == this.id &&
          other.saleId == this.saleId &&
          other.productId == this.productId &&
          other.skuSnapshot == this.skuSnapshot &&
          other.nameSnapshot == this.nameSnapshot &&
          other.quantityE4 == this.quantityE4 &&
          other.unitPriceE4 == this.unitPriceE4 &&
          other.discountE4 == this.discountE4 &&
          other.lineTotalE4 == this.lineTotalE4 &&
          other.taxE4 == this.taxE4);
}

class SaleLinesCompanion extends UpdateCompanion<SaleLineRow> {
  final Value<String> id;
  final Value<String> saleId;
  final Value<String> productId;
  final Value<String> skuSnapshot;
  final Value<String> nameSnapshot;
  final Value<int> quantityE4;
  final Value<int> unitPriceE4;
  final Value<int> discountE4;
  final Value<int> lineTotalE4;
  final Value<int> taxE4;
  final Value<int> rowid;
  const SaleLinesCompanion({
    this.id = const Value.absent(),
    this.saleId = const Value.absent(),
    this.productId = const Value.absent(),
    this.skuSnapshot = const Value.absent(),
    this.nameSnapshot = const Value.absent(),
    this.quantityE4 = const Value.absent(),
    this.unitPriceE4 = const Value.absent(),
    this.discountE4 = const Value.absent(),
    this.lineTotalE4 = const Value.absent(),
    this.taxE4 = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  SaleLinesCompanion.insert({
    required String id,
    required String saleId,
    required String productId,
    required String skuSnapshot,
    required String nameSnapshot,
    required int quantityE4,
    required int unitPriceE4,
    this.discountE4 = const Value.absent(),
    required int lineTotalE4,
    this.taxE4 = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       saleId = Value(saleId),
       productId = Value(productId),
       skuSnapshot = Value(skuSnapshot),
       nameSnapshot = Value(nameSnapshot),
       quantityE4 = Value(quantityE4),
       unitPriceE4 = Value(unitPriceE4),
       lineTotalE4 = Value(lineTotalE4);
  static Insertable<SaleLineRow> custom({
    Expression<String>? id,
    Expression<String>? saleId,
    Expression<String>? productId,
    Expression<String>? skuSnapshot,
    Expression<String>? nameSnapshot,
    Expression<int>? quantityE4,
    Expression<int>? unitPriceE4,
    Expression<int>? discountE4,
    Expression<int>? lineTotalE4,
    Expression<int>? taxE4,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (saleId != null) 'sale_id': saleId,
      if (productId != null) 'product_id': productId,
      if (skuSnapshot != null) 'sku_snapshot': skuSnapshot,
      if (nameSnapshot != null) 'name_snapshot': nameSnapshot,
      if (quantityE4 != null) 'quantity_e4': quantityE4,
      if (unitPriceE4 != null) 'unit_price_e4': unitPriceE4,
      if (discountE4 != null) 'discount_e4': discountE4,
      if (lineTotalE4 != null) 'line_total_e4': lineTotalE4,
      if (taxE4 != null) 'tax_e4': taxE4,
      if (rowid != null) 'rowid': rowid,
    });
  }

  SaleLinesCompanion copyWith({
    Value<String>? id,
    Value<String>? saleId,
    Value<String>? productId,
    Value<String>? skuSnapshot,
    Value<String>? nameSnapshot,
    Value<int>? quantityE4,
    Value<int>? unitPriceE4,
    Value<int>? discountE4,
    Value<int>? lineTotalE4,
    Value<int>? taxE4,
    Value<int>? rowid,
  }) {
    return SaleLinesCompanion(
      id: id ?? this.id,
      saleId: saleId ?? this.saleId,
      productId: productId ?? this.productId,
      skuSnapshot: skuSnapshot ?? this.skuSnapshot,
      nameSnapshot: nameSnapshot ?? this.nameSnapshot,
      quantityE4: quantityE4 ?? this.quantityE4,
      unitPriceE4: unitPriceE4 ?? this.unitPriceE4,
      discountE4: discountE4 ?? this.discountE4,
      lineTotalE4: lineTotalE4 ?? this.lineTotalE4,
      taxE4: taxE4 ?? this.taxE4,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (saleId.present) {
      map['sale_id'] = Variable<String>(saleId.value);
    }
    if (productId.present) {
      map['product_id'] = Variable<String>(productId.value);
    }
    if (skuSnapshot.present) {
      map['sku_snapshot'] = Variable<String>(skuSnapshot.value);
    }
    if (nameSnapshot.present) {
      map['name_snapshot'] = Variable<String>(nameSnapshot.value);
    }
    if (quantityE4.present) {
      map['quantity_e4'] = Variable<int>(quantityE4.value);
    }
    if (unitPriceE4.present) {
      map['unit_price_e4'] = Variable<int>(unitPriceE4.value);
    }
    if (discountE4.present) {
      map['discount_e4'] = Variable<int>(discountE4.value);
    }
    if (lineTotalE4.present) {
      map['line_total_e4'] = Variable<int>(lineTotalE4.value);
    }
    if (taxE4.present) {
      map['tax_e4'] = Variable<int>(taxE4.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('SaleLinesCompanion(')
          ..write('id: $id, ')
          ..write('saleId: $saleId, ')
          ..write('productId: $productId, ')
          ..write('skuSnapshot: $skuSnapshot, ')
          ..write('nameSnapshot: $nameSnapshot, ')
          ..write('quantityE4: $quantityE4, ')
          ..write('unitPriceE4: $unitPriceE4, ')
          ..write('discountE4: $discountE4, ')
          ..write('lineTotalE4: $lineTotalE4, ')
          ..write('taxE4: $taxE4, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $SalePaymentsTable extends SalePayments
    with TableInfo<$SalePaymentsTable, SalePaymentRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $SalePaymentsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _saleIdMeta = const VerificationMeta('saleId');
  @override
  late final GeneratedColumn<String> saleId = GeneratedColumn<String>(
    'sale_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES sales (id)',
    ),
  );
  static const VerificationMeta _amountE4Meta = const VerificationMeta(
    'amountE4',
  );
  @override
  late final GeneratedColumn<int> amountE4 = GeneratedColumn<int>(
    'amount_e4',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _methodMeta = const VerificationMeta('method');
  @override
  late final GeneratedColumn<String> method = GeneratedColumn<String>(
    'method',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _receivedAtMeta = const VerificationMeta(
    'receivedAt',
  );
  @override
  late final GeneratedColumn<DateTime> receivedAt = GeneratedColumn<DateTime>(
    'received_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _referenceMeta = const VerificationMeta(
    'reference',
  );
  @override
  late final GeneratedColumn<String> reference = GeneratedColumn<String>(
    'reference',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    saleId,
    amountE4,
    method,
    receivedAt,
    reference,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'sale_payments';
  @override
  VerificationContext validateIntegrity(
    Insertable<SalePaymentRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('sale_id')) {
      context.handle(
        _saleIdMeta,
        saleId.isAcceptableOrUnknown(data['sale_id']!, _saleIdMeta),
      );
    } else if (isInserting) {
      context.missing(_saleIdMeta);
    }
    if (data.containsKey('amount_e4')) {
      context.handle(
        _amountE4Meta,
        amountE4.isAcceptableOrUnknown(data['amount_e4']!, _amountE4Meta),
      );
    } else if (isInserting) {
      context.missing(_amountE4Meta);
    }
    if (data.containsKey('method')) {
      context.handle(
        _methodMeta,
        method.isAcceptableOrUnknown(data['method']!, _methodMeta),
      );
    } else if (isInserting) {
      context.missing(_methodMeta);
    }
    if (data.containsKey('received_at')) {
      context.handle(
        _receivedAtMeta,
        receivedAt.isAcceptableOrUnknown(data['received_at']!, _receivedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_receivedAtMeta);
    }
    if (data.containsKey('reference')) {
      context.handle(
        _referenceMeta,
        reference.isAcceptableOrUnknown(data['reference']!, _referenceMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  SalePaymentRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return SalePaymentRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      saleId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}sale_id'],
      )!,
      amountE4: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}amount_e4'],
      )!,
      method: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}method'],
      )!,
      receivedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}received_at'],
      )!,
      reference: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}reference'],
      ),
    );
  }

  @override
  $SalePaymentsTable createAlias(String alias) {
    return $SalePaymentsTable(attachedDatabase, alias);
  }
}

class SalePaymentRow extends DataClass implements Insertable<SalePaymentRow> {
  final String id;
  final String saleId;
  final int amountE4;
  final String method;
  final DateTime receivedAt;
  final String? reference;
  const SalePaymentRow({
    required this.id,
    required this.saleId,
    required this.amountE4,
    required this.method,
    required this.receivedAt,
    this.reference,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['sale_id'] = Variable<String>(saleId);
    map['amount_e4'] = Variable<int>(amountE4);
    map['method'] = Variable<String>(method);
    map['received_at'] = Variable<DateTime>(receivedAt);
    if (!nullToAbsent || reference != null) {
      map['reference'] = Variable<String>(reference);
    }
    return map;
  }

  SalePaymentsCompanion toCompanion(bool nullToAbsent) {
    return SalePaymentsCompanion(
      id: Value(id),
      saleId: Value(saleId),
      amountE4: Value(amountE4),
      method: Value(method),
      receivedAt: Value(receivedAt),
      reference: reference == null && nullToAbsent
          ? const Value.absent()
          : Value(reference),
    );
  }

  factory SalePaymentRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return SalePaymentRow(
      id: serializer.fromJson<String>(json['id']),
      saleId: serializer.fromJson<String>(json['saleId']),
      amountE4: serializer.fromJson<int>(json['amountE4']),
      method: serializer.fromJson<String>(json['method']),
      receivedAt: serializer.fromJson<DateTime>(json['receivedAt']),
      reference: serializer.fromJson<String?>(json['reference']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'saleId': serializer.toJson<String>(saleId),
      'amountE4': serializer.toJson<int>(amountE4),
      'method': serializer.toJson<String>(method),
      'receivedAt': serializer.toJson<DateTime>(receivedAt),
      'reference': serializer.toJson<String?>(reference),
    };
  }

  SalePaymentRow copyWith({
    String? id,
    String? saleId,
    int? amountE4,
    String? method,
    DateTime? receivedAt,
    Value<String?> reference = const Value.absent(),
  }) => SalePaymentRow(
    id: id ?? this.id,
    saleId: saleId ?? this.saleId,
    amountE4: amountE4 ?? this.amountE4,
    method: method ?? this.method,
    receivedAt: receivedAt ?? this.receivedAt,
    reference: reference.present ? reference.value : this.reference,
  );
  SalePaymentRow copyWithCompanion(SalePaymentsCompanion data) {
    return SalePaymentRow(
      id: data.id.present ? data.id.value : this.id,
      saleId: data.saleId.present ? data.saleId.value : this.saleId,
      amountE4: data.amountE4.present ? data.amountE4.value : this.amountE4,
      method: data.method.present ? data.method.value : this.method,
      receivedAt: data.receivedAt.present
          ? data.receivedAt.value
          : this.receivedAt,
      reference: data.reference.present ? data.reference.value : this.reference,
    );
  }

  @override
  String toString() {
    return (StringBuffer('SalePaymentRow(')
          ..write('id: $id, ')
          ..write('saleId: $saleId, ')
          ..write('amountE4: $amountE4, ')
          ..write('method: $method, ')
          ..write('receivedAt: $receivedAt, ')
          ..write('reference: $reference')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode =>
      Object.hash(id, saleId, amountE4, method, receivedAt, reference);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SalePaymentRow &&
          other.id == this.id &&
          other.saleId == this.saleId &&
          other.amountE4 == this.amountE4 &&
          other.method == this.method &&
          other.receivedAt == this.receivedAt &&
          other.reference == this.reference);
}

class SalePaymentsCompanion extends UpdateCompanion<SalePaymentRow> {
  final Value<String> id;
  final Value<String> saleId;
  final Value<int> amountE4;
  final Value<String> method;
  final Value<DateTime> receivedAt;
  final Value<String?> reference;
  final Value<int> rowid;
  const SalePaymentsCompanion({
    this.id = const Value.absent(),
    this.saleId = const Value.absent(),
    this.amountE4 = const Value.absent(),
    this.method = const Value.absent(),
    this.receivedAt = const Value.absent(),
    this.reference = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  SalePaymentsCompanion.insert({
    required String id,
    required String saleId,
    required int amountE4,
    required String method,
    required DateTime receivedAt,
    this.reference = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       saleId = Value(saleId),
       amountE4 = Value(amountE4),
       method = Value(method),
       receivedAt = Value(receivedAt);
  static Insertable<SalePaymentRow> custom({
    Expression<String>? id,
    Expression<String>? saleId,
    Expression<int>? amountE4,
    Expression<String>? method,
    Expression<DateTime>? receivedAt,
    Expression<String>? reference,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (saleId != null) 'sale_id': saleId,
      if (amountE4 != null) 'amount_e4': amountE4,
      if (method != null) 'method': method,
      if (receivedAt != null) 'received_at': receivedAt,
      if (reference != null) 'reference': reference,
      if (rowid != null) 'rowid': rowid,
    });
  }

  SalePaymentsCompanion copyWith({
    Value<String>? id,
    Value<String>? saleId,
    Value<int>? amountE4,
    Value<String>? method,
    Value<DateTime>? receivedAt,
    Value<String?>? reference,
    Value<int>? rowid,
  }) {
    return SalePaymentsCompanion(
      id: id ?? this.id,
      saleId: saleId ?? this.saleId,
      amountE4: amountE4 ?? this.amountE4,
      method: method ?? this.method,
      receivedAt: receivedAt ?? this.receivedAt,
      reference: reference ?? this.reference,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (saleId.present) {
      map['sale_id'] = Variable<String>(saleId.value);
    }
    if (amountE4.present) {
      map['amount_e4'] = Variable<int>(amountE4.value);
    }
    if (method.present) {
      map['method'] = Variable<String>(method.value);
    }
    if (receivedAt.present) {
      map['received_at'] = Variable<DateTime>(receivedAt.value);
    }
    if (reference.present) {
      map['reference'] = Variable<String>(reference.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('SalePaymentsCompanion(')
          ..write('id: $id, ')
          ..write('saleId: $saleId, ')
          ..write('amountE4: $amountE4, ')
          ..write('method: $method, ')
          ..write('receivedAt: $receivedAt, ')
          ..write('reference: $reference, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $PosSettingsTable extends PosSettings
    with TableInfo<$PosSettingsTable, PosSettingsRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $PosSettingsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    check: () => id.equals('pos-settings'),
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _companyIdMeta = const VerificationMeta(
    'companyId',
  );
  @override
  late final GeneratedColumn<String> companyId = GeneratedColumn<String>(
    'company_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _taxRateBpMeta = const VerificationMeta(
    'taxRateBp',
  );
  @override
  late final GeneratedColumn<int> taxRateBp = GeneratedColumn<int>(
    'tax_rate_bp',
    aliasedName,
    false,
    check: () => ComparableExpr(taxRateBp).isBetweenValues(0, 10000),
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _maxDiscountBpMeta = const VerificationMeta(
    'maxDiscountBp',
  );
  @override
  late final GeneratedColumn<int> maxDiscountBp = GeneratedColumn<int>(
    'max_discount_bp',
    aliasedName,
    false,
    check: () => ComparableExpr(maxDiscountBp).isBetweenValues(0, 10000),
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(10000),
  );
  static const VerificationMeta _receiptFooterMeta = const VerificationMeta(
    'receiptFooter',
  );
  @override
  late final GeneratedColumn<String> receiptFooter = GeneratedColumn<String>(
    'receipt_footer',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _updatedAtMeta = const VerificationMeta(
    'updatedAt',
  );
  @override
  late final GeneratedColumn<DateTime> updatedAt = GeneratedColumn<DateTime>(
    'updated_at',
    aliasedName,
    false,
    type: DriftSqlType.dateTime,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    companyId,
    taxRateBp,
    maxDiscountBp,
    receiptFooter,
    updatedAt,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'pos_settings';
  @override
  VerificationContext validateIntegrity(
    Insertable<PosSettingsRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('company_id')) {
      context.handle(
        _companyIdMeta,
        companyId.isAcceptableOrUnknown(data['company_id']!, _companyIdMeta),
      );
    } else if (isInserting) {
      context.missing(_companyIdMeta);
    }
    if (data.containsKey('tax_rate_bp')) {
      context.handle(
        _taxRateBpMeta,
        taxRateBp.isAcceptableOrUnknown(data['tax_rate_bp']!, _taxRateBpMeta),
      );
    }
    if (data.containsKey('max_discount_bp')) {
      context.handle(
        _maxDiscountBpMeta,
        maxDiscountBp.isAcceptableOrUnknown(
          data['max_discount_bp']!,
          _maxDiscountBpMeta,
        ),
      );
    }
    if (data.containsKey('receipt_footer')) {
      context.handle(
        _receiptFooterMeta,
        receiptFooter.isAcceptableOrUnknown(
          data['receipt_footer']!,
          _receiptFooterMeta,
        ),
      );
    }
    if (data.containsKey('updated_at')) {
      context.handle(
        _updatedAtMeta,
        updatedAt.isAcceptableOrUnknown(data['updated_at']!, _updatedAtMeta),
      );
    } else if (isInserting) {
      context.missing(_updatedAtMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  PosSettingsRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return PosSettingsRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      companyId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}company_id'],
      )!,
      taxRateBp: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}tax_rate_bp'],
      )!,
      maxDiscountBp: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}max_discount_bp'],
      )!,
      receiptFooter: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}receipt_footer'],
      ),
      updatedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.dateTime,
        data['${effectivePrefix}updated_at'],
      )!,
    );
  }

  @override
  $PosSettingsTable createAlias(String alias) {
    return $PosSettingsTable(attachedDatabase, alias);
  }
}

class PosSettingsRow extends DataClass implements Insertable<PosSettingsRow> {
  final String id;
  final String companyId;
  final int taxRateBp;
  final int maxDiscountBp;
  final String? receiptFooter;
  final DateTime updatedAt;
  const PosSettingsRow({
    required this.id,
    required this.companyId,
    required this.taxRateBp,
    required this.maxDiscountBp,
    this.receiptFooter,
    required this.updatedAt,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['company_id'] = Variable<String>(companyId);
    map['tax_rate_bp'] = Variable<int>(taxRateBp);
    map['max_discount_bp'] = Variable<int>(maxDiscountBp);
    if (!nullToAbsent || receiptFooter != null) {
      map['receipt_footer'] = Variable<String>(receiptFooter);
    }
    map['updated_at'] = Variable<DateTime>(updatedAt);
    return map;
  }

  PosSettingsCompanion toCompanion(bool nullToAbsent) {
    return PosSettingsCompanion(
      id: Value(id),
      companyId: Value(companyId),
      taxRateBp: Value(taxRateBp),
      maxDiscountBp: Value(maxDiscountBp),
      receiptFooter: receiptFooter == null && nullToAbsent
          ? const Value.absent()
          : Value(receiptFooter),
      updatedAt: Value(updatedAt),
    );
  }

  factory PosSettingsRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return PosSettingsRow(
      id: serializer.fromJson<String>(json['id']),
      companyId: serializer.fromJson<String>(json['companyId']),
      taxRateBp: serializer.fromJson<int>(json['taxRateBp']),
      maxDiscountBp: serializer.fromJson<int>(json['maxDiscountBp']),
      receiptFooter: serializer.fromJson<String?>(json['receiptFooter']),
      updatedAt: serializer.fromJson<DateTime>(json['updatedAt']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'companyId': serializer.toJson<String>(companyId),
      'taxRateBp': serializer.toJson<int>(taxRateBp),
      'maxDiscountBp': serializer.toJson<int>(maxDiscountBp),
      'receiptFooter': serializer.toJson<String?>(receiptFooter),
      'updatedAt': serializer.toJson<DateTime>(updatedAt),
    };
  }

  PosSettingsRow copyWith({
    String? id,
    String? companyId,
    int? taxRateBp,
    int? maxDiscountBp,
    Value<String?> receiptFooter = const Value.absent(),
    DateTime? updatedAt,
  }) => PosSettingsRow(
    id: id ?? this.id,
    companyId: companyId ?? this.companyId,
    taxRateBp: taxRateBp ?? this.taxRateBp,
    maxDiscountBp: maxDiscountBp ?? this.maxDiscountBp,
    receiptFooter: receiptFooter.present
        ? receiptFooter.value
        : this.receiptFooter,
    updatedAt: updatedAt ?? this.updatedAt,
  );
  PosSettingsRow copyWithCompanion(PosSettingsCompanion data) {
    return PosSettingsRow(
      id: data.id.present ? data.id.value : this.id,
      companyId: data.companyId.present ? data.companyId.value : this.companyId,
      taxRateBp: data.taxRateBp.present ? data.taxRateBp.value : this.taxRateBp,
      maxDiscountBp: data.maxDiscountBp.present
          ? data.maxDiscountBp.value
          : this.maxDiscountBp,
      receiptFooter: data.receiptFooter.present
          ? data.receiptFooter.value
          : this.receiptFooter,
      updatedAt: data.updatedAt.present ? data.updatedAt.value : this.updatedAt,
    );
  }

  @override
  String toString() {
    return (StringBuffer('PosSettingsRow(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('taxRateBp: $taxRateBp, ')
          ..write('maxDiscountBp: $maxDiscountBp, ')
          ..write('receiptFooter: $receiptFooter, ')
          ..write('updatedAt: $updatedAt')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    companyId,
    taxRateBp,
    maxDiscountBp,
    receiptFooter,
    updatedAt,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is PosSettingsRow &&
          other.id == this.id &&
          other.companyId == this.companyId &&
          other.taxRateBp == this.taxRateBp &&
          other.maxDiscountBp == this.maxDiscountBp &&
          other.receiptFooter == this.receiptFooter &&
          other.updatedAt == this.updatedAt);
}

class PosSettingsCompanion extends UpdateCompanion<PosSettingsRow> {
  final Value<String> id;
  final Value<String> companyId;
  final Value<int> taxRateBp;
  final Value<int> maxDiscountBp;
  final Value<String?> receiptFooter;
  final Value<DateTime> updatedAt;
  final Value<int> rowid;
  const PosSettingsCompanion({
    this.id = const Value.absent(),
    this.companyId = const Value.absent(),
    this.taxRateBp = const Value.absent(),
    this.maxDiscountBp = const Value.absent(),
    this.receiptFooter = const Value.absent(),
    this.updatedAt = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  PosSettingsCompanion.insert({
    required String id,
    required String companyId,
    this.taxRateBp = const Value.absent(),
    this.maxDiscountBp = const Value.absent(),
    this.receiptFooter = const Value.absent(),
    required DateTime updatedAt,
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       companyId = Value(companyId),
       updatedAt = Value(updatedAt);
  static Insertable<PosSettingsRow> custom({
    Expression<String>? id,
    Expression<String>? companyId,
    Expression<int>? taxRateBp,
    Expression<int>? maxDiscountBp,
    Expression<String>? receiptFooter,
    Expression<DateTime>? updatedAt,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (companyId != null) 'company_id': companyId,
      if (taxRateBp != null) 'tax_rate_bp': taxRateBp,
      if (maxDiscountBp != null) 'max_discount_bp': maxDiscountBp,
      if (receiptFooter != null) 'receipt_footer': receiptFooter,
      if (updatedAt != null) 'updated_at': updatedAt,
      if (rowid != null) 'rowid': rowid,
    });
  }

  PosSettingsCompanion copyWith({
    Value<String>? id,
    Value<String>? companyId,
    Value<int>? taxRateBp,
    Value<int>? maxDiscountBp,
    Value<String?>? receiptFooter,
    Value<DateTime>? updatedAt,
    Value<int>? rowid,
  }) {
    return PosSettingsCompanion(
      id: id ?? this.id,
      companyId: companyId ?? this.companyId,
      taxRateBp: taxRateBp ?? this.taxRateBp,
      maxDiscountBp: maxDiscountBp ?? this.maxDiscountBp,
      receiptFooter: receiptFooter ?? this.receiptFooter,
      updatedAt: updatedAt ?? this.updatedAt,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (companyId.present) {
      map['company_id'] = Variable<String>(companyId.value);
    }
    if (taxRateBp.present) {
      map['tax_rate_bp'] = Variable<int>(taxRateBp.value);
    }
    if (maxDiscountBp.present) {
      map['max_discount_bp'] = Variable<int>(maxDiscountBp.value);
    }
    if (receiptFooter.present) {
      map['receipt_footer'] = Variable<String>(receiptFooter.value);
    }
    if (updatedAt.present) {
      map['updated_at'] = Variable<DateTime>(updatedAt.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('PosSettingsCompanion(')
          ..write('id: $id, ')
          ..write('companyId: $companyId, ')
          ..write('taxRateBp: $taxRateBp, ')
          ..write('maxDiscountBp: $maxDiscountBp, ')
          ..write('receiptFooter: $receiptFooter, ')
          ..write('updatedAt: $updatedAt, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

abstract class _$AppDatabase extends GeneratedDatabase {
  _$AppDatabase(QueryExecutor e) : super(e);
  $AppDatabaseManager get managers => $AppDatabaseManager(this);
  late final $UnitsTable units = $UnitsTable(this);
  late final $ProductsTable products = $ProductsTable(this);
  late final $BusinessProfilesTable businessProfiles = $BusinessProfilesTable(
    this,
  );
  late final $WarehousesTable warehouses = $WarehousesTable(this);
  late final $StockMovementsTable stockMovements = $StockMovementsTable(this);
  late final $StockBalancesTable stockBalances = $StockBalancesTable(this);
  late final $SalesTable sales = $SalesTable(this);
  late final $SaleLinesTable saleLines = $SaleLinesTable(this);
  late final $SalePaymentsTable salePayments = $SalePaymentsTable(this);
  late final $PosSettingsTable posSettings = $PosSettingsTable(this);
  @override
  Iterable<TableInfo<Table, Object?>> get allTables =>
      allSchemaEntities.whereType<TableInfo<Table, Object?>>();
  @override
  List<DatabaseSchemaEntity> get allSchemaEntities => [
    units,
    products,
    businessProfiles,
    warehouses,
    stockMovements,
    stockBalances,
    sales,
    saleLines,
    salePayments,
    posSettings,
  ];
}

typedef $$UnitsTableCreateCompanionBuilder = UnitsCompanion Function({
  required String id,
  required String companyId,
  required String code,
  required String name,
  Value<int> version,
  required DateTime updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});
typedef $$UnitsTableUpdateCompanionBuilder = UnitsCompanion Function({
  Value<String> id,
  Value<String> companyId,
  Value<String> code,
  Value<String> name,
  Value<int> version,
  Value<DateTime> updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});

final class $$UnitsTableReferences
    extends BaseReferences<_$AppDatabase, $UnitsTable, UnitRow> {
  $$UnitsTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static MultiTypedResultKey<$ProductsTable, List<ProductRow>>
  _productsRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.products,
    aliasName: 'units__id__products__unit_id',
  );

  $$ProductsTableProcessedTableManager get productsRefs {
    final manager = $$ProductsTableTableManager(
      $_db,
      $_db.products,
    ).filter((f) => f.unitId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_productsRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }
}

class $$UnitsTableFilterComposer extends Composer<_$AppDatabase, $UnitsTable> {
  $$UnitsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get code => $composableBuilder(
    column: $table.code,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnFilters(column),
  );

  Expression<bool> productsRefs(
    Expression<bool> Function($$ProductsTableFilterComposer f) f,
  ) {
    final $$ProductsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.unitId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableFilterComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$UnitsTableOrderingComposer
    extends Composer<_$AppDatabase, $UnitsTable> {
  $$UnitsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get code => $composableBuilder(
    column: $table.code,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$UnitsTableAnnotationComposer
    extends Composer<_$AppDatabase, $UnitsTable> {
  $$UnitsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get code =>
      $composableBuilder(column: $table.code, builder: (column) => column);

  GeneratedColumn<String> get name =>
      $composableBuilder(column: $table.name, builder: (column) => column);

  GeneratedColumn<int> get version =>
      $composableBuilder(column: $table.version, builder: (column) => column);

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);

  GeneratedColumn<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get deletedAt =>
      $composableBuilder(column: $table.deletedAt, builder: (column) => column);

  Expression<T> productsRefs<T extends Object>(
    Expression<T> Function($$ProductsTableAnnotationComposer a) f,
  ) {
    final $$ProductsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.unitId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableAnnotationComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$UnitsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $UnitsTable,
          UnitRow,
          $$UnitsTableFilterComposer,
          $$UnitsTableOrderingComposer,
          $$UnitsTableAnnotationComposer,
          $$UnitsTableCreateCompanionBuilder,
          $$UnitsTableUpdateCompanionBuilder,
          (UnitRow, $$UnitsTableReferences),
          UnitRow,
          PrefetchHooks Function({bool productsRefs})
        > {
  $$UnitsTableTableManager(_$AppDatabase db, $UnitsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$UnitsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$UnitsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$UnitsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> code = const Value.absent(),
                Value<String> name = const Value.absent(),
                Value<int> version = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => UnitsCompanion(
                id: id,
                companyId: companyId,
                code: code,
                name: name,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String code,
                required String name,
                Value<int> version = const Value.absent(),
                required DateTime updatedAt,
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => UnitsCompanion.insert(
                id: id,
                companyId: companyId,
                code: code,
                name: name,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$UnitsTable, UnitRow>(table),
                  $$UnitsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({productsRefs = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [if (productsRefs) db.products],
              addJoins: null,
              getPrefetchedDataCallback: (items) async {
                return [
                  if (productsRefs)
                    await $_getPrefetchedData<UnitRow, $UnitsTable, ProductRow>(
                      currentTable: table,
                      referencedTable: $$UnitsTableReferences
                          ._productsRefsTable(db),
                      managerFromTypedResult: (p0) =>
                          $$UnitsTableReferences(db, table, p0).productsRefs,
                      referencedItemsForCurrentItem: (item, referencedItems) =>
                          referencedItems.where((e) => e.unitId == item.id),
                      typedResults: items,
                    ),
                ];
              },
            );
          },
        ),
      );
}

typedef $$UnitsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $UnitsTable,
      UnitRow,
      $$UnitsTableFilterComposer,
      $$UnitsTableOrderingComposer,
      $$UnitsTableAnnotationComposer,
      $$UnitsTableCreateCompanionBuilder,
      $$UnitsTableUpdateCompanionBuilder,
      (UnitRow, $$UnitsTableReferences),
      UnitRow,
      PrefetchHooks Function({bool productsRefs})
    >;
typedef $$ProductsTableCreateCompanionBuilder = ProductsCompanion Function({
  required String id,
  required String companyId,
  required String sku,
  Value<String?> barcode,
  required String name,
  Value<String?> unitId,
  Value<int> salePriceE4,
  Value<int> costPriceE4,
  Value<int> version,
  required DateTime updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});
typedef $$ProductsTableUpdateCompanionBuilder = ProductsCompanion Function({
  Value<String> id,
  Value<String> companyId,
  Value<String> sku,
  Value<String?> barcode,
  Value<String> name,
  Value<String?> unitId,
  Value<int> salePriceE4,
  Value<int> costPriceE4,
  Value<int> version,
  Value<DateTime> updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});

final class $$ProductsTableReferences
    extends BaseReferences<_$AppDatabase, $ProductsTable, ProductRow> {
  $$ProductsTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static $UnitsTable _unitIdTable(_$AppDatabase db) =>
      db.units.createAlias('products__unit_id__units__id');

  $$UnitsTableProcessedTableManager? get unitId {
    final $_column = $_itemColumn<String>('unit_id');
    if ($_column == null) return null;
    final manager = $$UnitsTableTableManager(
      $_db,
      $_db.units,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_unitIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }

  static MultiTypedResultKey<$StockMovementsTable, List<StockMovementRow>>
  _stockMovementsRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.stockMovements,
    aliasName: 'products__id__stock_movements__product_id',
  );

  $$StockMovementsTableProcessedTableManager get stockMovementsRefs {
    final manager = $$StockMovementsTableTableManager(
      $_db,
      $_db.stockMovements,
    ).filter((f) => f.productId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_stockMovementsRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }

  static MultiTypedResultKey<$StockBalancesTable, List<StockBalanceRow>>
  _stockBalancesRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.stockBalances,
    aliasName: 'products__id__stock_balances__product_id',
  );

  $$StockBalancesTableProcessedTableManager get stockBalancesRefs {
    final manager = $$StockBalancesTableTableManager(
      $_db,
      $_db.stockBalances,
    ).filter((f) => f.productId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_stockBalancesRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }

  static MultiTypedResultKey<$SaleLinesTable, List<SaleLineRow>>
  _saleLinesRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.saleLines,
    aliasName: 'products__id__sale_lines__product_id',
  );

  $$SaleLinesTableProcessedTableManager get saleLinesRefs {
    final manager = $$SaleLinesTableTableManager(
      $_db,
      $_db.saleLines,
    ).filter((f) => f.productId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_saleLinesRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }
}

class $$ProductsTableFilterComposer
    extends Composer<_$AppDatabase, $ProductsTable> {
  $$ProductsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get sku => $composableBuilder(
    column: $table.sku,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get barcode => $composableBuilder(
    column: $table.barcode,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get salePriceE4 => $composableBuilder(
    column: $table.salePriceE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get costPriceE4 => $composableBuilder(
    column: $table.costPriceE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnFilters(column),
  );

  $$UnitsTableFilterComposer get unitId {
    final $$UnitsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.unitId,
      referencedTable: $db.units,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$UnitsTableFilterComposer(
            $db: $db,
            $table: $db.units,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  Expression<bool> stockMovementsRefs(
    Expression<bool> Function($$StockMovementsTableFilterComposer f) f,
  ) {
    final $$StockMovementsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockMovements,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockMovementsTableFilterComposer(
            $db: $db,
            $table: $db.stockMovements,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<bool> stockBalancesRefs(
    Expression<bool> Function($$StockBalancesTableFilterComposer f) f,
  ) {
    final $$StockBalancesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockBalances,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockBalancesTableFilterComposer(
            $db: $db,
            $table: $db.stockBalances,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<bool> saleLinesRefs(
    Expression<bool> Function($$SaleLinesTableFilterComposer f) f,
  ) {
    final $$SaleLinesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.saleLines,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SaleLinesTableFilterComposer(
            $db: $db,
            $table: $db.saleLines,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$ProductsTableOrderingComposer
    extends Composer<_$AppDatabase, $ProductsTable> {
  $$ProductsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get sku => $composableBuilder(
    column: $table.sku,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get barcode => $composableBuilder(
    column: $table.barcode,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get salePriceE4 => $composableBuilder(
    column: $table.salePriceE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get costPriceE4 => $composableBuilder(
    column: $table.costPriceE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnOrderings(column),
  );

  $$UnitsTableOrderingComposer get unitId {
    final $$UnitsTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.unitId,
      referencedTable: $db.units,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$UnitsTableOrderingComposer(
            $db: $db,
            $table: $db.units,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$ProductsTableAnnotationComposer
    extends Composer<_$AppDatabase, $ProductsTable> {
  $$ProductsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get sku =>
      $composableBuilder(column: $table.sku, builder: (column) => column);

  GeneratedColumn<String> get barcode =>
      $composableBuilder(column: $table.barcode, builder: (column) => column);

  GeneratedColumn<String> get name =>
      $composableBuilder(column: $table.name, builder: (column) => column);

  GeneratedColumn<int> get salePriceE4 => $composableBuilder(
    column: $table.salePriceE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get costPriceE4 => $composableBuilder(
    column: $table.costPriceE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get version =>
      $composableBuilder(column: $table.version, builder: (column) => column);

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);

  GeneratedColumn<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get deletedAt =>
      $composableBuilder(column: $table.deletedAt, builder: (column) => column);

  $$UnitsTableAnnotationComposer get unitId {
    final $$UnitsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.unitId,
      referencedTable: $db.units,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$UnitsTableAnnotationComposer(
            $db: $db,
            $table: $db.units,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  Expression<T> stockMovementsRefs<T extends Object>(
    Expression<T> Function($$StockMovementsTableAnnotationComposer a) f,
  ) {
    final $$StockMovementsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockMovements,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockMovementsTableAnnotationComposer(
            $db: $db,
            $table: $db.stockMovements,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<T> stockBalancesRefs<T extends Object>(
    Expression<T> Function($$StockBalancesTableAnnotationComposer a) f,
  ) {
    final $$StockBalancesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockBalances,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockBalancesTableAnnotationComposer(
            $db: $db,
            $table: $db.stockBalances,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<T> saleLinesRefs<T extends Object>(
    Expression<T> Function($$SaleLinesTableAnnotationComposer a) f,
  ) {
    final $$SaleLinesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.saleLines,
      getReferencedColumn: (t) => t.productId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SaleLinesTableAnnotationComposer(
            $db: $db,
            $table: $db.saleLines,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$ProductsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $ProductsTable,
          ProductRow,
          $$ProductsTableFilterComposer,
          $$ProductsTableOrderingComposer,
          $$ProductsTableAnnotationComposer,
          $$ProductsTableCreateCompanionBuilder,
          $$ProductsTableUpdateCompanionBuilder,
          (ProductRow, $$ProductsTableReferences),
          ProductRow,
          PrefetchHooks Function({
            bool unitId,
            bool stockMovementsRefs,
            bool stockBalancesRefs,
            bool saleLinesRefs,
          })
        > {
  $$ProductsTableTableManager(_$AppDatabase db, $ProductsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$ProductsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$ProductsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$ProductsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> sku = const Value.absent(),
                Value<String?> barcode = const Value.absent(),
                Value<String> name = const Value.absent(),
                Value<String?> unitId = const Value.absent(),
                Value<int> salePriceE4 = const Value.absent(),
                Value<int> costPriceE4 = const Value.absent(),
                Value<int> version = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => ProductsCompanion(
                id: id,
                companyId: companyId,
                sku: sku,
                barcode: barcode,
                name: name,
                unitId: unitId,
                salePriceE4: salePriceE4,
                costPriceE4: costPriceE4,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String sku,
                Value<String?> barcode = const Value.absent(),
                required String name,
                Value<String?> unitId = const Value.absent(),
                Value<int> salePriceE4 = const Value.absent(),
                Value<int> costPriceE4 = const Value.absent(),
                Value<int> version = const Value.absent(),
                required DateTime updatedAt,
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => ProductsCompanion.insert(
                id: id,
                companyId: companyId,
                sku: sku,
                barcode: barcode,
                name: name,
                unitId: unitId,
                salePriceE4: salePriceE4,
                costPriceE4: costPriceE4,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$ProductsTable, ProductRow>(table),
                  $$ProductsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback:
              ({
                unitId = false,
                stockMovementsRefs = false,
                stockBalancesRefs = false,
                saleLinesRefs = false,
              }) {
                return PrefetchHooks(
                  db: db,
                  explicitlyWatchedTables: [
                    if (stockMovementsRefs) db.stockMovements,
                    if (stockBalancesRefs) db.stockBalances,
                    if (saleLinesRefs) db.saleLines,
                  ],
                  addJoins:
                      <
                        T extends TableManagerState<
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic
                        >
                      >(state) {
                        if (unitId) {
                          state = state.withJoin(
                            currentTable: table,
                            currentColumn: table.unitId,
                            referencedTable: $$ProductsTableReferences
                                ._unitIdTable(db),
                            referencedColumn: $$ProductsTableReferences
                                ._unitIdTable(db)
                                .id,
                          ) as T;
                        }

                        return state;
                      },
                  getPrefetchedDataCallback: (items) async {
                    return [
                      if (stockMovementsRefs)
                        await $_getPrefetchedData<
                          ProductRow,
                          $ProductsTable,
                          StockMovementRow
                        >(
                          currentTable: table,
                          referencedTable: $$ProductsTableReferences
                              ._stockMovementsRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$ProductsTableReferences(
                                db,
                                table,
                                p0,
                              ).stockMovementsRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.productId == item.id,
                              ),
                          typedResults: items,
                        ),
                      if (stockBalancesRefs)
                        await $_getPrefetchedData<
                          ProductRow,
                          $ProductsTable,
                          StockBalanceRow
                        >(
                          currentTable: table,
                          referencedTable: $$ProductsTableReferences
                              ._stockBalancesRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$ProductsTableReferences(
                                db,
                                table,
                                p0,
                              ).stockBalancesRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.productId == item.id,
                              ),
                          typedResults: items,
                        ),
                      if (saleLinesRefs)
                        await $_getPrefetchedData<
                          ProductRow,
                          $ProductsTable,
                          SaleLineRow
                        >(
                          currentTable: table,
                          referencedTable: $$ProductsTableReferences
                              ._saleLinesRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$ProductsTableReferences(
                                db,
                                table,
                                p0,
                              ).saleLinesRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.productId == item.id,
                              ),
                          typedResults: items,
                        ),
                    ];
                  },
                );
              },
        ),
      );
}

typedef $$ProductsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $ProductsTable,
      ProductRow,
      $$ProductsTableFilterComposer,
      $$ProductsTableOrderingComposer,
      $$ProductsTableAnnotationComposer,
      $$ProductsTableCreateCompanionBuilder,
      $$ProductsTableUpdateCompanionBuilder,
      (ProductRow, $$ProductsTableReferences),
      ProductRow,
      PrefetchHooks Function({
        bool unitId,
        bool stockMovementsRefs,
        bool stockBalancesRefs,
        bool saleLinesRefs,
      })
    >;
typedef $$BusinessProfilesTableCreateCompanionBuilder =
    BusinessProfilesCompanion Function({
      required String id,
      required String companyId,
      required String businessName,
      required String industry,
      required String currency,
      required String ownerName,
      required DateTime createdAt,
      required DateTime updatedAt,
      Value<int> rowid,
    });
typedef $$BusinessProfilesTableUpdateCompanionBuilder =
    BusinessProfilesCompanion Function({
      Value<String> id,
      Value<String> companyId,
      Value<String> businessName,
      Value<String> industry,
      Value<String> currency,
      Value<String> ownerName,
      Value<DateTime> createdAt,
      Value<DateTime> updatedAt,
      Value<int> rowid,
    });

class $$BusinessProfilesTableFilterComposer
    extends Composer<_$AppDatabase, $BusinessProfilesTable> {
  $$BusinessProfilesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get businessName => $composableBuilder(
    column: $table.businessName,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get industry => $composableBuilder(
    column: $table.industry,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get currency => $composableBuilder(
    column: $table.currency,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get ownerName => $composableBuilder(
    column: $table.ownerName,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );
}

class $$BusinessProfilesTableOrderingComposer
    extends Composer<_$AppDatabase, $BusinessProfilesTable> {
  $$BusinessProfilesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get businessName => $composableBuilder(
    column: $table.businessName,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get industry => $composableBuilder(
    column: $table.industry,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get currency => $composableBuilder(
    column: $table.currency,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get ownerName => $composableBuilder(
    column: $table.ownerName,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$BusinessProfilesTableAnnotationComposer
    extends Composer<_$AppDatabase, $BusinessProfilesTable> {
  $$BusinessProfilesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get businessName => $composableBuilder(
    column: $table.businessName,
    builder: (column) => column,
  );

  GeneratedColumn<String> get industry =>
      $composableBuilder(column: $table.industry, builder: (column) => column);

  GeneratedColumn<String> get currency =>
      $composableBuilder(column: $table.currency, builder: (column) => column);

  GeneratedColumn<String> get ownerName =>
      $composableBuilder(column: $table.ownerName, builder: (column) => column);

  GeneratedColumn<DateTime> get createdAt =>
      $composableBuilder(column: $table.createdAt, builder: (column) => column);

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);
}

class $$BusinessProfilesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $BusinessProfilesTable,
          BusinessProfileRow,
          $$BusinessProfilesTableFilterComposer,
          $$BusinessProfilesTableOrderingComposer,
          $$BusinessProfilesTableAnnotationComposer,
          $$BusinessProfilesTableCreateCompanionBuilder,
          $$BusinessProfilesTableUpdateCompanionBuilder,
          (
            BusinessProfileRow,
            BaseReferences<
              _$AppDatabase,
              $BusinessProfilesTable,
              BusinessProfileRow
            >,
          ),
          BusinessProfileRow,
          PrefetchHooks Function()
        > {
  $$BusinessProfilesTableTableManager(
    _$AppDatabase db,
    $BusinessProfilesTable table,
  ) : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$BusinessProfilesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$BusinessProfilesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$BusinessProfilesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> businessName = const Value.absent(),
                Value<String> industry = const Value.absent(),
                Value<String> currency = const Value.absent(),
                Value<String> ownerName = const Value.absent(),
                Value<DateTime> createdAt = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => BusinessProfilesCompanion(
                id: id,
                companyId: companyId,
                businessName: businessName,
                industry: industry,
                currency: currency,
                ownerName: ownerName,
                createdAt: createdAt,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String businessName,
                required String industry,
                required String currency,
                required String ownerName,
                required DateTime createdAt,
                required DateTime updatedAt,
                Value<int> rowid = const Value.absent(),
              }) => BusinessProfilesCompanion.insert(
                id: id,
                companyId: companyId,
                businessName: businessName,
                industry: industry,
                currency: currency,
                ownerName: ownerName,
                createdAt: createdAt,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$BusinessProfilesTable, BusinessProfileRow>(
                    table,
                  ),
                  BaseReferences<
                    _$AppDatabase,
                    $BusinessProfilesTable,
                    BusinessProfileRow
                  >(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$BusinessProfilesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $BusinessProfilesTable,
      BusinessProfileRow,
      $$BusinessProfilesTableFilterComposer,
      $$BusinessProfilesTableOrderingComposer,
      $$BusinessProfilesTableAnnotationComposer,
      $$BusinessProfilesTableCreateCompanionBuilder,
      $$BusinessProfilesTableUpdateCompanionBuilder,
      (
        BusinessProfileRow,
        BaseReferences<
          _$AppDatabase,
          $BusinessProfilesTable,
          BusinessProfileRow
        >,
      ),
      BusinessProfileRow,
      PrefetchHooks Function()
    >;
typedef $$WarehousesTableCreateCompanionBuilder = WarehousesCompanion Function({
  required String id,
  required String companyId,
  required String code,
  required String name,
  Value<bool> isDefault,
  Value<int> version,
  required DateTime updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});
typedef $$WarehousesTableUpdateCompanionBuilder = WarehousesCompanion Function({
  Value<String> id,
  Value<String> companyId,
  Value<String> code,
  Value<String> name,
  Value<bool> isDefault,
  Value<int> version,
  Value<DateTime> updatedAt,
  Value<String?> originDeviceId,
  Value<DateTime?> deletedAt,
  Value<int> rowid,
});

final class $$WarehousesTableReferences
    extends BaseReferences<_$AppDatabase, $WarehousesTable, WarehouseRow> {
  $$WarehousesTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static MultiTypedResultKey<$StockMovementsTable, List<StockMovementRow>>
  _stockMovementsRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.stockMovements,
    aliasName: 'warehouses__id__stock_movements__warehouse_id',
  );

  $$StockMovementsTableProcessedTableManager get stockMovementsRefs {
    final manager = $$StockMovementsTableTableManager(
      $_db,
      $_db.stockMovements,
    ).filter((f) => f.warehouseId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_stockMovementsRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }

  static MultiTypedResultKey<$StockBalancesTable, List<StockBalanceRow>>
  _stockBalancesRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.stockBalances,
    aliasName: 'warehouses__id__stock_balances__warehouse_id',
  );

  $$StockBalancesTableProcessedTableManager get stockBalancesRefs {
    final manager = $$StockBalancesTableTableManager(
      $_db,
      $_db.stockBalances,
    ).filter((f) => f.warehouseId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_stockBalancesRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }

  static MultiTypedResultKey<$SalesTable, List<SaleRow>> _salesRefsTable(
    _$AppDatabase db,
  ) => MultiTypedResultKey.fromTable(
    db.sales,
    aliasName: 'warehouses__id__sales__warehouse_id',
  );

  $$SalesTableProcessedTableManager get salesRefs {
    final manager = $$SalesTableTableManager(
      $_db,
      $_db.sales,
    ).filter((f) => f.warehouseId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_salesRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }
}

class $$WarehousesTableFilterComposer
    extends Composer<_$AppDatabase, $WarehousesTable> {
  $$WarehousesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get code => $composableBuilder(
    column: $table.code,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<bool> get isDefault => $composableBuilder(
    column: $table.isDefault,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnFilters(column),
  );

  Expression<bool> stockMovementsRefs(
    Expression<bool> Function($$StockMovementsTableFilterComposer f) f,
  ) {
    final $$StockMovementsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockMovements,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockMovementsTableFilterComposer(
            $db: $db,
            $table: $db.stockMovements,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<bool> stockBalancesRefs(
    Expression<bool> Function($$StockBalancesTableFilterComposer f) f,
  ) {
    final $$StockBalancesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockBalances,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockBalancesTableFilterComposer(
            $db: $db,
            $table: $db.stockBalances,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<bool> salesRefs(
    Expression<bool> Function($$SalesTableFilterComposer f) f,
  ) {
    final $$SalesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableFilterComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$WarehousesTableOrderingComposer
    extends Composer<_$AppDatabase, $WarehousesTable> {
  $$WarehousesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get code => $composableBuilder(
    column: $table.code,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get name => $composableBuilder(
    column: $table.name,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<bool> get isDefault => $composableBuilder(
    column: $table.isDefault,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get version => $composableBuilder(
    column: $table.version,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get deletedAt => $composableBuilder(
    column: $table.deletedAt,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$WarehousesTableAnnotationComposer
    extends Composer<_$AppDatabase, $WarehousesTable> {
  $$WarehousesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get code =>
      $composableBuilder(column: $table.code, builder: (column) => column);

  GeneratedColumn<String> get name =>
      $composableBuilder(column: $table.name, builder: (column) => column);

  GeneratedColumn<bool> get isDefault =>
      $composableBuilder(column: $table.isDefault, builder: (column) => column);

  GeneratedColumn<int> get version =>
      $composableBuilder(column: $table.version, builder: (column) => column);

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);

  GeneratedColumn<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get deletedAt =>
      $composableBuilder(column: $table.deletedAt, builder: (column) => column);

  Expression<T> stockMovementsRefs<T extends Object>(
    Expression<T> Function($$StockMovementsTableAnnotationComposer a) f,
  ) {
    final $$StockMovementsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockMovements,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockMovementsTableAnnotationComposer(
            $db: $db,
            $table: $db.stockMovements,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<T> stockBalancesRefs<T extends Object>(
    Expression<T> Function($$StockBalancesTableAnnotationComposer a) f,
  ) {
    final $$StockBalancesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.stockBalances,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$StockBalancesTableAnnotationComposer(
            $db: $db,
            $table: $db.stockBalances,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<T> salesRefs<T extends Object>(
    Expression<T> Function($$SalesTableAnnotationComposer a) f,
  ) {
    final $$SalesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.warehouseId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableAnnotationComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$WarehousesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $WarehousesTable,
          WarehouseRow,
          $$WarehousesTableFilterComposer,
          $$WarehousesTableOrderingComposer,
          $$WarehousesTableAnnotationComposer,
          $$WarehousesTableCreateCompanionBuilder,
          $$WarehousesTableUpdateCompanionBuilder,
          (WarehouseRow, $$WarehousesTableReferences),
          WarehouseRow,
          PrefetchHooks Function({
            bool stockMovementsRefs,
            bool stockBalancesRefs,
            bool salesRefs,
          })
        > {
  $$WarehousesTableTableManager(_$AppDatabase db, $WarehousesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$WarehousesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$WarehousesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$WarehousesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> code = const Value.absent(),
                Value<String> name = const Value.absent(),
                Value<bool> isDefault = const Value.absent(),
                Value<int> version = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => WarehousesCompanion(
                id: id,
                companyId: companyId,
                code: code,
                name: name,
                isDefault: isDefault,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String code,
                required String name,
                Value<bool> isDefault = const Value.absent(),
                Value<int> version = const Value.absent(),
                required DateTime updatedAt,
                Value<String?> originDeviceId = const Value.absent(),
                Value<DateTime?> deletedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => WarehousesCompanion.insert(
                id: id,
                companyId: companyId,
                code: code,
                name: name,
                isDefault: isDefault,
                version: version,
                updatedAt: updatedAt,
                originDeviceId: originDeviceId,
                deletedAt: deletedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$WarehousesTable, WarehouseRow>(table),
                  $$WarehousesTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback:
              ({
                stockMovementsRefs = false,
                stockBalancesRefs = false,
                salesRefs = false,
              }) {
                return PrefetchHooks(
                  db: db,
                  explicitlyWatchedTables: [
                    if (stockMovementsRefs) db.stockMovements,
                    if (stockBalancesRefs) db.stockBalances,
                    if (salesRefs) db.sales,
                  ],
                  addJoins: null,
                  getPrefetchedDataCallback: (items) async {
                    return [
                      if (stockMovementsRefs)
                        await $_getPrefetchedData<
                          WarehouseRow,
                          $WarehousesTable,
                          StockMovementRow
                        >(
                          currentTable: table,
                          referencedTable: $$WarehousesTableReferences
                              ._stockMovementsRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$WarehousesTableReferences(
                                db,
                                table,
                                p0,
                              ).stockMovementsRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.warehouseId == item.id,
                              ),
                          typedResults: items,
                        ),
                      if (stockBalancesRefs)
                        await $_getPrefetchedData<
                          WarehouseRow,
                          $WarehousesTable,
                          StockBalanceRow
                        >(
                          currentTable: table,
                          referencedTable: $$WarehousesTableReferences
                              ._stockBalancesRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$WarehousesTableReferences(
                                db,
                                table,
                                p0,
                              ).stockBalancesRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.warehouseId == item.id,
                              ),
                          typedResults: items,
                        ),
                      if (salesRefs)
                        await $_getPrefetchedData<
                          WarehouseRow,
                          $WarehousesTable,
                          SaleRow
                        >(
                          currentTable: table,
                          referencedTable: $$WarehousesTableReferences
                              ._salesRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$WarehousesTableReferences(
                                db,
                                table,
                                p0,
                              ).salesRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.warehouseId == item.id,
                              ),
                          typedResults: items,
                        ),
                    ];
                  },
                );
              },
        ),
      );
}

typedef $$WarehousesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $WarehousesTable,
      WarehouseRow,
      $$WarehousesTableFilterComposer,
      $$WarehousesTableOrderingComposer,
      $$WarehousesTableAnnotationComposer,
      $$WarehousesTableCreateCompanionBuilder,
      $$WarehousesTableUpdateCompanionBuilder,
      (WarehouseRow, $$WarehousesTableReferences),
      WarehouseRow,
      PrefetchHooks Function({
        bool stockMovementsRefs,
        bool stockBalancesRefs,
        bool salesRefs,
      })
    >;
typedef $$StockMovementsTableCreateCompanionBuilder =
    StockMovementsCompanion Function({
      required String id,
      required String companyId,
      required String productId,
      required String warehouseId,
      required String movementType,
      required int qtyE4,
      Value<int?> unitCostE4,
      Value<String?> reason,
      Value<String?> sourceType,
      Value<String?> sourceId,
      Value<String?> reversesMovementId,
      required String clientRequestId,
      required DateTime occurredAt,
      required DateTime createdAt,
      required String createdBy,
      Value<String?> originDeviceId,
      Value<int> rowid,
    });
typedef $$StockMovementsTableUpdateCompanionBuilder =
    StockMovementsCompanion Function({
      Value<String> id,
      Value<String> companyId,
      Value<String> productId,
      Value<String> warehouseId,
      Value<String> movementType,
      Value<int> qtyE4,
      Value<int?> unitCostE4,
      Value<String?> reason,
      Value<String?> sourceType,
      Value<String?> sourceId,
      Value<String?> reversesMovementId,
      Value<String> clientRequestId,
      Value<DateTime> occurredAt,
      Value<DateTime> createdAt,
      Value<String> createdBy,
      Value<String?> originDeviceId,
      Value<int> rowid,
    });

final class $$StockMovementsTableReferences
    extends
        BaseReferences<_$AppDatabase, $StockMovementsTable, StockMovementRow> {
  $$StockMovementsTableReferences(
    super.$_db,
    super.$_table,
    super.$_typedResult,
  );

  static $ProductsTable _productIdTable(_$AppDatabase db) =>
      db.products.createAlias('stock_movements__product_id__products__id');

  $$ProductsTableProcessedTableManager get productId {
    final $_column = $_itemColumn<String>('product_id')!;

    final manager = $$ProductsTableTableManager(
      $_db,
      $_db.products,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_productIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }

  static $WarehousesTable _warehouseIdTable(_$AppDatabase db) => db.warehouses
      .createAlias('stock_movements__warehouse_id__warehouses__id');

  $$WarehousesTableProcessedTableManager get warehouseId {
    final $_column = $_itemColumn<String>('warehouse_id')!;

    final manager = $$WarehousesTableTableManager(
      $_db,
      $_db.warehouses,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_warehouseIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }
}

class $$StockMovementsTableFilterComposer
    extends Composer<_$AppDatabase, $StockMovementsTable> {
  $$StockMovementsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get movementType => $composableBuilder(
    column: $table.movementType,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get qtyE4 => $composableBuilder(
    column: $table.qtyE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get unitCostE4 => $composableBuilder(
    column: $table.unitCostE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get reason => $composableBuilder(
    column: $table.reason,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get sourceType => $composableBuilder(
    column: $table.sourceType,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get sourceId => $composableBuilder(
    column: $table.sourceId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get reversesMovementId => $composableBuilder(
    column: $table.reversesMovementId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get createdBy => $composableBuilder(
    column: $table.createdBy,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnFilters(column),
  );

  $$ProductsTableFilterComposer get productId {
    final $$ProductsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableFilterComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableFilterComposer get warehouseId {
    final $$WarehousesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableFilterComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockMovementsTableOrderingComposer
    extends Composer<_$AppDatabase, $StockMovementsTable> {
  $$StockMovementsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get movementType => $composableBuilder(
    column: $table.movementType,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get qtyE4 => $composableBuilder(
    column: $table.qtyE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get unitCostE4 => $composableBuilder(
    column: $table.unitCostE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get reason => $composableBuilder(
    column: $table.reason,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get sourceType => $composableBuilder(
    column: $table.sourceType,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get sourceId => $composableBuilder(
    column: $table.sourceId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get reversesMovementId => $composableBuilder(
    column: $table.reversesMovementId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get createdBy => $composableBuilder(
    column: $table.createdBy,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnOrderings(column),
  );

  $$ProductsTableOrderingComposer get productId {
    final $$ProductsTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableOrderingComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableOrderingComposer get warehouseId {
    final $$WarehousesTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableOrderingComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockMovementsTableAnnotationComposer
    extends Composer<_$AppDatabase, $StockMovementsTable> {
  $$StockMovementsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get movementType => $composableBuilder(
    column: $table.movementType,
    builder: (column) => column,
  );

  GeneratedColumn<int> get qtyE4 =>
      $composableBuilder(column: $table.qtyE4, builder: (column) => column);

  GeneratedColumn<int> get unitCostE4 => $composableBuilder(
    column: $table.unitCostE4,
    builder: (column) => column,
  );

  GeneratedColumn<String> get reason =>
      $composableBuilder(column: $table.reason, builder: (column) => column);

  GeneratedColumn<String> get sourceType => $composableBuilder(
    column: $table.sourceType,
    builder: (column) => column,
  );

  GeneratedColumn<String> get sourceId =>
      $composableBuilder(column: $table.sourceId, builder: (column) => column);

  GeneratedColumn<String> get reversesMovementId => $composableBuilder(
    column: $table.reversesMovementId,
    builder: (column) => column,
  );

  GeneratedColumn<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get occurredAt => $composableBuilder(
    column: $table.occurredAt,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get createdAt =>
      $composableBuilder(column: $table.createdAt, builder: (column) => column);

  GeneratedColumn<String> get createdBy =>
      $composableBuilder(column: $table.createdBy, builder: (column) => column);

  GeneratedColumn<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => column,
  );

  $$ProductsTableAnnotationComposer get productId {
    final $$ProductsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableAnnotationComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableAnnotationComposer get warehouseId {
    final $$WarehousesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableAnnotationComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockMovementsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $StockMovementsTable,
          StockMovementRow,
          $$StockMovementsTableFilterComposer,
          $$StockMovementsTableOrderingComposer,
          $$StockMovementsTableAnnotationComposer,
          $$StockMovementsTableCreateCompanionBuilder,
          $$StockMovementsTableUpdateCompanionBuilder,
          (StockMovementRow, $$StockMovementsTableReferences),
          StockMovementRow,
          PrefetchHooks Function({bool productId, bool warehouseId})
        > {
  $$StockMovementsTableTableManager(
    _$AppDatabase db,
    $StockMovementsTable table,
  ) : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$StockMovementsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$StockMovementsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$StockMovementsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> productId = const Value.absent(),
                Value<String> warehouseId = const Value.absent(),
                Value<String> movementType = const Value.absent(),
                Value<int> qtyE4 = const Value.absent(),
                Value<int?> unitCostE4 = const Value.absent(),
                Value<String?> reason = const Value.absent(),
                Value<String?> sourceType = const Value.absent(),
                Value<String?> sourceId = const Value.absent(),
                Value<String?> reversesMovementId = const Value.absent(),
                Value<String> clientRequestId = const Value.absent(),
                Value<DateTime> occurredAt = const Value.absent(),
                Value<DateTime> createdAt = const Value.absent(),
                Value<String> createdBy = const Value.absent(),
                Value<String?> originDeviceId = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => StockMovementsCompanion(
                id: id,
                companyId: companyId,
                productId: productId,
                warehouseId: warehouseId,
                movementType: movementType,
                qtyE4: qtyE4,
                unitCostE4: unitCostE4,
                reason: reason,
                sourceType: sourceType,
                sourceId: sourceId,
                reversesMovementId: reversesMovementId,
                clientRequestId: clientRequestId,
                occurredAt: occurredAt,
                createdAt: createdAt,
                createdBy: createdBy,
                originDeviceId: originDeviceId,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String productId,
                required String warehouseId,
                required String movementType,
                required int qtyE4,
                Value<int?> unitCostE4 = const Value.absent(),
                Value<String?> reason = const Value.absent(),
                Value<String?> sourceType = const Value.absent(),
                Value<String?> sourceId = const Value.absent(),
                Value<String?> reversesMovementId = const Value.absent(),
                required String clientRequestId,
                required DateTime occurredAt,
                required DateTime createdAt,
                required String createdBy,
                Value<String?> originDeviceId = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => StockMovementsCompanion.insert(
                id: id,
                companyId: companyId,
                productId: productId,
                warehouseId: warehouseId,
                movementType: movementType,
                qtyE4: qtyE4,
                unitCostE4: unitCostE4,
                reason: reason,
                sourceType: sourceType,
                sourceId: sourceId,
                reversesMovementId: reversesMovementId,
                clientRequestId: clientRequestId,
                occurredAt: occurredAt,
                createdAt: createdAt,
                createdBy: createdBy,
                originDeviceId: originDeviceId,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$StockMovementsTable, StockMovementRow>(table),
                  $$StockMovementsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({productId = false, warehouseId = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [],
              addJoins:
                  <
                    T extends TableManagerState<
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic
                    >
                  >(state) {
                    if (productId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.productId,
                        referencedTable: $$StockMovementsTableReferences
                            ._productIdTable(db),
                        referencedColumn: $$StockMovementsTableReferences
                            ._productIdTable(db)
                            .id,
                      ) as T;
                    }
                    if (warehouseId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.warehouseId,
                        referencedTable: $$StockMovementsTableReferences
                            ._warehouseIdTable(db),
                        referencedColumn: $$StockMovementsTableReferences
                            ._warehouseIdTable(db)
                            .id,
                      ) as T;
                    }

                    return state;
                  },
              getPrefetchedDataCallback: (items) async {
                return [];
              },
            );
          },
        ),
      );
}

typedef $$StockMovementsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $StockMovementsTable,
      StockMovementRow,
      $$StockMovementsTableFilterComposer,
      $$StockMovementsTableOrderingComposer,
      $$StockMovementsTableAnnotationComposer,
      $$StockMovementsTableCreateCompanionBuilder,
      $$StockMovementsTableUpdateCompanionBuilder,
      (StockMovementRow, $$StockMovementsTableReferences),
      StockMovementRow,
      PrefetchHooks Function({bool productId, bool warehouseId})
    >;
typedef $$StockBalancesTableCreateCompanionBuilder =
    StockBalancesCompanion Function({
      required String companyId,
      required String productId,
      required String warehouseId,
      required int qtyE4,
      required DateTime updatedAt,
      Value<int> rowid,
    });
typedef $$StockBalancesTableUpdateCompanionBuilder =
    StockBalancesCompanion Function({
      Value<String> companyId,
      Value<String> productId,
      Value<String> warehouseId,
      Value<int> qtyE4,
      Value<DateTime> updatedAt,
      Value<int> rowid,
    });

final class $$StockBalancesTableReferences
    extends
        BaseReferences<_$AppDatabase, $StockBalancesTable, StockBalanceRow> {
  $$StockBalancesTableReferences(
    super.$_db,
    super.$_table,
    super.$_typedResult,
  );

  static $ProductsTable _productIdTable(_$AppDatabase db) =>
      db.products.createAlias('stock_balances__product_id__products__id');

  $$ProductsTableProcessedTableManager get productId {
    final $_column = $_itemColumn<String>('product_id')!;

    final manager = $$ProductsTableTableManager(
      $_db,
      $_db.products,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_productIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }

  static $WarehousesTable _warehouseIdTable(_$AppDatabase db) =>
      db.warehouses.createAlias('stock_balances__warehouse_id__warehouses__id');

  $$WarehousesTableProcessedTableManager get warehouseId {
    final $_column = $_itemColumn<String>('warehouse_id')!;

    final manager = $$WarehousesTableTableManager(
      $_db,
      $_db.warehouses,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_warehouseIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }
}

class $$StockBalancesTableFilterComposer
    extends Composer<_$AppDatabase, $StockBalancesTable> {
  $$StockBalancesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get qtyE4 => $composableBuilder(
    column: $table.qtyE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );

  $$ProductsTableFilterComposer get productId {
    final $$ProductsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableFilterComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableFilterComposer get warehouseId {
    final $$WarehousesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableFilterComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockBalancesTableOrderingComposer
    extends Composer<_$AppDatabase, $StockBalancesTable> {
  $$StockBalancesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get qtyE4 => $composableBuilder(
    column: $table.qtyE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );

  $$ProductsTableOrderingComposer get productId {
    final $$ProductsTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableOrderingComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableOrderingComposer get warehouseId {
    final $$WarehousesTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableOrderingComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockBalancesTableAnnotationComposer
    extends Composer<_$AppDatabase, $StockBalancesTable> {
  $$StockBalancesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<int> get qtyE4 =>
      $composableBuilder(column: $table.qtyE4, builder: (column) => column);

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);

  $$ProductsTableAnnotationComposer get productId {
    final $$ProductsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableAnnotationComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$WarehousesTableAnnotationComposer get warehouseId {
    final $$WarehousesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableAnnotationComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$StockBalancesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $StockBalancesTable,
          StockBalanceRow,
          $$StockBalancesTableFilterComposer,
          $$StockBalancesTableOrderingComposer,
          $$StockBalancesTableAnnotationComposer,
          $$StockBalancesTableCreateCompanionBuilder,
          $$StockBalancesTableUpdateCompanionBuilder,
          (StockBalanceRow, $$StockBalancesTableReferences),
          StockBalanceRow,
          PrefetchHooks Function({bool productId, bool warehouseId})
        > {
  $$StockBalancesTableTableManager(_$AppDatabase db, $StockBalancesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$StockBalancesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$StockBalancesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$StockBalancesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> companyId = const Value.absent(),
                Value<String> productId = const Value.absent(),
                Value<String> warehouseId = const Value.absent(),
                Value<int> qtyE4 = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => StockBalancesCompanion(
                companyId: companyId,
                productId: productId,
                warehouseId: warehouseId,
                qtyE4: qtyE4,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String companyId,
                required String productId,
                required String warehouseId,
                required int qtyE4,
                required DateTime updatedAt,
                Value<int> rowid = const Value.absent(),
              }) => StockBalancesCompanion.insert(
                companyId: companyId,
                productId: productId,
                warehouseId: warehouseId,
                qtyE4: qtyE4,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$StockBalancesTable, StockBalanceRow>(table),
                  $$StockBalancesTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({productId = false, warehouseId = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [],
              addJoins:
                  <
                    T extends TableManagerState<
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic
                    >
                  >(state) {
                    if (productId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.productId,
                        referencedTable: $$StockBalancesTableReferences
                            ._productIdTable(db),
                        referencedColumn: $$StockBalancesTableReferences
                            ._productIdTable(db)
                            .id,
                      ) as T;
                    }
                    if (warehouseId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.warehouseId,
                        referencedTable: $$StockBalancesTableReferences
                            ._warehouseIdTable(db),
                        referencedColumn: $$StockBalancesTableReferences
                            ._warehouseIdTable(db)
                            .id,
                      ) as T;
                    }

                    return state;
                  },
              getPrefetchedDataCallback: (items) async {
                return [];
              },
            );
          },
        ),
      );
}

typedef $$StockBalancesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $StockBalancesTable,
      StockBalanceRow,
      $$StockBalancesTableFilterComposer,
      $$StockBalancesTableOrderingComposer,
      $$StockBalancesTableAnnotationComposer,
      $$StockBalancesTableCreateCompanionBuilder,
      $$StockBalancesTableUpdateCompanionBuilder,
      (StockBalanceRow, $$StockBalancesTableReferences),
      StockBalanceRow,
      PrefetchHooks Function({bool productId, bool warehouseId})
    >;
typedef $$SalesTableCreateCompanionBuilder = SalesCompanion Function({
  required String id,
  required String companyId,
  required String receiptNumber,
  required String clientRequestId,
  required String warehouseId,
  required int subtotalE4,
  Value<int> discountE4,
  Value<int> taxE4,
  required int totalE4,
  required int amountPaidE4,
  Value<String> status,
  Value<int> changeE4,
  Value<int> taxRateBp,
  Value<String?> discountReason,
  required String paymentMethod,
  required DateTime createdAt,
  required String createdBy,
  Value<String?> originDeviceId,
  Value<int> rowid,
});
typedef $$SalesTableUpdateCompanionBuilder = SalesCompanion Function({
  Value<String> id,
  Value<String> companyId,
  Value<String> receiptNumber,
  Value<String> clientRequestId,
  Value<String> warehouseId,
  Value<int> subtotalE4,
  Value<int> discountE4,
  Value<int> taxE4,
  Value<int> totalE4,
  Value<int> amountPaidE4,
  Value<String> status,
  Value<int> changeE4,
  Value<int> taxRateBp,
  Value<String?> discountReason,
  Value<String> paymentMethod,
  Value<DateTime> createdAt,
  Value<String> createdBy,
  Value<String?> originDeviceId,
  Value<int> rowid,
});

final class $$SalesTableReferences
    extends BaseReferences<_$AppDatabase, $SalesTable, SaleRow> {
  $$SalesTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static $WarehousesTable _warehouseIdTable(_$AppDatabase db) =>
      db.warehouses.createAlias('sales__warehouse_id__warehouses__id');

  $$WarehousesTableProcessedTableManager get warehouseId {
    final $_column = $_itemColumn<String>('warehouse_id')!;

    final manager = $$WarehousesTableTableManager(
      $_db,
      $_db.warehouses,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_warehouseIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }

  static MultiTypedResultKey<$SaleLinesTable, List<SaleLineRow>>
  _saleLinesRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.saleLines,
    aliasName: 'sales__id__sale_lines__sale_id',
  );

  $$SaleLinesTableProcessedTableManager get saleLinesRefs {
    final manager = $$SaleLinesTableTableManager(
      $_db,
      $_db.saleLines,
    ).filter((f) => f.saleId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_saleLinesRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }

  static MultiTypedResultKey<$SalePaymentsTable, List<SalePaymentRow>>
  _salePaymentsRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.salePayments,
    aliasName: 'sales__id__sale_payments__sale_id',
  );

  $$SalePaymentsTableProcessedTableManager get salePaymentsRefs {
    final manager = $$SalePaymentsTableTableManager(
      $_db,
      $_db.salePayments,
    ).filter((f) => f.saleId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(_salePaymentsRefsTable($_db));
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }
}

class $$SalesTableFilterComposer extends Composer<_$AppDatabase, $SalesTable> {
  $$SalesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get receiptNumber => $composableBuilder(
    column: $table.receiptNumber,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get subtotalE4 => $composableBuilder(
    column: $table.subtotalE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get taxE4 => $composableBuilder(
    column: $table.taxE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get totalE4 => $composableBuilder(
    column: $table.totalE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get amountPaidE4 => $composableBuilder(
    column: $table.amountPaidE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get status => $composableBuilder(
    column: $table.status,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get changeE4 => $composableBuilder(
    column: $table.changeE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get taxRateBp => $composableBuilder(
    column: $table.taxRateBp,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get discountReason => $composableBuilder(
    column: $table.discountReason,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get paymentMethod => $composableBuilder(
    column: $table.paymentMethod,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get createdBy => $composableBuilder(
    column: $table.createdBy,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnFilters(column),
  );

  $$WarehousesTableFilterComposer get warehouseId {
    final $$WarehousesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableFilterComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  Expression<bool> saleLinesRefs(
    Expression<bool> Function($$SaleLinesTableFilterComposer f) f,
  ) {
    final $$SaleLinesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.saleLines,
      getReferencedColumn: (t) => t.saleId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SaleLinesTableFilterComposer(
            $db: $db,
            $table: $db.saleLines,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<bool> salePaymentsRefs(
    Expression<bool> Function($$SalePaymentsTableFilterComposer f) f,
  ) {
    final $$SalePaymentsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.salePayments,
      getReferencedColumn: (t) => t.saleId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalePaymentsTableFilterComposer(
            $db: $db,
            $table: $db.salePayments,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$SalesTableOrderingComposer
    extends Composer<_$AppDatabase, $SalesTable> {
  $$SalesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get receiptNumber => $composableBuilder(
    column: $table.receiptNumber,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get subtotalE4 => $composableBuilder(
    column: $table.subtotalE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get taxE4 => $composableBuilder(
    column: $table.taxE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get totalE4 => $composableBuilder(
    column: $table.totalE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get amountPaidE4 => $composableBuilder(
    column: $table.amountPaidE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get status => $composableBuilder(
    column: $table.status,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get changeE4 => $composableBuilder(
    column: $table.changeE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get taxRateBp => $composableBuilder(
    column: $table.taxRateBp,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get discountReason => $composableBuilder(
    column: $table.discountReason,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get paymentMethod => $composableBuilder(
    column: $table.paymentMethod,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get createdAt => $composableBuilder(
    column: $table.createdAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get createdBy => $composableBuilder(
    column: $table.createdBy,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => ColumnOrderings(column),
  );

  $$WarehousesTableOrderingComposer get warehouseId {
    final $$WarehousesTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableOrderingComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SalesTableAnnotationComposer
    extends Composer<_$AppDatabase, $SalesTable> {
  $$SalesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<String> get receiptNumber => $composableBuilder(
    column: $table.receiptNumber,
    builder: (column) => column,
  );

  GeneratedColumn<String> get clientRequestId => $composableBuilder(
    column: $table.clientRequestId,
    builder: (column) => column,
  );

  GeneratedColumn<int> get subtotalE4 => $composableBuilder(
    column: $table.subtotalE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get taxE4 =>
      $composableBuilder(column: $table.taxE4, builder: (column) => column);

  GeneratedColumn<int> get totalE4 =>
      $composableBuilder(column: $table.totalE4, builder: (column) => column);

  GeneratedColumn<int> get amountPaidE4 => $composableBuilder(
    column: $table.amountPaidE4,
    builder: (column) => column,
  );

  GeneratedColumn<String> get status =>
      $composableBuilder(column: $table.status, builder: (column) => column);

  GeneratedColumn<int> get changeE4 =>
      $composableBuilder(column: $table.changeE4, builder: (column) => column);

  GeneratedColumn<int> get taxRateBp =>
      $composableBuilder(column: $table.taxRateBp, builder: (column) => column);

  GeneratedColumn<String> get discountReason => $composableBuilder(
    column: $table.discountReason,
    builder: (column) => column,
  );

  GeneratedColumn<String> get paymentMethod => $composableBuilder(
    column: $table.paymentMethod,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get createdAt =>
      $composableBuilder(column: $table.createdAt, builder: (column) => column);

  GeneratedColumn<String> get createdBy =>
      $composableBuilder(column: $table.createdBy, builder: (column) => column);

  GeneratedColumn<String> get originDeviceId => $composableBuilder(
    column: $table.originDeviceId,
    builder: (column) => column,
  );

  $$WarehousesTableAnnotationComposer get warehouseId {
    final $$WarehousesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.warehouseId,
      referencedTable: $db.warehouses,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$WarehousesTableAnnotationComposer(
            $db: $db,
            $table: $db.warehouses,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  Expression<T> saleLinesRefs<T extends Object>(
    Expression<T> Function($$SaleLinesTableAnnotationComposer a) f,
  ) {
    final $$SaleLinesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.saleLines,
      getReferencedColumn: (t) => t.saleId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SaleLinesTableAnnotationComposer(
            $db: $db,
            $table: $db.saleLines,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }

  Expression<T> salePaymentsRefs<T extends Object>(
    Expression<T> Function($$SalePaymentsTableAnnotationComposer a) f,
  ) {
    final $$SalePaymentsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.salePayments,
      getReferencedColumn: (t) => t.saleId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalePaymentsTableAnnotationComposer(
            $db: $db,
            $table: $db.salePayments,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$SalesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $SalesTable,
          SaleRow,
          $$SalesTableFilterComposer,
          $$SalesTableOrderingComposer,
          $$SalesTableAnnotationComposer,
          $$SalesTableCreateCompanionBuilder,
          $$SalesTableUpdateCompanionBuilder,
          (SaleRow, $$SalesTableReferences),
          SaleRow,
          PrefetchHooks Function({
            bool warehouseId,
            bool saleLinesRefs,
            bool salePaymentsRefs,
          })
        > {
  $$SalesTableTableManager(_$AppDatabase db, $SalesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$SalesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$SalesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$SalesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<String> receiptNumber = const Value.absent(),
                Value<String> clientRequestId = const Value.absent(),
                Value<String> warehouseId = const Value.absent(),
                Value<int> subtotalE4 = const Value.absent(),
                Value<int> discountE4 = const Value.absent(),
                Value<int> taxE4 = const Value.absent(),
                Value<int> totalE4 = const Value.absent(),
                Value<int> amountPaidE4 = const Value.absent(),
                Value<String> status = const Value.absent(),
                Value<int> changeE4 = const Value.absent(),
                Value<int> taxRateBp = const Value.absent(),
                Value<String?> discountReason = const Value.absent(),
                Value<String> paymentMethod = const Value.absent(),
                Value<DateTime> createdAt = const Value.absent(),
                Value<String> createdBy = const Value.absent(),
                Value<String?> originDeviceId = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SalesCompanion(
                id: id,
                companyId: companyId,
                receiptNumber: receiptNumber,
                clientRequestId: clientRequestId,
                warehouseId: warehouseId,
                subtotalE4: subtotalE4,
                discountE4: discountE4,
                taxE4: taxE4,
                totalE4: totalE4,
                amountPaidE4: amountPaidE4,
                status: status,
                changeE4: changeE4,
                taxRateBp: taxRateBp,
                discountReason: discountReason,
                paymentMethod: paymentMethod,
                createdAt: createdAt,
                createdBy: createdBy,
                originDeviceId: originDeviceId,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                required String receiptNumber,
                required String clientRequestId,
                required String warehouseId,
                required int subtotalE4,
                Value<int> discountE4 = const Value.absent(),
                Value<int> taxE4 = const Value.absent(),
                required int totalE4,
                required int amountPaidE4,
                Value<String> status = const Value.absent(),
                Value<int> changeE4 = const Value.absent(),
                Value<int> taxRateBp = const Value.absent(),
                Value<String?> discountReason = const Value.absent(),
                required String paymentMethod,
                required DateTime createdAt,
                required String createdBy,
                Value<String?> originDeviceId = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SalesCompanion.insert(
                id: id,
                companyId: companyId,
                receiptNumber: receiptNumber,
                clientRequestId: clientRequestId,
                warehouseId: warehouseId,
                subtotalE4: subtotalE4,
                discountE4: discountE4,
                taxE4: taxE4,
                totalE4: totalE4,
                amountPaidE4: amountPaidE4,
                status: status,
                changeE4: changeE4,
                taxRateBp: taxRateBp,
                discountReason: discountReason,
                paymentMethod: paymentMethod,
                createdAt: createdAt,
                createdBy: createdBy,
                originDeviceId: originDeviceId,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$SalesTable, SaleRow>(table),
                  $$SalesTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback:
              ({
                warehouseId = false,
                saleLinesRefs = false,
                salePaymentsRefs = false,
              }) {
                return PrefetchHooks(
                  db: db,
                  explicitlyWatchedTables: [
                    if (saleLinesRefs) db.saleLines,
                    if (salePaymentsRefs) db.salePayments,
                  ],
                  addJoins:
                      <
                        T extends TableManagerState<
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic,
                          dynamic
                        >
                      >(state) {
                        if (warehouseId) {
                          state = state.withJoin(
                            currentTable: table,
                            currentColumn: table.warehouseId,
                            referencedTable: $$SalesTableReferences
                                ._warehouseIdTable(db),
                            referencedColumn: $$SalesTableReferences
                                ._warehouseIdTable(db)
                                .id,
                          ) as T;
                        }

                        return state;
                      },
                  getPrefetchedDataCallback: (items) async {
                    return [
                      if (saleLinesRefs)
                        await $_getPrefetchedData<
                          SaleRow,
                          $SalesTable,
                          SaleLineRow
                        >(
                          currentTable: table,
                          referencedTable: $$SalesTableReferences
                              ._saleLinesRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$SalesTableReferences(
                                db,
                                table,
                                p0,
                              ).saleLinesRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.saleId == item.id,
                              ),
                          typedResults: items,
                        ),
                      if (salePaymentsRefs)
                        await $_getPrefetchedData<
                          SaleRow,
                          $SalesTable,
                          SalePaymentRow
                        >(
                          currentTable: table,
                          referencedTable: $$SalesTableReferences
                              ._salePaymentsRefsTable(db),
                          managerFromTypedResult: (p0) =>
                              $$SalesTableReferences(
                                db,
                                table,
                                p0,
                              ).salePaymentsRefs,
                          referencedItemsForCurrentItem:
                              (item, referencedItems) => referencedItems.where(
                                (e) => e.saleId == item.id,
                              ),
                          typedResults: items,
                        ),
                    ];
                  },
                );
              },
        ),
      );
}

typedef $$SalesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $SalesTable,
      SaleRow,
      $$SalesTableFilterComposer,
      $$SalesTableOrderingComposer,
      $$SalesTableAnnotationComposer,
      $$SalesTableCreateCompanionBuilder,
      $$SalesTableUpdateCompanionBuilder,
      (SaleRow, $$SalesTableReferences),
      SaleRow,
      PrefetchHooks Function({
        bool warehouseId,
        bool saleLinesRefs,
        bool salePaymentsRefs,
      })
    >;
typedef $$SaleLinesTableCreateCompanionBuilder = SaleLinesCompanion Function({
  required String id,
  required String saleId,
  required String productId,
  required String skuSnapshot,
  required String nameSnapshot,
  required int quantityE4,
  required int unitPriceE4,
  Value<int> discountE4,
  required int lineTotalE4,
  Value<int> taxE4,
  Value<int> rowid,
});
typedef $$SaleLinesTableUpdateCompanionBuilder = SaleLinesCompanion Function({
  Value<String> id,
  Value<String> saleId,
  Value<String> productId,
  Value<String> skuSnapshot,
  Value<String> nameSnapshot,
  Value<int> quantityE4,
  Value<int> unitPriceE4,
  Value<int> discountE4,
  Value<int> lineTotalE4,
  Value<int> taxE4,
  Value<int> rowid,
});

final class $$SaleLinesTableReferences
    extends BaseReferences<_$AppDatabase, $SaleLinesTable, SaleLineRow> {
  $$SaleLinesTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static $SalesTable _saleIdTable(_$AppDatabase db) =>
      db.sales.createAlias('sale_lines__sale_id__sales__id');

  $$SalesTableProcessedTableManager get saleId {
    final $_column = $_itemColumn<String>('sale_id')!;

    final manager = $$SalesTableTableManager(
      $_db,
      $_db.sales,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_saleIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }

  static $ProductsTable _productIdTable(_$AppDatabase db) =>
      db.products.createAlias('sale_lines__product_id__products__id');

  $$ProductsTableProcessedTableManager get productId {
    final $_column = $_itemColumn<String>('product_id')!;

    final manager = $$ProductsTableTableManager(
      $_db,
      $_db.products,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_productIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }
}

class $$SaleLinesTableFilterComposer
    extends Composer<_$AppDatabase, $SaleLinesTable> {
  $$SaleLinesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get skuSnapshot => $composableBuilder(
    column: $table.skuSnapshot,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get nameSnapshot => $composableBuilder(
    column: $table.nameSnapshot,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get quantityE4 => $composableBuilder(
    column: $table.quantityE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get unitPriceE4 => $composableBuilder(
    column: $table.unitPriceE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get lineTotalE4 => $composableBuilder(
    column: $table.lineTotalE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get taxE4 => $composableBuilder(
    column: $table.taxE4,
    builder: (column) => ColumnFilters(column),
  );

  $$SalesTableFilterComposer get saleId {
    final $$SalesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableFilterComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$ProductsTableFilterComposer get productId {
    final $$ProductsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableFilterComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SaleLinesTableOrderingComposer
    extends Composer<_$AppDatabase, $SaleLinesTable> {
  $$SaleLinesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get skuSnapshot => $composableBuilder(
    column: $table.skuSnapshot,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get nameSnapshot => $composableBuilder(
    column: $table.nameSnapshot,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get quantityE4 => $composableBuilder(
    column: $table.quantityE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get unitPriceE4 => $composableBuilder(
    column: $table.unitPriceE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get lineTotalE4 => $composableBuilder(
    column: $table.lineTotalE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get taxE4 => $composableBuilder(
    column: $table.taxE4,
    builder: (column) => ColumnOrderings(column),
  );

  $$SalesTableOrderingComposer get saleId {
    final $$SalesTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableOrderingComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$ProductsTableOrderingComposer get productId {
    final $$ProductsTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableOrderingComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SaleLinesTableAnnotationComposer
    extends Composer<_$AppDatabase, $SaleLinesTable> {
  $$SaleLinesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get skuSnapshot => $composableBuilder(
    column: $table.skuSnapshot,
    builder: (column) => column,
  );

  GeneratedColumn<String> get nameSnapshot => $composableBuilder(
    column: $table.nameSnapshot,
    builder: (column) => column,
  );

  GeneratedColumn<int> get quantityE4 => $composableBuilder(
    column: $table.quantityE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get unitPriceE4 => $composableBuilder(
    column: $table.unitPriceE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get discountE4 => $composableBuilder(
    column: $table.discountE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get lineTotalE4 => $composableBuilder(
    column: $table.lineTotalE4,
    builder: (column) => column,
  );

  GeneratedColumn<int> get taxE4 =>
      $composableBuilder(column: $table.taxE4, builder: (column) => column);

  $$SalesTableAnnotationComposer get saleId {
    final $$SalesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableAnnotationComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }

  $$ProductsTableAnnotationComposer get productId {
    final $$ProductsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.productId,
      referencedTable: $db.products,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$ProductsTableAnnotationComposer(
            $db: $db,
            $table: $db.products,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SaleLinesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $SaleLinesTable,
          SaleLineRow,
          $$SaleLinesTableFilterComposer,
          $$SaleLinesTableOrderingComposer,
          $$SaleLinesTableAnnotationComposer,
          $$SaleLinesTableCreateCompanionBuilder,
          $$SaleLinesTableUpdateCompanionBuilder,
          (SaleLineRow, $$SaleLinesTableReferences),
          SaleLineRow,
          PrefetchHooks Function({bool saleId, bool productId})
        > {
  $$SaleLinesTableTableManager(_$AppDatabase db, $SaleLinesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$SaleLinesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$SaleLinesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$SaleLinesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> saleId = const Value.absent(),
                Value<String> productId = const Value.absent(),
                Value<String> skuSnapshot = const Value.absent(),
                Value<String> nameSnapshot = const Value.absent(),
                Value<int> quantityE4 = const Value.absent(),
                Value<int> unitPriceE4 = const Value.absent(),
                Value<int> discountE4 = const Value.absent(),
                Value<int> lineTotalE4 = const Value.absent(),
                Value<int> taxE4 = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SaleLinesCompanion(
                id: id,
                saleId: saleId,
                productId: productId,
                skuSnapshot: skuSnapshot,
                nameSnapshot: nameSnapshot,
                quantityE4: quantityE4,
                unitPriceE4: unitPriceE4,
                discountE4: discountE4,
                lineTotalE4: lineTotalE4,
                taxE4: taxE4,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String saleId,
                required String productId,
                required String skuSnapshot,
                required String nameSnapshot,
                required int quantityE4,
                required int unitPriceE4,
                Value<int> discountE4 = const Value.absent(),
                required int lineTotalE4,
                Value<int> taxE4 = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SaleLinesCompanion.insert(
                id: id,
                saleId: saleId,
                productId: productId,
                skuSnapshot: skuSnapshot,
                nameSnapshot: nameSnapshot,
                quantityE4: quantityE4,
                unitPriceE4: unitPriceE4,
                discountE4: discountE4,
                lineTotalE4: lineTotalE4,
                taxE4: taxE4,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$SaleLinesTable, SaleLineRow>(table),
                  $$SaleLinesTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({saleId = false, productId = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [],
              addJoins:
                  <
                    T extends TableManagerState<
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic
                    >
                  >(state) {
                    if (saleId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.saleId,
                        referencedTable: $$SaleLinesTableReferences
                            ._saleIdTable(db),
                        referencedColumn: $$SaleLinesTableReferences
                            ._saleIdTable(db)
                            .id,
                      ) as T;
                    }
                    if (productId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.productId,
                        referencedTable: $$SaleLinesTableReferences
                            ._productIdTable(db),
                        referencedColumn: $$SaleLinesTableReferences
                            ._productIdTable(db)
                            .id,
                      ) as T;
                    }

                    return state;
                  },
              getPrefetchedDataCallback: (items) async {
                return [];
              },
            );
          },
        ),
      );
}

typedef $$SaleLinesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $SaleLinesTable,
      SaleLineRow,
      $$SaleLinesTableFilterComposer,
      $$SaleLinesTableOrderingComposer,
      $$SaleLinesTableAnnotationComposer,
      $$SaleLinesTableCreateCompanionBuilder,
      $$SaleLinesTableUpdateCompanionBuilder,
      (SaleLineRow, $$SaleLinesTableReferences),
      SaleLineRow,
      PrefetchHooks Function({bool saleId, bool productId})
    >;
typedef $$SalePaymentsTableCreateCompanionBuilder =
    SalePaymentsCompanion Function({
      required String id,
      required String saleId,
      required int amountE4,
      required String method,
      required DateTime receivedAt,
      Value<String?> reference,
      Value<int> rowid,
    });
typedef $$SalePaymentsTableUpdateCompanionBuilder =
    SalePaymentsCompanion Function({
      Value<String> id,
      Value<String> saleId,
      Value<int> amountE4,
      Value<String> method,
      Value<DateTime> receivedAt,
      Value<String?> reference,
      Value<int> rowid,
    });

final class $$SalePaymentsTableReferences
    extends BaseReferences<_$AppDatabase, $SalePaymentsTable, SalePaymentRow> {
  $$SalePaymentsTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static $SalesTable _saleIdTable(_$AppDatabase db) =>
      db.sales.createAlias('sale_payments__sale_id__sales__id');

  $$SalesTableProcessedTableManager get saleId {
    final $_column = $_itemColumn<String>('sale_id')!;

    final manager = $$SalesTableTableManager(
      $_db,
      $_db.sales,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_saleIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }
}

class $$SalePaymentsTableFilterComposer
    extends Composer<_$AppDatabase, $SalePaymentsTable> {
  $$SalePaymentsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get amountE4 => $composableBuilder(
    column: $table.amountE4,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get method => $composableBuilder(
    column: $table.method,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get receivedAt => $composableBuilder(
    column: $table.receivedAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get reference => $composableBuilder(
    column: $table.reference,
    builder: (column) => ColumnFilters(column),
  );

  $$SalesTableFilterComposer get saleId {
    final $$SalesTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableFilterComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SalePaymentsTableOrderingComposer
    extends Composer<_$AppDatabase, $SalePaymentsTable> {
  $$SalePaymentsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get amountE4 => $composableBuilder(
    column: $table.amountE4,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get method => $composableBuilder(
    column: $table.method,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get receivedAt => $composableBuilder(
    column: $table.receivedAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get reference => $composableBuilder(
    column: $table.reference,
    builder: (column) => ColumnOrderings(column),
  );

  $$SalesTableOrderingComposer get saleId {
    final $$SalesTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableOrderingComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SalePaymentsTableAnnotationComposer
    extends Composer<_$AppDatabase, $SalePaymentsTable> {
  $$SalePaymentsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<int> get amountE4 =>
      $composableBuilder(column: $table.amountE4, builder: (column) => column);

  GeneratedColumn<String> get method =>
      $composableBuilder(column: $table.method, builder: (column) => column);

  GeneratedColumn<DateTime> get receivedAt => $composableBuilder(
    column: $table.receivedAt,
    builder: (column) => column,
  );

  GeneratedColumn<String> get reference =>
      $composableBuilder(column: $table.reference, builder: (column) => column);

  $$SalesTableAnnotationComposer get saleId {
    final $$SalesTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.saleId,
      referencedTable: $db.sales,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$SalesTableAnnotationComposer(
            $db: $db,
            $table: $db.sales,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$SalePaymentsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $SalePaymentsTable,
          SalePaymentRow,
          $$SalePaymentsTableFilterComposer,
          $$SalePaymentsTableOrderingComposer,
          $$SalePaymentsTableAnnotationComposer,
          $$SalePaymentsTableCreateCompanionBuilder,
          $$SalePaymentsTableUpdateCompanionBuilder,
          (SalePaymentRow, $$SalePaymentsTableReferences),
          SalePaymentRow,
          PrefetchHooks Function({bool saleId})
        > {
  $$SalePaymentsTableTableManager(_$AppDatabase db, $SalePaymentsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$SalePaymentsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$SalePaymentsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$SalePaymentsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> saleId = const Value.absent(),
                Value<int> amountE4 = const Value.absent(),
                Value<String> method = const Value.absent(),
                Value<DateTime> receivedAt = const Value.absent(),
                Value<String?> reference = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SalePaymentsCompanion(
                id: id,
                saleId: saleId,
                amountE4: amountE4,
                method: method,
                receivedAt: receivedAt,
                reference: reference,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String saleId,
                required int amountE4,
                required String method,
                required DateTime receivedAt,
                Value<String?> reference = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SalePaymentsCompanion.insert(
                id: id,
                saleId: saleId,
                amountE4: amountE4,
                method: method,
                receivedAt: receivedAt,
                reference: reference,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$SalePaymentsTable, SalePaymentRow>(table),
                  $$SalePaymentsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({saleId = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [],
              addJoins:
                  <
                    T extends TableManagerState<
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic
                    >
                  >(state) {
                    if (saleId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.saleId,
                        referencedTable: $$SalePaymentsTableReferences
                            ._saleIdTable(db),
                        referencedColumn: $$SalePaymentsTableReferences
                            ._saleIdTable(db)
                            .id,
                      ) as T;
                    }

                    return state;
                  },
              getPrefetchedDataCallback: (items) async {
                return [];
              },
            );
          },
        ),
      );
}

typedef $$SalePaymentsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $SalePaymentsTable,
      SalePaymentRow,
      $$SalePaymentsTableFilterComposer,
      $$SalePaymentsTableOrderingComposer,
      $$SalePaymentsTableAnnotationComposer,
      $$SalePaymentsTableCreateCompanionBuilder,
      $$SalePaymentsTableUpdateCompanionBuilder,
      (SalePaymentRow, $$SalePaymentsTableReferences),
      SalePaymentRow,
      PrefetchHooks Function({bool saleId})
    >;
typedef $$PosSettingsTableCreateCompanionBuilder =
    PosSettingsCompanion Function({
      required String id,
      required String companyId,
      Value<int> taxRateBp,
      Value<int> maxDiscountBp,
      Value<String?> receiptFooter,
      required DateTime updatedAt,
      Value<int> rowid,
    });
typedef $$PosSettingsTableUpdateCompanionBuilder =
    PosSettingsCompanion Function({
      Value<String> id,
      Value<String> companyId,
      Value<int> taxRateBp,
      Value<int> maxDiscountBp,
      Value<String?> receiptFooter,
      Value<DateTime> updatedAt,
      Value<int> rowid,
    });

class $$PosSettingsTableFilterComposer
    extends Composer<_$AppDatabase, $PosSettingsTable> {
  $$PosSettingsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get taxRateBp => $composableBuilder(
    column: $table.taxRateBp,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get maxDiscountBp => $composableBuilder(
    column: $table.maxDiscountBp,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get receiptFooter => $composableBuilder(
    column: $table.receiptFooter,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnFilters(column),
  );
}

class $$PosSettingsTableOrderingComposer
    extends Composer<_$AppDatabase, $PosSettingsTable> {
  $$PosSettingsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get companyId => $composableBuilder(
    column: $table.companyId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get taxRateBp => $composableBuilder(
    column: $table.taxRateBp,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get maxDiscountBp => $composableBuilder(
    column: $table.maxDiscountBp,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get receiptFooter => $composableBuilder(
    column: $table.receiptFooter,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<DateTime> get updatedAt => $composableBuilder(
    column: $table.updatedAt,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$PosSettingsTableAnnotationComposer
    extends Composer<_$AppDatabase, $PosSettingsTable> {
  $$PosSettingsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get companyId =>
      $composableBuilder(column: $table.companyId, builder: (column) => column);

  GeneratedColumn<int> get taxRateBp =>
      $composableBuilder(column: $table.taxRateBp, builder: (column) => column);

  GeneratedColumn<int> get maxDiscountBp => $composableBuilder(
    column: $table.maxDiscountBp,
    builder: (column) => column,
  );

  GeneratedColumn<String> get receiptFooter => $composableBuilder(
    column: $table.receiptFooter,
    builder: (column) => column,
  );

  GeneratedColumn<DateTime> get updatedAt =>
      $composableBuilder(column: $table.updatedAt, builder: (column) => column);
}

class $$PosSettingsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $PosSettingsTable,
          PosSettingsRow,
          $$PosSettingsTableFilterComposer,
          $$PosSettingsTableOrderingComposer,
          $$PosSettingsTableAnnotationComposer,
          $$PosSettingsTableCreateCompanionBuilder,
          $$PosSettingsTableUpdateCompanionBuilder,
          (
            PosSettingsRow,
            BaseReferences<_$AppDatabase, $PosSettingsTable, PosSettingsRow>,
          ),
          PosSettingsRow,
          PrefetchHooks Function()
        > {
  $$PosSettingsTableTableManager(_$AppDatabase db, $PosSettingsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$PosSettingsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$PosSettingsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$PosSettingsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> companyId = const Value.absent(),
                Value<int> taxRateBp = const Value.absent(),
                Value<int> maxDiscountBp = const Value.absent(),
                Value<String?> receiptFooter = const Value.absent(),
                Value<DateTime> updatedAt = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => PosSettingsCompanion(
                id: id,
                companyId: companyId,
                taxRateBp: taxRateBp,
                maxDiscountBp: maxDiscountBp,
                receiptFooter: receiptFooter,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String companyId,
                Value<int> taxRateBp = const Value.absent(),
                Value<int> maxDiscountBp = const Value.absent(),
                Value<String?> receiptFooter = const Value.absent(),
                required DateTime updatedAt,
                Value<int> rowid = const Value.absent(),
              }) => PosSettingsCompanion.insert(
                id: id,
                companyId: companyId,
                taxRateBp: taxRateBp,
                maxDiscountBp: maxDiscountBp,
                receiptFooter: receiptFooter,
                updatedAt: updatedAt,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$PosSettingsTable, PosSettingsRow>(table),
                  BaseReferences<
                    _$AppDatabase,
                    $PosSettingsTable,
                    PosSettingsRow
                  >(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$PosSettingsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $PosSettingsTable,
      PosSettingsRow,
      $$PosSettingsTableFilterComposer,
      $$PosSettingsTableOrderingComposer,
      $$PosSettingsTableAnnotationComposer,
      $$PosSettingsTableCreateCompanionBuilder,
      $$PosSettingsTableUpdateCompanionBuilder,
      (
        PosSettingsRow,
        BaseReferences<_$AppDatabase, $PosSettingsTable, PosSettingsRow>,
      ),
      PosSettingsRow,
      PrefetchHooks Function()
    >;

class $AppDatabaseManager {
  final _$AppDatabase _db;
  $AppDatabaseManager(this._db);
  $$UnitsTableTableManager get units =>
      $$UnitsTableTableManager(_db, _db.units);
  $$ProductsTableTableManager get products =>
      $$ProductsTableTableManager(_db, _db.products);
  $$BusinessProfilesTableTableManager get businessProfiles =>
      $$BusinessProfilesTableTableManager(_db, _db.businessProfiles);
  $$WarehousesTableTableManager get warehouses =>
      $$WarehousesTableTableManager(_db, _db.warehouses);
  $$StockMovementsTableTableManager get stockMovements =>
      $$StockMovementsTableTableManager(_db, _db.stockMovements);
  $$StockBalancesTableTableManager get stockBalances =>
      $$StockBalancesTableTableManager(_db, _db.stockBalances);
  $$SalesTableTableManager get sales =>
      $$SalesTableTableManager(_db, _db.sales);
  $$SaleLinesTableTableManager get saleLines =>
      $$SaleLinesTableTableManager(_db, _db.saleLines);
  $$SalePaymentsTableTableManager get salePayments =>
      $$SalePaymentsTableTableManager(_db, _db.salePayments);
  $$PosSettingsTableTableManager get posSettings =>
      $$PosSettingsTableTableManager(_db, _db.posSettings);
}
