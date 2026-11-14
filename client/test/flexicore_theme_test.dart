import 'package:flexicore_client/core/flexicore_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('light and dark themes share the FlexiCore design foundation', () {
    final light = FlexiCoreTheme.light;
    final dark = FlexiCoreTheme.dark;

    expect(FlexiCoreTheme.brand, const Color(0xFF125352));
    expect(FlexiCorePalette.primary950, const Color(0xFF082C2C));
    expect(FlexiCorePalette.primary500, const Color(0xFF269C93));
    expect(FlexiCorePalette.luminousAqua, const Color(0xFF45D6C3));
    expect(light.brightness, Brightness.light);
    expect(dark.brightness, Brightness.dark);
    expect(light.colorScheme.primary, FlexiCoreTheme.brand);
    expect(light.scaffoldBackgroundColor, FlexiCorePalette.lightBackground);
    expect(dark.scaffoldBackgroundColor, FlexiCorePalette.darkBackground);
    expect(light.colorScheme.surface, FlexiCorePalette.lightSurface);
    expect(dark.colorScheme.surface, FlexiCorePalette.darkSurface);
    expect(light.cardTheme.shape, isA<RoundedRectangleBorder>());
    expect(dark.cardTheme.shape, isA<RoundedRectangleBorder>());
    expect(light.inputDecorationTheme.filled, isTrue);
    expect(dark.inputDecorationTheme.filled, isTrue);
    expect(light.dataTableTheme.dataRowMinHeight, 56);
    expect(dark.dataTableTheme.dataRowMinHeight, 56);
  });

  test('brand gradients, elevation, and motion use centralized tokens', () {
    expect(FlexiCoreGradients.primary.colors, [
      FlexiCorePalette.primary950,
      FlexiCorePalette.primary800,
      FlexiCorePalette.primary500,
    ]);
    expect(
      FlexiCoreGradients.accent.colors.last,
      FlexiCorePalette.luminousAqua,
    );
    expect(FlexiCoreElevation.xs.single.blurRadius, 3);
    expect(FlexiCoreElevation.medium.single.blurRadius, 28);
    expect(FlexiCoreMotion.fast, const Duration(milliseconds: 160));
    expect(FlexiCoreMotion.navigation, const Duration(milliseconds: 260));
  });

  test('buttons and controls meet the shared touch-target sizing', () {
    final minimumSize = FlexiCoreTheme
        .light
        .filledButtonTheme
        .style!
        .minimumSize!
        .resolve(const {});

    expect(minimumSize, const Size(0, 48));
    expect(
      FlexiCoreTheme.light.materialTapTargetSize,
      MaterialTapTargetSize.padded,
    );
    expect(
      FlexiCoreTheme.dark.materialTapTargetSize,
      MaterialTapTargetSize.padded,
    );
  });
}
