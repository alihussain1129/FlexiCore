import 'package:flutter/material.dart';

import '../../core/app_theme.dart';
import '../../core/widgets/app_widgets.dart';
import '../catalog/presentation/catalog_controller.dart';
import '../profile/presentation/profile_controller.dart';
import '../shell/app_shell.dart';

/// Home page. Every number comes from the local database:
///  - Products: active catalog products (reflects the Products search box
///    if a search is active, and says so).
///  - Units of measure: active units.
class OverviewScreen extends StatelessWidget {
  const OverviewScreen({
    super.key,
    required this.profile,
    required this.catalog,
    this.actions = const [],
    this.productsIndex = 1,
  });

  final ProfileController profile;
  final CatalogController catalog;
  final List<Widget> actions;
  final int productsIndex;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: Listenable.merge([profile, catalog]),
      builder: (context, _) {
        final p = profile.profile;
        final name = (p == null || p.businessName.trim().isEmpty)
            ? 'My business'
            : p.businessName.trim();
        final subtitle = [
          if (p != null && p.industry.trim().isNotEmpty) p.industry.trim(),
          if (p != null && p.currency.trim().isNotEmpty) p.currency.trim(),
        ].join('  |  ');
        final filtered = catalog.query.trim().isNotEmpty;
        final h = pagePadding(context);
        void go() => ShellScope.maybeOf(context)?.goTo(productsIndex);

        return Column(
          children: [
            PageHeader(
              title: name,
              subtitle: subtitle.isEmpty ? 'Overview' : subtitle,
              icon: Icons.dashboard_outlined,
              actions: actions,
            ),
            Expanded(
              child: ListView(
                padding: EdgeInsets.fromLTRB(h, 20, h, 28),
                children: [
                  Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(
                        maxWidth: Breakpoints.content,
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          if (catalog.loadError != null) ...[
                            InlineError(catalog.loadError!),
                            const SizedBox(height: 14),
                          ],
                          const Text(
                            'At a glance',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                          const SizedBox(height: 12),
                          CardGrid(
                            children: [
                              StatCard(
                                icon: Icons.sell_outlined,
                                label: 'Products',
                                value: '${catalog.products.length}',
                                caption: filtered
                                    ? 'Filtered by your search'
                                    : 'In your catalog',
                                onTap: go,
                              ),
                              StatCard(
                                icon: Icons.straighten,
                                label: 'Units of measure',
                                value: '${catalog.units.length}',
                                caption: 'For pricing and stock',
                                onTap: go,
                              ),
                            ],
                          ),
                          const SizedBox(height: 22),
                          if (!catalog.loading &&
                              !filtered &&
                              catalog.products.isEmpty) ...[
                            SectionCard(
                              title: 'Get started',
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  const Text(
                                    'Your catalog is empty. Add your units of '
                                    'measure and your first products.',
                                    style: TextStyle(color: AppColors.muted),
                                  ),
                                  const SizedBox(height: 14),
                                  FilledButton(
                                    onPressed: go,
                                    child: const Text('Add your first product'),
                                  ),
                                ],
                              ),
                            ),
                            const SizedBox(height: 16),
                          ],
                          SectionCard(
                            title: 'Quick actions',
                            child: InkWell(
                              onTap: go,
                              borderRadius: BorderRadius.circular(12),
                              child: Padding(
                                padding: const EdgeInsets.symmetric(
                                  vertical: 6,
                                ),
                                child: Row(
                                  children: [
                                    Container(
                                      width: 40,
                                      height: 40,
                                      decoration: BoxDecoration(
                                        color: AppColors.tealSoft,
                                        borderRadius: BorderRadius.circular(12),
                                      ),
                                      child: const Icon(
                                        Icons.add_box_outlined,
                                        color: AppColors.teal,
                                        size: 22,
                                      ),
                                    ),
                                    const SizedBox(width: 12),
                                    const Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                            'Manage products and units',
                                            maxLines: 1,
                                            overflow: TextOverflow.ellipsis,
                                            style: TextStyle(
                                              fontWeight: FontWeight.w600,
                                            ),
                                          ),
                                          Text(
                                            'Add, edit, search or remove items',
                                            maxLines: 2,
                                            overflow: TextOverflow.ellipsis,
                                            style: TextStyle(
                                              color: AppColors.muted,
                                              fontSize: 12,
                                            ),
                                          ),
                                        ],
                                      ),
                                    ),
                                    const Icon(
                                      Icons.chevron_right,
                                      color: AppColors.muted,
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                          const SizedBox(height: 18),
                          const Text(
                            'FlexiCore works offline. Your data is stored on '
                            'this device. Use the menu at the top right to '
                            'back it up.',
                            style: TextStyle(
                              color: AppColors.muted,
                              fontSize: 12.5,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        );
      },
    );
  }
}
