import 'package:flutter/material.dart';

/// FlexiCore palette: deep teal for trust, warm clay for accents, paper
/// background so white cards lift off the page.
class AppColors {
  AppColors._();

  static const ink = Color(0xFF16302F);
  static const tealDark = Color(0xFF0A4442);
  static const teal = Color(0xFF0F5C5A);
  static const tealSoft = Color(0xFFDDEDEB);
  static const clay = Color(0xFFC8743A);
  static const claySoft = Color(0xFFF6E4D6);
  static const paper = Color(0xFFF3EFE7);
  static const line = Color(0xFFE4DED2);
  static const muted = Color(0xFF5E706F);
  static const danger = Color(0xFFB3382C);
}

ThemeData buildAppTheme() {
  final scheme = ColorScheme.fromSeed(seedColor: AppColors.teal).copyWith(
    primary: AppColors.teal,
    onPrimary: Colors.white,
    secondary: AppColors.clay,
    error: AppColors.danger,
    surface: Colors.white,
    onSurface: AppColors.ink,
  );
  final base = ThemeData(useMaterial3: true, colorScheme: scheme);
  final r12 = BorderRadius.circular(12);

  OutlineInputBorder border(Color color, [double width = 1]) =>
      OutlineInputBorder(
        borderRadius: r12,
        borderSide: BorderSide(color: color, width: width),
      );

  return base.copyWith(
    scaffoldBackgroundColor: AppColors.paper,
    textTheme: base.textTheme.apply(
      bodyColor: AppColors.ink,
      displayColor: AppColors.ink,
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(
      color: AppColors.teal,
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.teal,
        foregroundColor: Colors.white,
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
        shape: RoundedRectangleBorder(borderRadius: r12),
        textStyle: const TextStyle(fontWeight: FontWeight.w600),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: AppColors.teal,
        shape: RoundedRectangleBorder(borderRadius: r12),
        textStyle: const TextStyle(fontWeight: FontWeight.w600),
      ),
    ),
    inputDecorationTheme: InputDecorationThemeData(
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
      labelStyle: const TextStyle(color: AppColors.muted),
      helperStyle: const TextStyle(color: AppColors.muted),
      border: border(AppColors.line),
      enabledBorder: border(AppColors.line),
      focusedBorder: border(AppColors.teal, 1.8),
      errorBorder: border(AppColors.danger),
      focusedErrorBorder: border(AppColors.danger, 1.8),
    ),
    dialogTheme: DialogThemeData(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      elevation: 12,
      shadowColor: AppColors.ink.withValues(alpha: 0.3),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      titleTextStyle: const TextStyle(
        fontSize: 20,
        fontWeight: FontWeight.w700,
        color: AppColors.ink,
      ),
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: AppColors.ink,
      contentTextStyle: const TextStyle(color: Colors.white),
      shape: RoundedRectangleBorder(borderRadius: r12),
    ),
    popupMenuTheme: PopupMenuThemeData(
      color: Colors.white,
      surfaceTintColor: Colors.transparent,
      elevation: 12,
      shadowColor: AppColors.ink.withValues(alpha: 0.3),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
    ),
  );
}
