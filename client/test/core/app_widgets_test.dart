import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/core/app_theme.dart';
import 'package:flexicore_client/core/widgets/app_widgets.dart';

Widget _app(Widget child) => MaterialApp(theme: buildAppTheme(), home: child);

void _size(WidgetTester t, double w, double h) {
  t.view.physicalSize = Size(w, h);
  t.view.devicePixelRatio = 1;
  addTearDown(t.view.resetPhysicalSize);
  addTearDown(t.view.resetDevicePixelRatio);
}

void main() {
  testWidgets('PageHeader does not overflow on a narrow phone', (t) async {
    _size(t, 320, 640);
    await t.pumpWidget(
      _app(
        Scaffold(
          body: Column(
            children: [
              PageHeader(
                title: 'A very long business name that must be cut with an ellipsis',
                subtitle: 'Warehouse: A very long warehouse name for testing',
                actions: [
                  IconButton(onPressed: () {}, icon: const Icon(Icons.tune)),
                  IconButton(onPressed: () {}, icon: const Icon(Icons.search)),
                ],
                stats: const [
                  HeaderStat(label: 'Products', value: '12,345'),
                  HeaderStat(label: 'In stock', value: '9,876'),
                  HeaderStat(label: 'Out of stock', value: '2,469'),
                ],
              ),
            ],
          ),
        ),
      ),
    );
    expect(t.takeException(), isNull);
  });

  testWidgets('CardGrid: one column on a phone, several on desktop', (t) async {
    Widget grid() => _app(
      const Scaffold(
        body: SingleChildScrollView(
          child: CardGrid(
            children: [
              StatCard(icon: Icons.sell, label: 'A', value: '1'),
              StatCard(icon: Icons.sell, label: 'B', value: '2'),
              StatCard(icon: Icons.sell, label: 'C', value: '3'),
            ],
          ),
        ),
      ),
    );
    _size(t, 360, 740);
    await t.pumpWidget(grid());
    expect(
      t.getTopLeft(find.text('B')).dy,
      greaterThan(t.getTopLeft(find.text('A')).dy),
    );
    _size(t, 1200, 800);
    await t.pumpWidget(grid());
    await t.pump();
    expect(t.getTopLeft(find.text('B')).dy, t.getTopLeft(find.text('A')).dy);
    expect(t.takeException(), isNull);
  });

  testWidgets('StatCard calls onTap', (t) async {
    var taps = 0;
    await t.pumpWidget(
      _app(
        Scaffold(
          body: StatCard(
            icon: Icons.sell,
            label: 'Products',
            value: '5',
            onTap: () => taps++,
          ),
        ),
      ),
    );
    await t.tap(find.text('Products'));
    expect(taps, 1);
  });

  testWidgets('EmptyState shows its action and InlineError its message', (
    t,
  ) async {
    await t.pumpWidget(
      _app(
        Scaffold(
          body: Column(
            children: [
              Expanded(
                child: EmptyState(
                  icon: Icons.inbox,
                  title: 'Nothing here',
                  message: 'Add something first.',
                  action: FilledButton(
                    onPressed: () {},
                    child: const Text('Add'),
                  ),
                ),
              ),
              const InlineError('Something went wrong'),
            ],
          ),
        ),
      ),
    );
    expect(find.text('Nothing here'), findsOneWidget);
    expect(find.text('Add'), findsOneWidget);
    expect(find.text('Something went wrong'), findsOneWidget);
    expect(t.takeException(), isNull);
  });
}
