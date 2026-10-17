import 'dart:convert';
import 'dart:typed_data';

import 'package:archive/archive.dart';
import 'package:excel/excel.dart';
import 'package:flexicore_client/features/workspace/spreadsheet_exchange.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:xml/xml.dart';

void main() {
  test('XLSX export reopens with named sheet, styled heading, filter and frozen row', () {
    final bytes = SpreadsheetExchange.encodeXlsx(
      sheetName: 'Organizations',
      headers: SpreadsheetExchange.organizationHeaders,
      rows: [
        ['Example Org', 'example-org'],
        ['=HYPERLINK("https://bad.example")', 'safe-slug'],
      ],
    );

    final workbook = Excel.decodeBytes(bytes);
    expect(workbook.tables.keys, contains('Organizations'));
    final sheet = workbook.tables['Organizations']!;
    expect(sheet.rows[0][0]!.value.toString(), 'Name');
    expect(sheet.rows[1][0]!.value.toString(), 'Example Org');
    expect(
      sheet.rows[2][0]!.value.toString(),
      '=HYPERLINK("https://bad.example")',
    );

    final archive = ZipDecoder().decodeBytes(bytes);
    final worksheet = utf8.decode(
      archive.findFile('xl/worksheets/sheet1.xml')!.content as List<int>,
    );
    expect(worksheet, contains('<autoFilter ref="A1:B3"'));
    expect(worksheet, contains('<pane'));
    expect(worksheet, contains('ySplit="1"'));
  });

  test('CSV export neutralizes formula-leading values and round-trips', () {
    final csv = SpreadsheetExchange.encodeCsv([
      SpreadsheetExchange.organizationHeaders,
      ['=1+1', 'acme'],
    ]);
    final parsed = SpreadsheetExchange.decodeCsv(
      Uint8List.fromList(utf8.encode(csv)),
    );

    expect(parsed.headers, ['Name', 'Slug']);
    expect(parsed.rows.single, ["'=1+1", 'acme']);
  });

  test(
    'preview detects required fields, existing duplicates and repeated rows',
    () {
      final grid = const SpreadsheetGrid(
        headers: ['Business name', 'Code', 'Industry'],
        rows: [
          ['Acme', 'acme-shop', 'retail'],
          ['Acme duplicate', 'acme-shop', 'retail'],
          ['Existing company', 'existing', 'retail'],
          ['Bad industry', 'bad-industry', 'Retail!'],
        ],
      );

      final preview = SpreadsheetExchange.previewImport(
        grid: grid,
        entity: ImportEntity.companies,
        columnMap: {'name': 0, 'slug': 1, 'industryKey': 2},
        existingSlugs: {'existing'},
      );

      expect(preview[0].isValid, isTrue);
      expect(preview[1].errors, contains('Slug is duplicated in this file.'));
      expect(
        preview[2].errors,
        contains('Slug already exists in this workspace.'),
      );
      expect(preview[3].errors.single, contains('Industry key'));
    },
  );

  test('invalid rows do not reserve slugs during duplicate detection', () {
    final preview = SpreadsheetExchange.previewImport(
      grid: const SpreadsheetGrid(
        headers: ['Name', 'Slug', 'Industry'],
        rows: [
          ['Bad industry row', 'shared-slug', 'Retail!'],
          ['Valid row', 'shared-slug', 'retail'],
        ],
      ),
      entity: ImportEntity.companies,
      columnMap: {'name': 0, 'slug': 1, 'industryKey': 2},
      existingSlugs: {},
    );

    expect(preview.first.isValid, isFalse);
    expect(preview.last.isValid, isTrue);
  });

  test(
    'formula cells are rejected on import and column maps cannot collide',
    () {
      final workbook = Excel.createExcel();
      final sheet = workbook['Sheet1'];
      sheet.appendRow([TextCellValue('Name'), TextCellValue('Slug')]);
      sheet.appendRow([TextCellValue('Acme'), TextCellValue('acme')]);
      sheet.cell(CellIndex.indexByString('A2')).setFormula('1+1');
      final bytes = Uint8List.fromList(workbook.save()!);
      expect(
        () => SpreadsheetExchange.decodeXlsx(bytes),
        throwsA(isA<FormatException>()),
      );

      expect(
        () => SpreadsheetExchange.previewImport(
          grid: const SpreadsheetGrid(
            headers: ['Name', 'Slug'],
            rows: [
              ['Acme', 'acme'],
            ],
          ),
          entity: ImportEntity.organizations,
          columnMap: {'name': 0, 'slug': 0},
          existingSlugs: {},
        ),
        throwsA(isA<FormatException>()),
      );
    },
  );

  test(
    'DOCX export reopens as a valid package with escaped report content',
    () {
      final bytes = SpreadsheetExchange.encodeDocx(
        title: 'Workspace <directory>',
        headers: const ['Name', 'Slug'],
        rows: const [
          ['Acme & Sons', 'acme-sons'],
        ],
        generatedOn: DateTime.utc(2025, 1, 2),
      );
      final archive = ZipDecoder().decodeBytes(bytes);
      final requiredParts = [
        '[Content_Types].xml',
        '_rels/.rels',
        'word/document.xml',
        'word/styles.xml',
        'word/_rels/document.xml.rels',
      ];
      for (final part in requiredParts) {
        expect(archive.findFile(part), isNotNull, reason: part);
      }

      for (final part in requiredParts) {
        XmlDocument.parse(
          utf8.decode(archive.findFile(part)!.content as List<int>),
        );
      }
      final document = utf8.decode(
        archive.findFile('word/document.xml')!.content as List<int>,
      );
      expect(document, contains('Workspace &lt;directory&gt;'));
      expect(document, contains('Acme &amp; Sons'));
      expect(document, contains('Generated 2025-01-02'));
      expect(document, contains('<w:tblHeader/>'));
    },
  );

  test('XLSX import rejects files that are not ZIP workbooks', () {
    expect(
      () => SpreadsheetExchange.decodeXlsx(Uint8List.fromList([1, 2, 3])),
      throwsA(isA<FormatException>()),
    );
  });

  test(
    'XLSX import rejects oversized expanded ZIP entries before decoding',
    () {
      final bytes = SpreadsheetExchange.encodeXlsx(
        sheetName: 'Organizations',
        headers: const ['Name', 'Slug'],
        rows: const [],
      );
      final view = ByteData.sublistView(bytes);
      var endRecordOffset = bytes.length - 22;
      while (view.getUint32(endRecordOffset, Endian.little) != 0x06054b50) {
        endRecordOffset--;
      }
      final directoryOffset = view.getUint32(
        endRecordOffset + 16,
        Endian.little,
      );
      view.setUint32(directoryOffset + 24, 33 * 1024 * 1024, Endian.little);

      expect(
        () => SpreadsheetExchange.decodeXlsx(bytes),
        throwsA(
          isA<FormatException>().having(
            (error) => error.message,
            'message',
            contains('safe import limits'),
          ),
        ),
      );
    },
  );

  test('XLSX import caps actual expansion when ZIP sizes are understated', () {
    const expandedSize = 33 * 1024 * 1024;
    final archive = Archive()
      ..addFile(ArchiveFile.string('xl/sharedStrings.xml', 'x' * expandedSize));
    final bytes = Uint8List.fromList(ZipEncoder().encode(archive)!);
    final view = ByteData.sublistView(bytes);
    var endRecordOffset = bytes.length - 22;
    while (view.getUint32(endRecordOffset, Endian.little) != 0x06054b50) {
      endRecordOffset--;
    }
    final directoryOffset = view.getUint32(endRecordOffset + 16, Endian.little);
    final localHeaderOffset = view.getUint32(
      directoryOffset + 42,
      Endian.little,
    );
    view.setUint32(directoryOffset + 24, 1, Endian.little);
    view.setUint32(localHeaderOffset + 22, 1, Endian.little);

    expect(bytes.length, lessThan(SpreadsheetExchange.maxFileBytes));
    expect(
      () => SpreadsheetExchange.decodeXlsx(bytes),
      throwsA(
        isA<FormatException>().having(
          (error) => error.message,
          'message',
          contains('safe import limits'),
        ),
      ),
    );
  });
}
