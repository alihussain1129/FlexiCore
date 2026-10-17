import 'dart:convert';
import 'dart:io' as io;
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:csv/csv.dart';
import 'package:excel/excel.dart';
import 'package:xml/xml.dart';

enum ImportEntity { organizations, companies }

class _ExpandedBytesCounter implements Sink<List<int>> {
  _ExpandedBytesCounter(this.maximumBytes);

  final int maximumBytes;
  int bytes = 0;

  @override
  void add(List<int> chunk) {
    if (bytes + chunk.length > maximumBytes) {
      throw const FormatException(
        'The Excel workbook exceeds safe import limits.',
      );
    }
    bytes += chunk.length;
  }

  @override
  void close() {}
}

class SpreadsheetGrid {
  const SpreadsheetGrid({required this.headers, required this.rows});

  final List<String> headers;
  final List<List<String>> rows;
}

class ImportRowPreview {
  const ImportRowPreview({
    required this.sourceRow,
    required this.values,
    required this.errors,
  });

  final int sourceRow;
  final Map<String, String> values;
  final List<String> errors;

  bool get isValid => errors.isEmpty;
}

class SpreadsheetExchange {
  static const maxFileBytes = 20 * 1024 * 1024;
  static const _maxExpandedXlsxBytes = 100 * 1024 * 1024;
  static const _maxXlsxEntryBytes = 32 * 1024 * 1024;
  static const _maxXlsxEntries = 256;
  static const _maxXlsxCompressionRatio = 1000;
  static const maxRows = 5000;
  static const maxColumns = 32;

  static const organizationHeaders = ['Name', 'Slug'];
  static const companyHeaders = ['Name', 'Slug', 'Industry key'];

  static SpreadsheetGrid decodeXlsx(Uint8List bytes) {
    _checkFileSize(bytes.length);
    try {
      _checkXlsxArchive(bytes);
      final workbook = Excel.decodeBytes(bytes);
      final populated = workbook.tables.values
          .where((sheet) => sheet.maxRows > 0)
          .toList();
      if (populated.length != 1) {
        throw const FormatException(
          'Workbook must contain exactly one non-empty worksheet.',
        );
      }
      final sheet = populated.single;
      return _gridFromRows(
        sheet.rows.map(
          (row) => row.map((cell) => _cellText(cell?.value)).toList(),
        ),
      );
    } on FormatException {
      rethrow;
    } on Object {
      throw const FormatException(
        'The Excel file is invalid or cannot be read.',
      );
    }
  }

  static SpreadsheetGrid decodeCsv(Uint8List bytes) {
    _checkFileSize(bytes.length);
    try {
      var content = utf8.decode(bytes);
      if (content.startsWith('\uFEFF')) content = content.substring(1);
      final rows = Csv()
          .decode(content)
          .map((row) => row.map((cell) => cell?.toString() ?? '').toList());
      return _gridFromRows(rows);
    } on FormatException {
      rethrow;
    } on Object {
      throw const FormatException('The CSV file is invalid or cannot be read.');
    }
  }

  static List<ImportRowPreview> previewImport({
    required SpreadsheetGrid grid,
    required ImportEntity entity,
    required Map<String, int?> columnMap,
    required Set<String> existingSlugs,
  }) {
    final fields = entity == ImportEntity.organizations
        ? const ['name', 'slug']
        : const ['name', 'slug', 'industryKey'];
    final indexes = <int>[];
    for (final field in fields) {
      final index = columnMap[field];
      if (index == null || index < 0 || index >= grid.headers.length) {
        throw const FormatException('Map every required field to a column.');
      }
      indexes.add(index);
    }
    if (indexes.toSet().length != indexes.length) {
      throw const FormatException('Each field must use a different column.');
    }

    final seenSlugs = <String>{};
    return [
      for (var index = 0; index < grid.rows.length; index++)
        _previewRow(
          sourceRow: index + 2,
          row: grid.rows[index],
          entity: entity,
          fields: fields,
          indexes: indexes,
          existingSlugs: existingSlugs,
          seenSlugs: seenSlugs,
        ),
    ];
  }

