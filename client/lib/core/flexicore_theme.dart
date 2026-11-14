import 'package:flutter/material.dart';

abstract final class FlexiCorePalette {
  static const Color primary950 = Color(0xFF082C2C);
  static const Color primary900 = Color(0xFF0B3D3C);
  static const Color primary800 = Color(0xFF125352);
  static const Color primary700 = Color(0xFF176A67);
  static const Color primary600 = Color(0xFF1D817B);
  static const Color primary500 = Color(0xFF269C93);
  static const Color primary400 = Color(0xFF39B9AB);
  static const Color primary300 = Color(0xFF65D0C1);
  static const Color primary200 = Color(0xFF9BE4D8);
  static const Color primary100 = Color(0xFFC9F1E9);
  static const Color primary50 = Color(0xFFEAF8F5);
  static const Color primary25 = Color(0xFFF5FBF9);

  static const Color luminousAqua = Color(0xFF45D6C3);
  static const Color softMint = Color(0xFFA5E9D9);
  static const Color paleMint = Color(0xFFD6F4EB);
  static const Color premiumGold = Color(0xFFE6B86A);
  static const Color slateBlue = Color(0xFF6B83B9);
  static const Color softViolet = Color(0xFFB5A1D8);

  static const Color lightBackground = Color(0xFFF3F8F7);
  static const Color lightSurface = Color(0xFFFFFFFF);
  static const Color lightSecondarySurface = Color(0xFFEAF4F2);
  static const Color lightBorder = Color(0xFFDCE9E6);
  static const Color lightTextPrimary = Color(0xFF142D2C);
  static const Color lightTextSecondary = Color(0xFF607775);

  static const Color darkBackground = Color(0xFF071F20);
  static const Color darkSurface = Color(0xFF0D3030);
  static const Color darkElevatedSurface = Color(0xFF164342);
  static const Color darkTextPrimary = Color(0xFFF2FAF8);
  static const Color darkTextSecondary = Color(0xFFB7D4CD);

  static const Color success = Color(0xFF16866A);
  static const Color warning = Color(0xFFB7791F);
  static const Color error = Color(0xFFC84F58);
  static const Color information = Color(0xFF277DA1);
  static const Color scrim = Color(0x99071F20);
}

abstract final class FlexiCoreSpacing {
  static const double xSmall = 4;
  static const double small = 8;
  static const double medium = 16;
  static const double large = 24;
  static const double xLarge = 32;
  static const double xxLarge = 40;
  static const double xxxLarge = 56;
}

abstract final class FlexiCoreRadius {
  static const double xSmall = 6;
  static const double small = 10;
  static const double medium = 14;
  static const double large = 20;
  static const double xLarge = 24;
  static const double pill = 999;
}

abstract final class FlexiCoreGradients {
  static const LinearGradient primary = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      FlexiCorePalette.primary950,
      FlexiCorePalette.primary800,
      FlexiCorePalette.primary500,
    ],
  );
  static const LinearGradient accent = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      FlexiCorePalette.primary800,
      FlexiCorePalette.primary500,
      FlexiCorePalette.luminousAqua,
    ],
  );
  static const LinearGradient light = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      FlexiCorePalette.primary50,
      FlexiCorePalette.lightSurface,
      FlexiCorePalette.paleMint,
    ],
  );
  static const LinearGradient dark = LinearGradient(
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
    colors: [
      FlexiCorePalette.darkBackground,
      FlexiCorePalette.darkSurface,
      FlexiCorePalette.darkElevatedSurface,
    ],
  );
}

abstract final class FlexiCoreElevation {
  static const List<BoxShadow> xs = [
    BoxShadow(
      color: Color.fromRGBO(18, 83, 82, 0.06),
      offset: Offset(0, 1),
      blurRadius: 3,
    ),
  ];
  static const List<BoxShadow> small = [
    BoxShadow(
      color: Color.fromRGBO(18, 83, 82, 0.08),
      offset: Offset(0, 4),
      blurRadius: 12,
    ),
  ];
  static const List<BoxShadow> medium = [
    BoxShadow(
      color: Color.fromRGBO(18, 83, 82, 0.12),
      offset: Offset(0, 10),
      blurRadius: 28,
    ),
  ];
  static const List<BoxShadow> large = [
    BoxShadow(
      color: Color.fromRGBO(7, 31, 32, 0.16),
      offset: Offset(0, 18),
      blurRadius: 45,
    ),
  ];
  static const List<BoxShadow> glow = [
    BoxShadow(color: Color.fromRGBO(69, 214, 195, 0.22), blurRadius: 24),
  ];
}

abstract final class FlexiCoreMotion {
  static const Duration fast = Duration(milliseconds: 160);
  static const Duration standard = Duration(milliseconds: 220);
  static const Duration navigation = Duration(milliseconds: 260);
  static const Duration large = Duration(milliseconds: 360);
  static const Duration chartEntrance = Duration(milliseconds: 620);
  static const Curve standardCurve = Curves.easeOutCubic;
  static const Curve emphasizedCurve = Curves.easeInOutCubic;

