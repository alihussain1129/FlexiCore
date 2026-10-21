import 'package:flutter/material.dart';

import '../../core/app_theme.dart';
import '../../core/widgets/app_widgets.dart';

class ShellDestination {
  const ShellDestination({
    required this.label,
    required this.icon,
    required this.selectedIcon,
    required this.builder,
  });

  final String label;
  final IconData icon;
  final IconData selectedIcon;
  final WidgetBuilder builder;
}

/// Lets a page ask the shell to open another destination.
class ShellScope extends InheritedWidget {
  const ShellScope({super.key, required this.goTo, required super.child});

  final void Function(int index) goTo;

  static ShellScope? maybeOf(BuildContext context) =>
      context.getInheritedWidgetOfExactType<ShellScope>();

  @override
  bool updateShouldNotify(ShellScope oldWidget) => false;
}

/// Responsive navigation: a side rail on wide windows, a bottom bar on
/// phones. Pages are built the first time they are opened, then kept alive.
class AppShell extends StatefulWidget {
  const AppShell({
    super.key,
    required this.destinations,
    this.initialIndex = 0,
    this.onSelected,
  });

  final List<ShellDestination> destinations;
  final int initialIndex;
  final ValueChanged<int>? onSelected;

  @override
  State<AppShell> createState() => _AppShellState();
}

class _AppShellState extends State<AppShell> {
  late int _index = widget.initialIndex;
  final Set<int> _built = {};
  final GlobalKey _bodyKey = GlobalKey();

  void _go(int i) {
    if (i == _index || i < 0 || i >= widget.destinations.length) return;
    setState(() => _index = i);
    widget.onSelected?.call(i);
  }

  Widget _brandIcon() => Container(
    width: 40,
    height: 40,
    decoration: BoxDecoration(
      gradient: const LinearGradient(
        begin: Alignment.topLeft,
        end: Alignment.bottomRight,
        colors: [AppColors.tealDark, AppColors.teal],
      ),
      borderRadius: BorderRadius.circular(12),
    ),
    child: const Icon(Icons.storefront_outlined, color: Colors.white, size: 22),
  );

  @override
  Widget build(BuildContext context) {
    _built.add(_index);
    final width = MediaQuery.sizeOf(context).width;
    final useRail = width >= Breakpoints.rail;
    final extended = width >= Breakpoints.wideRail;
    final keyboardOpen = MediaQuery.viewInsetsOf(context).bottom > 0;
    final dests = widget.destinations;

    final body = KeyedSubtree(
      key: _bodyKey,
      child: IndexedStack(
        index: _index,
        children: [
          for (var i = 0; i < dests.length; i++)
            _built.contains(i)
                ? Builder(builder: dests[i].builder)
                : const SizedBox.shrink(),
        ],
      ),
    );

    return ShellScope(
      goTo: _go,
      child: Scaffold(
        backgroundColor: AppColors.paper,
        body: useRail
            ? Row(
                children: [
                  DecoratedBox(
                    decoration: const BoxDecoration(
                      color: Colors.white,
                      border: Border(right: BorderSide(color: AppColors.line)),
                    ),
                    child: SafeArea(
                      right: false,
                      child: NavigationRail(
                        selectedIndex: _index,
                        extended: extended,
                        onDestinationSelected: _go,
                        labelType: extended
                            ? NavigationRailLabelType.none
                            : NavigationRailLabelType.all,
                        backgroundColor: Colors.white,
                        indicatorColor: AppColors.tealSoft,
                        selectedIconTheme: const IconThemeData(
                          color: AppColors.teal,
                        ),
                        unselectedIconTheme: const IconThemeData(
                          color: AppColors.muted,
                        ),
                        selectedLabelTextStyle: const TextStyle(
                          color: AppColors.teal,
                          fontWeight: FontWeight.w700,
                        ),
                        unselectedLabelTextStyle: const TextStyle(
                          color: AppColors.muted,
                        ),
                        leading: Padding(
                          padding: const EdgeInsets.symmetric(vertical: 16),
                          child: extended
                              ? Row(
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    _brandIcon(),
                                    const SizedBox(width: 10),
                                    const Text(
                                      'FlexiCore',
                                      style: TextStyle(
                                        fontSize: 18,
                                        fontWeight: FontWeight.w700,
                                      ),
                                    ),
                                  ],
                                )
                              : _brandIcon(),
                        ),
                        destinations: [
                          for (final d in dests)
                            NavigationRailDestination(
                              icon: Icon(d.icon),
                              selectedIcon: Icon(d.selectedIcon),
                              label: Text(d.label),
                            ),
                        ],
                      ),
                    ),
                  ),
                  Expanded(child: body),
                ],
              )
            : body,
        bottomNavigationBar: (useRail || keyboardOpen)
            ? null
            : NavigationBarTheme(
                data: const NavigationBarThemeData(
                  backgroundColor: Colors.white,
                  surfaceTintColor: Colors.transparent,
                  indicatorColor: AppColors.tealSoft,
                ),
                child: NavigationBar(
                  selectedIndex: _index,
                  onDestinationSelected: _go,
                  destinations: [
                    for (final d in dests)
                      NavigationDestination(
                        icon: Icon(d.icon),
                        selectedIcon: Icon(d.selectedIcon),
                        label: d.label,
                      ),
                  ],
                ),
              ),
      ),
    );
  }
}