  static Uint8List encodeXlsx({
    required String sheetName,
    required List<String> headers,
    required List<List<String>> rows,
  }) {
    if (!RegExp(r'^[^\\/?*:[\]]{1,31}$').hasMatch(sheetName)) {
      throw ArgumentError.value(
        sheetName,
        'sheetName',
        'Invalid worksheet name.',
      );
    }
    final workbook = Excel.createExcel();
    workbook.rename('Sheet1', sheetName);
    final sheet = workbook[sheetName];
    final headerStyle = CellStyle(
      bold: true,
      fontSize: 11,
      fontColorHex: ExcelColor.white,
      backgroundColorHex: ExcelColor.fromHexString('176B5B'),
      verticalAlign: VerticalAlign.Center,
    );
    sheet.appendRow(headers.map((header) => TextCellValue(header)).toList());
    for (var column = 0; column < headers.length; column++) {
      sheet
              .cell(
                CellIndex.indexByColumnRow(columnIndex: column, rowIndex: 0),
              )
              .cellStyle =
          headerStyle;
      sheet.setColumnWidth(column, _columnWidth(headers[column]));
    }
    for (final row in rows) {
      if (row.length != headers.length) {
        throw ArgumentError('Every export row must match the header width.');
      }
      sheet.appendRow(row.map((value) => TextCellValue(value)).toList());
    }

    final encoded = workbook.save();
    if (encoded == null) throw StateError('Excel workbook generation failed.');
    return _addFilterAndFrozenHeader(
      Uint8List.fromList(encoded),
      headers.length,
      rows.length + 1,
    );
  }

  static String encodeCsv(List<List<String>> rows) {
    final safeRows = rows
        .map((row) => row.map(_spreadsheetSafeText).toList())
        .toList();
    return Csv(addBom: true).encode(safeRows);
  }

  static String encodeTsv(List<List<String>> rows) => rows
      .map(
        (row) => row
            .map((value) {
              final safe = _spreadsheetSafeText(value);
              return safe
                  .replaceAll('\t', ' ')
                  .replaceAll('\r', ' ')
                  .replaceAll('\n', ' ');
            })
            .join('\t'),
      )
      .join('\r\n');

