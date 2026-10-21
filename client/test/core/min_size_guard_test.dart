import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/core/widgets/app_widgets.dart';

void _size(WidgetTester t, double w, double h) {
  t.view.physicalSize = Size(w, h);
  t.view.devicePixelRatio = 1;
  addTearDown(t.view.resetPhysicalSize);
  addTearDown(t.view.resetDevicePixelRatio);
}

Widget _app() => MaterialApp(
  builder: (context, child) => MinSizeGuard(child: child!),
  home: const Scaffold(
    body: Row(
      children: [
        SizedBox(width: 250, height: 20),
        SizedBox(width: 50, height: 20),
      ],
    ),
  ),
);

void main() {
  testWidgets('a tiny window is laid out at the minimum width', (t) async {
    _size(t, 120, 600);
    await t.pumpWidget(_app());
    expect(t.takeException(), isNull);
    expect(find.byType(SingleChildScrollView), findsWidgets);
  });

  testWidgets('a normal window is left alone', (t) async {
    _size(t, 800, 600);
    await t.pumpWidget(_app());
    expect(t.takeException(), isNull);
    expect(find.byType(SingleChildScrollView), findsNothing);
  });
}
