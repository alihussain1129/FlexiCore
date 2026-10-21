import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:flexicore_client/core/app_theme.dart';
import 'package:flexicore_client/features/shell/app_shell.dart';

void main() {
  testWidgets('the bottom bar is hidden while the keyboard is open', (t) async {
    t.view.physicalSize = const Size(360, 740);
    t.view.devicePixelRatio = 1;
    addTearDown(t.view.resetPhysicalSize);
    addTearDown(t.view.resetDevicePixelRatio);
    addTearDown(t.view.resetViewInsets);

    await t.pumpWidget(
      MaterialApp(
        theme: buildAppTheme(),
        home: AppShell(
          destinations: [
            for (final n in ['One', 'Two'])
              ShellDestination(
                label: n,
                icon: Icons.circle_outlined,
                selectedIcon: Icons.circle,
                builder: (_) => Center(child: Text('Page $n')),
              ),
          ],
        ),
      ),
    );
    expect(find.byType(NavigationBar), findsOneWidget);

    t.view.viewInsets = const FakeViewPadding(bottom: 300);
    await t.pump();
    expect(find.byType(NavigationBar), findsNothing);
    expect(find.text('Page One'), findsOneWidget);

    t.view.resetViewInsets();
    await t.pump();
    expect(find.byType(NavigationBar), findsOneWidget);
    expect(t.takeException(), isNull);
  });
}