  static Uint8List encodeDocx({
    required String title,
    required List<String> headers,
    required List<List<String>> rows,
    DateTime? generatedOn,
  }) {
    if (headers.isEmpty || rows.any((row) => row.length != headers.length)) {
      throw ArgumentError(
        'Document rows must match the non-empty header list.',
      );
    }
    final generated = (generatedOn ?? DateTime.now())
        .toIso8601String()
        .substring(0, 10);
    final tableRows = <String>[
      _wordTableRow(headers, header: true),
      for (final row in rows) _wordTableRow(row),
    ].join();
    final gridColumns = List.filled(
      headers.length,
      '<w:gridCol w:w="2100"/>',
    ).join();
    final document =
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
 <w:body>
  <w:p><w:pPr><w:pStyle w:val="Title"/></w:pPr><w:r><w:t>${_xmlEscape(title)}</w:t></w:r></w:p>
  <w:p><w:r><w:t>Generated $generated · ${rows.length} record(s)</w:t></w:r></w:p>
  <w:tbl>
   <w:tblPr><w:tblStyle w:val="TableGrid"/><w:tblW w:w="0" w:type="auto"/><w:tblLayout w:type="autofit"/></w:tblPr>
   <w:tblGrid>$gridColumns</w:tblGrid>
   $tableRows
  </w:tbl>
  <w:sectPr><w:pgSz w:w="16838" w:h="11906" w:orient="landscape"/><w:pgMar w:top="720" w:right="720" w:bottom="720" w:left="720" w:header="360" w:footer="360" w:gutter="0"/></w:sectPr>
 </w:body>
</w:document>''';
    const contentTypes =
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
 <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
 <Default Extension="xml" ContentType="application/xml"/>
 <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
 <Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
</Types>''';
    const packageRelationships =
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
 <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
</Relationships>''';
    const documentRelationships =
        '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
 <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
</Relationships>''';
    const styles = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
 <w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Aptos" w:hAnsi="Aptos"/><w:sz w:val="20"/></w:rPr></w:rPrDefault></w:docDefaults>
 <w:style w:type="paragraph" w:styleId="Title"><w:name w:val="Title"/><w:pPr><w:spacing w:after="240"/></w:pPr><w:rPr><w:b/><w:color w:val="176B5B"/><w:sz w:val="32"/></w:rPr></w:style>
 <w:style w:type="table" w:styleId="TableGrid"><w:name w:val="Table Grid"/></w:style>
</w:styles>''';

    final archive = Archive()
      ..addFile(ArchiveFile.string('[Content_Types].xml', contentTypes))
      ..addFile(ArchiveFile.string('_rels/.rels', packageRelationships))
      ..addFile(ArchiveFile.string('word/document.xml', document))
      ..addFile(ArchiveFile.string('word/styles.xml', styles))
      ..addFile(
        ArchiveFile.string(
          'word/_rels/document.xml.rels',
          documentRelationships,
        ),
      );
    final bytes = ZipEncoder().encode(archive);
    if (bytes == null) {
      throw StateError('Could not generate the Word document.');
    }
    return Uint8List.fromList(bytes);
  }

  static String _cellText(CellValue? value) => switch (value) {
    null => '',
    FormulaCellValue() => throw const FormatException(
      'Formula cells are not accepted. Replace formulas with plain values.',
    ),
    TextCellValue() => value.toString(),
    IntCellValue() => value.toString(),
    DoubleCellValue() => value.toString(),
    BoolCellValue() => value.toString(),
    DateCellValue() => value.toString(),
    DateTimeCellValue() => value.toString(),
    TimeCellValue() => value.toString(),
  };

  static SpreadsheetGrid _gridFromRows(Iterable<List<String>> input) {
    final rows = input.toList();
    while (rows.isNotEmpty && rows.last.every((cell) => cell.trim().isEmpty)) {
      rows.removeLast();
    }
    if (rows.isEmpty) {
      throw const FormatException('The file has no header row.');
    }
    if (rows.length > maxRows + 1) {
      throw const FormatException(
        'The file exceeds the 5,000-row import limit.',
      );
    }
    final width = rows.map((row) => row.length).reduce((a, b) => a > b ? a : b);
    if (width < 1 || width > maxColumns) {
      throw const FormatException(
        'The file must contain between 1 and 32 columns.',
      );
    }
    final headers = List.generate(
      width,
      (index) => index < rows.first.length ? rows.first[index].trim() : '',
    );
    if (headers.every((header) => header.isEmpty)) {
      throw const FormatException('The file has no column headings.');
    }
    final normalizedHeaders = <String>{};
    for (final header in headers.where((item) => item.isNotEmpty)) {
      if (!normalizedHeaders.add(header.toLowerCase())) {
        throw const FormatException('Column headings must be unique.');
      }
    }
    return SpreadsheetGrid(
      headers: headers
          .asMap()
          .entries
          .map(
            (entry) =>
                entry.value.isEmpty ? 'Column ${entry.key + 1}' : entry.value,
          )
          .toList(),
      rows: [
        for (final row in rows.skip(1))
          List.generate(width, (index) => index < row.length ? row[index] : ''),
      ],
    );
  }

  static ImportRowPreview _previewRow({
    required int sourceRow,
    required List<String> row,
    required ImportEntity entity,
    required List<String> fields,
    required List<int> indexes,
    required Set<String> existingSlugs,
    required Set<String> seenSlugs,
  }) {
    final values = <String, String>{
      for (var index = 0; index < fields.length; index++)
        fields[index]: indexes[index] < row.length ? row[index].trim() : '',
    };
    final errors = <String>[];
    final name = values['name']!;
    final slug = values['slug']!;
    if (name.length < 2 ||
        name.length > 160 ||
        name.contains(RegExp(r'[\x00-\x1F\x7F]'))) {
      errors.add(
        'Name must be 2 to 160 characters without control characters.',
      );
    }
    String? normalizedSlug;
    if (!RegExp(r'^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$').hasMatch(slug)) {
      errors.add('Slug must be 3 to 80 lowercase letters, digits or hyphens.');
    } else {
      normalizedSlug = slug.toLowerCase();
      if (existingSlugs.contains(normalizedSlug)) {
        errors.add('Slug already exists in this workspace.');
      } else if (seenSlugs.contains(normalizedSlug)) {
        errors.add('Slug is duplicated in this file.');
      }
    }
    if (entity == ImportEntity.companies &&
        !RegExp(r'^[a-z][a-z0-9_]{1,63}$').hasMatch(values['industryKey']!)) {
      errors.add(
        'Industry key must be 2 to 64 lowercase letters, digits or underscores.',
      );
    }
    if (errors.isEmpty && normalizedSlug != null) {
      seenSlugs.add(normalizedSlug);
    }
    return ImportRowPreview(
      sourceRow: sourceRow,
      values: values,
      errors: errors,
    );
  }

  static Uint8List _addFilterAndFrozenHeader(
    Uint8List bytes,
    int columnCount,
    int rowCount,
  ) {
    final archive = ZipDecoder().decodeBytes(bytes);
    final sheetFile = archive.findFile('xl/worksheets/sheet1.xml');
    if (sheetFile == null) {
      throw StateError('Generated workbook has no first worksheet.');
    }
    final document = XmlDocument.parse(
      utf8.decode(sheetFile.content as List<int>),
    );
    final root = document.rootElement;
    final sheetData = root.findElements('sheetData').first;
    final sheetChildren = root.children;
    sheetChildren.insert(
      sheetChildren.indexOf(sheetData) + 1,
      XmlElement(XmlName('autoFilter'), [
        XmlAttribute(
          XmlName('ref'),
          'A1:${_columnName(columnCount - 1)}$rowCount',
        ),
      ]),
    );

    var sheetViews = root.findElements('sheetViews').firstOrNull;
    if (sheetViews == null) {
      sheetViews = XmlElement(XmlName('sheetViews'));
      root.children.insert(0, sheetViews);
    }
    var sheetView = sheetViews.findElements('sheetView').firstOrNull;
    if (sheetView == null) {
      sheetView = XmlElement(XmlName('sheetView'), [
        XmlAttribute(XmlName('workbookViewId'), '0'),
      ]);
      sheetViews.children.add(sheetView);
    }
    sheetView.children.insert(
      0,
      XmlElement(XmlName('pane'), [
        XmlAttribute(XmlName('ySplit'), '1'),
        XmlAttribute(XmlName('topLeftCell'), 'A2'),
        XmlAttribute(XmlName('activePane'), 'bottomLeft'),
        XmlAttribute(XmlName('state'), 'frozen'),
      ]),
    );

    archive.removeFile(sheetFile);
    archive.addFile(ArchiveFile.string(sheetFile.name, document.toXmlString()));
    final output = ZipEncoder().encode(archive);
    if (output == null) {
      throw StateError('Could not finish the Excel workbook.');
    }
    return Uint8List.fromList(output);
  }

  static String _columnName(int index) {
    var number = index + 1;
    var name = '';
    while (number > 0) {
      final remainder = (number - 1) % 26;
      name = String.fromCharCode(65 + remainder) + name;
      number = (number - remainder - 1) ~/ 26;
    }
    return name;
  }

  static double _columnWidth(String heading) =>
      (heading.length + 6).clamp(16, 36).toDouble();

  static String _spreadsheetSafeText(String value) =>
      RegExp(r'^[\t\r\n ]*[=+\-@]').hasMatch(value) ? "'$value" : value;

  static String _wordTableRow(List<String> values, {bool header = false}) =>
      '<w:tr>${header ? '<w:trPr><w:tblHeader/></w:trPr>' : ''}'
      '${values.map((value) => '<w:tc><w:tcPr>${header ? '<w:shd w:fill="176B5B"/>' : ''}'
          '</w:tcPr><w:p><w:r>${header ? '<w:rPr><w:b/><w:color w:val="FFFFFF"/></w:rPr>' : ''}'
          '<w:t xml:space="preserve">${_xmlEscape(value)}</w:t></w:r></w:p></w:tc>').join()}'
      '</w:tr>';

  static String _xmlEscape(String value) => value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&apos;');

  static void _checkFileSize(int bytes) {
    if (bytes > maxFileBytes) {
      throw const FormatException('The file exceeds the 20 MB import limit.');
    }
    if (bytes == 0) throw const FormatException('The selected file is empty.');
  }

  static void _checkXlsxArchive(Uint8List bytes) {
    if (bytes.length < 22) {
      throw const FormatException(
        'The Excel file is not a valid ZIP workbook.',
      );
    }
    final view = ByteData.sublistView(bytes);
    final minimumEndRecordLength = 22;
    final searchStart = (bytes.length - 65557).clamp(
      0,
      bytes.length - minimumEndRecordLength,
    );
    var endRecordOffset = -1;
    for (
      var offset = bytes.length - minimumEndRecordLength;
      offset >= searchStart;
      offset--
    ) {
      if (view.getUint32(offset, Endian.little) == 0x06054b50) {
        final commentLength = view.getUint16(offset + 20, Endian.little);
        if (offset + minimumEndRecordLength + commentLength == bytes.length) {
          endRecordOffset = offset;
          break;
        }
      }
    }
    if (endRecordOffset < 0) {
      throw const FormatException(
        'The Excel file is not a valid ZIP workbook.',
      );
    }

    final diskNumber = view.getUint16(endRecordOffset + 4, Endian.little);
    final directoryDisk = view.getUint16(endRecordOffset + 6, Endian.little);
    final diskEntries = view.getUint16(endRecordOffset + 8, Endian.little);
    final totalEntries = view.getUint16(endRecordOffset + 10, Endian.little);
    final directorySize = view.getUint32(endRecordOffset + 12, Endian.little);
    final directoryOffset = view.getUint32(endRecordOffset + 16, Endian.little);
    if (diskNumber != 0 ||
        directoryDisk != 0 ||
        diskEntries != totalEntries ||
        totalEntries == 0 ||
        totalEntries > _maxXlsxEntries ||
        directoryOffset + directorySize > endRecordOffset) {
      throw const FormatException(
        'The Excel workbook structure is unsupported.',
      );
    }

    var cursor = directoryOffset;
    var expandedBytes = 0;
    final entries = <(int, int, int, int)>[];
    for (var index = 0; index < totalEntries; index++) {
      if (cursor + 46 > directoryOffset + directorySize ||
          view.getUint32(cursor, Endian.little) != 0x02014b50) {
        throw const FormatException('The Excel workbook structure is invalid.');
      }
      final flags = view.getUint16(cursor + 8, Endian.little);
      final method = view.getUint16(cursor + 10, Endian.little);
      final compressedSize = view.getUint32(cursor + 20, Endian.little);
      final expandedSize = view.getUint32(cursor + 24, Endian.little);
      final nameLength = view.getUint16(cursor + 28, Endian.little);
      final extraLength = view.getUint16(cursor + 30, Endian.little);
      final commentLength = view.getUint16(cursor + 32, Endian.little);
      final diskStart = view.getUint16(cursor + 34, Endian.little);
      final localHeaderOffset = view.getUint32(cursor + 42, Endian.little);
      final entryEnd = cursor + 46 + nameLength + extraLength + commentLength;
      if (entryEnd > directoryOffset + directorySize ||
          (flags & 1) != 0 ||
          diskStart != 0 ||
          (method != 0 && method != 8) ||
          (compressedSize == 0 && expandedSize > 0) ||
          compressedSize == 0xffffffff ||
          expandedSize == 0xffffffff ||
          localHeaderOffset == 0xffffffff ||
          expandedSize > _maxXlsxEntryBytes ||
          expandedBytes + expandedSize > _maxExpandedXlsxBytes ||
          expandedSize > compressedSize * _maxXlsxCompressionRatio) {
        throw const FormatException(
          'The Excel workbook exceeds safe import limits or uses unsupported ZIP features.',
        );
      }
      if (localHeaderOffset + 30 > directoryOffset ||
          view.getUint32(localHeaderOffset, Endian.little) != 0x04034b50) {
        throw const FormatException('The Excel workbook structure is invalid.');
      }
      final localFlags = view.getUint16(localHeaderOffset + 6, Endian.little);
      final localMethod = view.getUint16(localHeaderOffset + 8, Endian.little);
      final localNameLength = view.getUint16(
        localHeaderOffset + 26,
        Endian.little,
      );
      final localExtraLength = view.getUint16(
        localHeaderOffset + 28,
        Endian.little,
      );
      final dataOffset =
          localHeaderOffset + 30 + localNameLength + localExtraLength;
      if ((localFlags & 1) != 0 ||
          localMethod != method ||
          dataOffset + compressedSize > directoryOffset) {
        throw const FormatException('The Excel workbook structure is invalid.');
      }
      entries.add((dataOffset, compressedSize, expandedSize, method));
      cursor = entryEnd;
    }
    if (cursor != directoryOffset + directorySize) {
      throw const FormatException('The Excel workbook structure is invalid.');
    }

    for (final (dataOffset, compressedSize, expectedSize, method) in entries) {
      if (method == 0) {
        if (compressedSize != expectedSize) {
          throw const FormatException(
            'The Excel workbook structure is invalid.',
          );
        }
        expandedBytes += compressedSize;
        continue;
      }
      final remainingTotal = _maxExpandedXlsxBytes - expandedBytes;
      final maximumEntryBytes = remainingTotal < _maxXlsxEntryBytes
          ? remainingTotal
          : _maxXlsxEntryBytes;
      final counter = _ExpandedBytesCounter(maximumEntryBytes);
      final inflater = io.ZLibDecoder(raw: true)
          .startChunkedConversion(counter);
      inflater.add(
        Uint8List.sublistView(bytes, dataOffset, dataOffset + compressedSize),
      );
      inflater.close();
      if (counter.bytes != expectedSize) {
        throw const FormatException('The Excel workbook structure is invalid.');
      }
      expandedBytes += counter.bytes;
    }
  }
}
