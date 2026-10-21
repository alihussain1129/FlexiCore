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
  testWidgets('StatCard does not overflow when it is very narrow', (t) async {
    _size(t, 400, 600);
    await t.pumpWidget(
      _app(
        const Scaffold(
          body: Center(
            child: SizedBox(
              width: 103,
              child: StatCard(
                icon: Icons.straighten,
                label: 'Units of measure',
                value: '12',
                caption: 'For pricing and stock',
                onTap: null,
              ),
            ),
          ),
        ),
      ),
    );
    expect(t.takeException(), isNull);
  });

  testWidgets('PageHeader shows its bottom widget without overflow', (t) async {
    _size(t, 320, 640);
    await t.pumpWidget(
      _app(
        const Scaffold(
          body: Column(
            children: [
              PageHeader(
                title: 'Shop',
                subtitle: 'Your products and units',
                bottom: SizedBox(height: 42, child: Text('Bottom slot')),
              ),
            ],
          ),
        ),
      ),
    );
    expect(find.text('Bottom slot'), findsOneWidget);
    expect(t.takeException(), isNull);
  });
}