  static Duration duration(BuildContext context, Duration duration) =>
      MediaQuery.of(context).disableAnimations ? Duration.zero : duration;

  static Duration platformDuration(Duration duration) =>
      WidgetsBinding
          .instance
          .platformDispatcher
          .accessibilityFeatures
          .disableAnimations
      ? Duration.zero
      : duration;
}

class FlexiCoreTheme {
  static const Color brand = FlexiCorePalette.primary800;

  static final ThemeData light = _build(Brightness.light);
  static final ThemeData dark = _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final isLight = brightness == Brightness.light;
    final scheme =
        ColorScheme.fromSeed(seedColor: brand, brightness: brightness).copyWith(
          primary: isLight
              ? FlexiCorePalette.primary800
              : FlexiCorePalette.primary300,
          onPrimary: isLight
              ? FlexiCorePalette.lightSurface
              : FlexiCorePalette.primary950,
          primaryContainer: isLight
              ? FlexiCorePalette.primary100
              : FlexiCorePalette.darkElevatedSurface,
          onPrimaryContainer: isLight
              ? FlexiCorePalette.primary950
              : FlexiCorePalette.primary100,
          secondary: isLight
              ? FlexiCorePalette.primary700
              : FlexiCorePalette.primary200,
          onSecondary: isLight
              ? FlexiCorePalette.lightSurface
              : FlexiCorePalette.primary950,
          tertiary: FlexiCorePalette.luminousAqua,
          onTertiary: FlexiCorePalette.primary950,
          error: FlexiCorePalette.error,
          onError: FlexiCorePalette.lightSurface,
          surface: isLight
              ? FlexiCorePalette.lightSurface
              : FlexiCorePalette.darkSurface,
          onSurface: isLight
              ? FlexiCorePalette.lightTextPrimary
              : FlexiCorePalette.darkTextPrimary,
          surfaceContainerLowest: isLight
              ? FlexiCorePalette.lightSurface
              : FlexiCorePalette.darkBackground,
          surfaceContainerLow: isLight
              ? FlexiCorePalette.primary25
              : FlexiCorePalette.darkSurface,
          surfaceContainer: isLight
              ? FlexiCorePalette.lightSecondarySurface
              : FlexiCorePalette.darkElevatedSurface,
          surfaceContainerHigh: isLight
              ? FlexiCorePalette.primary50
              : FlexiCorePalette.primary900,
          surfaceContainerHighest: isLight
              ? FlexiCorePalette.lightBorder
              : FlexiCorePalette.primary800,
          onSurfaceVariant: isLight
              ? FlexiCorePalette.lightTextSecondary
              : FlexiCorePalette.darkTextSecondary,
          outline: isLight
              ? FlexiCorePalette.lightTextSecondary
              : FlexiCorePalette.darkTextSecondary,
          outlineVariant: isLight
              ? FlexiCorePalette.lightBorder
              : FlexiCorePalette.primary700,
          surfaceTint: isLight
              ? FlexiCorePalette.primary800
              : FlexiCorePalette.primary300,
          scrim: FlexiCorePalette.scrim,
          inverseSurface: isLight
              ? FlexiCorePalette.darkSurface
              : FlexiCorePalette.lightSurface,
          onInverseSurface: isLight
              ? FlexiCorePalette.darkTextPrimary
              : FlexiCorePalette.lightTextPrimary,
          inversePrimary: isLight
              ? FlexiCorePalette.primary300
              : FlexiCorePalette.primary800,
        );
    final radius = BorderRadius.circular(FlexiCoreRadius.medium);
    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: isLight
          ? FlexiCorePalette.lightBackground
          : FlexiCorePalette.darkBackground,
      visualDensity: VisualDensity.standard,
      materialTapTargetSize: MaterialTapTargetSize.padded,
    );
    final textTheme = base.textTheme
        .apply(bodyColor: scheme.onSurface, displayColor: scheme.onSurface)
        .copyWith(
          headlineMedium: base.textTheme.headlineMedium?.copyWith(
            fontSize: 30,
            height: 1.2,
            letterSpacing: -0.5,
            fontWeight: FontWeight.w700,
            color: scheme.onSurface,
          ),
          titleLarge: base.textTheme.titleLarge?.copyWith(
            fontSize: 20,
            height: 1.3,
            fontWeight: FontWeight.w700,
            color: scheme.onSurface,
          ),
          titleMedium: base.textTheme.titleMedium?.copyWith(
            fontWeight: FontWeight.w600,
            color: scheme.onSurface,
          ),
          labelLarge: base.textTheme.labelLarge?.copyWith(
            fontWeight: FontWeight.w600,
            color: scheme.onSurface,
          ),
        );
    final border = OutlineInputBorder(
      borderRadius: radius,
      borderSide: BorderSide(color: scheme.outlineVariant),
    );

    return base.copyWith(
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        centerTitle: false,
        toolbarHeight: 72,
        backgroundColor: isLight
            ? FlexiCorePalette.lightBackground
            : FlexiCorePalette.darkBackground,
        foregroundColor: scheme.onSurface,
        surfaceTintColor: Colors.transparent,
        scrolledUnderElevation: 0,
        titleTextStyle: textTheme.titleLarge,
      ),
      cardTheme: CardThemeData(
        color: scheme.surfaceContainerLow,
        elevation: 1,
        shadowColor: Color.fromRGBO(18, 83, 82, isLight ? 0.08 : 0.24),
        surfaceTintColor: Colors.transparent,
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FlexiCoreRadius.large),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: scheme.surfaceContainerLowest,
        isDense: false,
        floatingLabelBehavior: FloatingLabelBehavior.auto,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: FlexiCoreSpacing.medium,
          vertical: 16,
        ),
        border: border,
        enabledBorder: border,
        focusedBorder: OutlineInputBorder(
          borderRadius: radius,
          borderSide: BorderSide(color: scheme.primary, width: 1.8),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: radius,
          borderSide: BorderSide(color: scheme.error),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: radius,
          borderSide: BorderSide(color: scheme.error, width: 1.8),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: radius),
          disabledBackgroundColor: scheme.surfaceContainerHigh,
          disabledForegroundColor: scheme.onSurfaceVariant,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: radius),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(0, 48),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      iconButtonTheme: IconButtonThemeData(
        style: IconButton.styleFrom(
          minimumSize: const Size(48, 48),
          foregroundColor: scheme.onSurfaceVariant,
          shape: RoundedRectangleBorder(borderRadius: radius),
        ),
      ),
      dividerTheme: DividerThemeData(
        color: scheme.outlineVariant,
        thickness: 1,
        space: 1,
      ),
      dataTableTheme: DataTableThemeData(
        headingRowColor: WidgetStatePropertyAll(scheme.surfaceContainer),
        headingTextStyle: textTheme.labelLarge?.copyWith(
          color: scheme.onSurface,
        ),
        dataTextStyle: textTheme.bodyMedium,
        horizontalMargin: 20,
        columnSpacing: 28,
        headingRowHeight: 52,
        dataRowMinHeight: 56,
        dataRowMaxHeight: 64,
        dividerThickness: 1,
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FlexiCoreRadius.xLarge),
        ),
        titleTextStyle: textTheme.titleLarge,
        contentTextStyle: textTheme.bodyMedium,
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        modalBackgroundColor: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(
            top: Radius.circular(FlexiCoreRadius.xLarge),
          ),
        ),
      ),
      drawerTheme: DrawerThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.horizontal(
            right: Radius.circular(FlexiCoreRadius.large),
          ),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 72,
        backgroundColor: scheme.surfaceContainerLow,
        indicatorColor: scheme.primaryContainer,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => textTheme.labelSmall?.copyWith(
            color: states.contains(WidgetState.selected)
                ? scheme.primary
                : scheme.onSurfaceVariant,
            fontWeight: states.contains(WidgetState.selected)
                ? FontWeight.w700
                : FontWeight.w500,
          ),
        ),
      ),
      navigationRailTheme: NavigationRailThemeData(
        backgroundColor: scheme.surfaceContainerLow,
        indicatorColor: scheme.primaryContainer,
        selectedIconTheme: IconThemeData(color: scheme.primary),
        unselectedIconTheme: IconThemeData(color: scheme.onSurfaceVariant),
        selectedLabelTextStyle: textTheme.labelMedium?.copyWith(
          color: scheme.primary,
          fontWeight: FontWeight.w700,
        ),
        unselectedLabelTextStyle: textTheme.labelMedium?.copyWith(
          color: scheme.onSurfaceVariant,
        ),
      ),
      listTileTheme: ListTileThemeData(
        minVerticalPadding: 8,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FlexiCoreRadius.medium),
        ),
        selectedColor: scheme.primary,
        selectedTileColor: scheme.primaryContainer.withValues(alpha: 0.72),
      ),
      chipTheme: base.chipTheme.copyWith(
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FlexiCoreRadius.pill),
        ),
        side: BorderSide(color: scheme.outlineVariant),
        backgroundColor: scheme.surfaceContainerLow,
        selectedColor: scheme.primaryContainer,
        labelStyle: textTheme.labelMedium?.copyWith(color: scheme.onSurface),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: scheme.primary,
        linearTrackColor: scheme.primaryContainer,
        circularTrackColor: scheme.primaryContainer,
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: scheme.inverseSurface,
        contentTextStyle: textTheme.bodyMedium?.copyWith(
          color: scheme.onInverseSurface,
        ),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(FlexiCoreRadius.medium),
        ),
      ),
    );
  }
}
