import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/core/app_theme.dart';
import 'package:flexicore_client/features/shell/app_shell.dart';

Widget _app(Widget child) => MaterialApp(theme: buildAppTheme(), home: child);

AppShell _shell() => AppShell(
  destinations: [
    ShellDestination(
      label: 'One',
      icon: Icons.looks_one_outlined,
      selectedIcon: Icons.looks_one,
      builder: (_) => const Center(child: Text('Page one')),
    ),
    ShellDestination(
      label: 'Two',
      icon: Icons.looks_two_outlined,
      selectedIcon: Icons.looks_two,
      builder: (_) => const Center(child: Text('Page two')),
    ),
    ShellDestination(
      label: 'Three',
      icon: Icons.looks_3_outlined,
      selectedIcon: Icons.looks_3,
      builder: (_) => const Center(child: Text('Page three')),
    ),
  ],
);

void _size(WidgetTester t, double w, double h) {
  t.view.physicalSize = Size(w, h);
  t.view.devicePixelRatio = 1;
  addTearDown(t.view.resetPhysicalSize);
  addTearDown(t.view.resetDevicePixelRatio);
}

void main() {
  testWidgets('phone width uses the bottom bar', (t) async {
    _size(t, 360, 740);
    await t.pumpWidget(_app(_shell()));
    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.byType(NavigationRail), findsNothing);
    expect(find.text('Page one'), findsOneWidget);
    await t.tap(find.text('Two'));
    await t.pump();
    expect(find.text('Page two'), findsOneWidget);
    expect(t.takeException(), isNull);
  });

  testWidgets('tablet width uses a compact rail', (t) async {
    _size(t, 800, 900);
    await t.pumpWidget(_app(_shell()));
    expect(find.byType(NavigationRail), findsOneWidget);
    expect(find.byType(NavigationBar), findsNothing);
    expect(t.takeException(), isNull);
  });

  testWidgets('wide window shows the extended rail with the brand', (t) async {
    _size(t, 1400, 900);
    await t.pumpWidget(_app(_shell()));
    expect(find.byType(NavigationRail), findsOneWidget);
    expect(find.text('FlexiCore'), findsOneWidget);
    await t.tap(find.text('Three'));
    await t.pump();
    expect(find.text('Page three'), findsOneWidget);
    expect(t.takeException(), isNull);
  });

  testWidgets('resizing keeps the selected page', (t) async {
    _size(t, 1400, 900);
    await t.pumpWidget(_app(_shell()));
    await t.tap(find.text('Two'));
    await t.pump();
    _size(t, 360, 740);
    await t.pump();
    expect(find.byType(NavigationBar), findsOneWidget);
    expect(find.text('Page two'), findsOneWidget);
    expect(t.takeException(), isNull);
  });

  testWidgets('a page can ask the shell to open another page', (t) async {
    _size(t, 360, 740);
    await t.pumpWidget(
      _app(
        AppShell(
          destinations: [
            ShellDestination(
              label: 'One',
              icon: Icons.looks_one_outlined,
              selectedIcon: Icons.looks_one,
              builder: (context) => Center(
                child: TextButton(
                  onPressed: () => ShellScope.maybeOf(context)?.goTo(1),
                  child: const Text('Go'),
                ),
              ),
            ),
            ShellDestination(
              label: 'Two',
              icon: Icons.looks_two_outlined,
              selectedIcon: Icons.looks_two,
              builder: (_) => const Center(child: Text('Page two')),
            ),
          ],
        ),
      ),
    );
    await t.tap(find.text('Go'));
    await t.pump();
    expect(find.text('Page two'), findsOneWidget);
  });
}
