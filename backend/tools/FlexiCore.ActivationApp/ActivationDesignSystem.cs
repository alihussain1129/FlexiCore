using Avalonia;
using Avalonia.Media;

namespace FlexiCore.ActivationApp;

internal static class ActivationDesignSystem
{
    public const string Primary950Hex = "#082C2C";
    public const string Primary900Hex = "#0B3D3C";
    public const string Primary800Hex = "#125352";
    public const string Primary700Hex = "#176A67";
    public const string Primary600Hex = "#1D817B";
    public const string Primary500Hex = "#269C93";
    public const string Primary400Hex = "#39B9AB";
    public const string Primary300Hex = "#65D0C1";
    public const string Primary200Hex = "#9BE4D8";
    public const string Primary100Hex = "#C9F1E9";
    public const string Primary50Hex = "#EAF8F5";
    public const string Primary25Hex = "#F5FBF9";

    public const string LuminousAquaHex = "#45D6C3";
    public const string SoftMintHex = "#A5E9D9";
    public const string PaleMintHex = "#D6F4EB";
    public const string PremiumGoldHex = "#E6B86A";
    public const string SlateBlueHex = "#6B83B9";
    public const string SoftVioletHex = "#B5A1D8";

    public const double Space2 = 4;
    public const double Space4 = 8;
    public const double Space8 = 16;
    public const double Space12 = 24;
    public const double Space16 = 32;
    public const double RadiusSmall = 10;
    public const double RadiusMedium = 14;
    public const double RadiusLarge = 20;
    public const double RadiusXLarge = 24;
    public const double CompactBreakpoint = 780;
    public static readonly TimeSpan MotionFast = TimeSpan.FromMilliseconds(160);
    public static readonly TimeSpan MotionStandard = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan MotionNavigation = TimeSpan.FromMilliseconds(260);

    public static bool IsDark { get; private set; }

    public static void ToggleTheme() => IsDark = !IsDark;

    public static IBrush Background => Brush.Parse(IsDark ? "#071F20" : "#F3F8F7");
    public static IBrush Surface => Brush.Parse(IsDark ? "#0D3030" : "#FFFFFF");
    public static IBrush SurfaceElevated => Brush.Parse(IsDark ? "#164342" : "#EAF4F2");
    public static IBrush Border => Brush.Parse(IsDark ? "#176A67" : "#DCE9E6");
    public static IBrush PrimaryText => Brush.Parse(IsDark ? "#F2FAF8" : "#142D2C");
    public static IBrush SecondaryText => Brush.Parse(IsDark ? "#B7D4CD" : "#566F6D");
    public static IBrush Primary => Brush.Parse(IsDark ? Primary300Hex : Primary800Hex);
    public static IBrush PrimaryStrong => Brush.Parse(IsDark ? Primary200Hex : Primary700Hex);
    public static IBrush PrimaryContainer => Brush.Parse(IsDark ? "#164342" : Primary100Hex);
    public static IBrush OnPrimaryContainer => Brush.Parse(IsDark ? Primary100Hex : Primary950Hex);
    public static IBrush OnPrimary => Brush.Parse(IsDark ? "#071F20" : "#FFFFFF");
    public static IBrush HeroText => Brush.Parse(Primary100Hex);
    public static IBrush HeroSubtleText => Brush.Parse(Primary50Hex);
    public static IBrush Success => Brush.Parse(IsDark ? "#4ADE9A" : "#16866A");
    public static IBrush Warning => Brush.Parse(IsDark ? "#E6B86A" : "#8A5A00");
    public static IBrush Error => Brush.Parse(IsDark ? "#F08A91" : "#B42318");
    public static IBrush Information => Brush.Parse(IsDark ? "#78BBD4" : "#277DA1");
    public static IBrush InverseText => Brush.Parse(IsDark ? Primary950Hex : "#FFFFFF");
    public static IBrush ActionSurface => Brush.Parse(IsDark ? "#0B3D3C" : "#EAF4F2");
    public static IBrush StatusErrorSurface => Brush.Parse(IsDark ? "#4A292C" : "#FCEAEC");
    public static IBrush StatusSuccessSurface => Brush.Parse(IsDark ? "#153D35" : "#E7F5EF");
    public static IBrush StatusInfoSurface => Brush.Parse(IsDark ? "#173A43" : "#E9F4F7");
    public static IBrush StatusWarningSurface => Brush.Parse(IsDark ? "#493B24" : "#FFF4D8");
    public static IBrush StatusErrorText => Brush.Parse(IsDark ? "#F08A91" : "#9F3039");
    public static IBrush StatusSuccessText => Brush.Parse(IsDark ? "#85D6B6" : "#12634F");

    public static IBrush PrimaryGradient => CreateGradient(Primary950Hex, Primary800Hex, Primary500Hex);
    public static IBrush AccentGradient => CreateGradient(Primary800Hex, Primary500Hex, LuminousAquaHex);
    public static IBrush LightGradient => CreateGradient(Primary50Hex, "#FFFFFF", PaleMintHex);
    public static IBrush DarkGradient => CreateGradient("#071F20", "#0D3030", "#164342");

    public static BoxShadows ShadowXs => new(BoxShadow.Parse(
        IsDark ? "0 1 3 0 #26071F20" : "0 1 3 0 #0F125352"));
    public static BoxShadows ShadowSmall => new(BoxShadow.Parse(
        IsDark ? "0 4 12 0 #26071F20" : "0 4 12 0 #14125352"));
    public static BoxShadows ShadowMedium => new(BoxShadow.Parse(
        IsDark ? "0 10 28 0 #2B071F20" : "0 10 28 0 #1F125352"));
    public static BoxShadows ShadowLarge => new(BoxShadow.Parse(
        IsDark ? "0 18 45 0 #33071F20" : "0 18 45 0 #29071F20"));
    public static BoxShadows ShadowGlow => new(BoxShadow.Parse("0 0 24 0 #3845D6C3"));
    public static BoxShadows CardShadow => ShadowSmall;

    private static IBrush CreateGradient(params string[] colors)
    {
        var stops = new GradientStops();
        for (var index = 0; index < colors.Length; index++)
        {
            stops.Add(new GradientStop(Color.Parse(colors[index]), index / (double)(colors.Length - 1)));
        }

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = stops
        };
    }
}
