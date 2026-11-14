// Redesign Phase 1. Additive only: nothing references this file yet.
using Avalonia.Media;

namespace FlexiCore.ActivationApp;

public static class FlexiTokens
{
    // ---------- Brand ramp (official primary = Teal800 #125352) ----------
    public static class Brand
    {
        public static readonly Color Teal950 = Color.Parse("#082C2C");
        public static readonly Color Teal900 = Color.Parse("#0B3D3C");
        public static readonly Color Teal800 = Color.Parse("#125352"); // PRIMARY
        public static readonly Color Teal700 = Color.Parse("#176A67");
        public static readonly Color Teal600 = Color.Parse("#1D817B");
        public static readonly Color Teal500 = Color.Parse("#269C93");
        public static readonly Color Teal400 = Color.Parse("#39B9AB");
        public static readonly Color Teal300 = Color.Parse("#65D0C1");
        public static readonly Color Teal200 = Color.Parse("#9BE4D8");
        public static readonly Color Teal100 = Color.Parse("#C9F1E9");
        public static readonly Color Teal50  = Color.Parse("#EAF8F5");
        public static readonly Color Teal25  = Color.Parse("#F5FBF9");
        public static readonly Color Primary = Teal800;
        public static readonly Color Aqua    = Color.Parse("#45D6C3");
        public static readonly Color Mint    = Color.Parse("#A5E9D9");
        public static readonly Color PaleMint= Color.Parse("#D6F4EB");
        public static readonly Color Gold    = Color.Parse("#E6B86A");
        public static readonly Color Slate   = Color.Parse("#6B83B9");
        public static readonly Color Violet  = Color.Parse("#B5A1D8");
    }

    // ---------- Theme palettes ----------
    public sealed record ThemePalette(
        Color Background, Color Surface, Color SurfaceAlt, Color Elevated,
        Color Border, Color Divider, Color Text, Color TextMuted,
        Color Primary, Color OnPrimary, Color Success, Color Warning,
        Color Error, Color Info, Color Disabled, Color Focus);

    public static readonly ThemePalette Light = new(
        Background: Color.Parse("#F3F8F7"), Surface: Color.Parse("#FFFFFF"),
        SurfaceAlt: Color.Parse("#EAF4F2"), Elevated: Color.Parse("#FFFFFF"),
        Border: Color.Parse("#DCE9E6"), Divider: Color.Parse("#E5EFED"),
        Text: Color.Parse("#142D2C"), TextMuted: Color.Parse("#566F6D"),
        Primary: Color.Parse("#125352"), OnPrimary: Color.Parse("#FFFFFF"),
        Success: Color.Parse("#1B7F5C"), Warning: Color.Parse("#8A5A00"),
        Error: Color.Parse("#B42318"), Info: Color.Parse("#1B6A9C"),
        Disabled: Color.Parse("#9DB2AF"), Focus: Color.Parse("#269C93"));

    public static readonly ThemePalette Dark = new(
        Background: Color.Parse("#071F20"), Surface: Color.Parse("#0D3030"),
        SurfaceAlt: Color.Parse("#123A39"), Elevated: Color.Parse("#164342"),
        Border: Color.Parse("#1F4A49"), Divider: Color.Parse("#1A4040"),
        Text: Color.Parse("#F2FAF8"), TextMuted: Color.Parse("#B7D4CD"),
        Primary: Color.Parse("#65D0C1"), OnPrimary: Color.Parse("#071F20"),
        Success: Color.Parse("#4ADE9A"), Warning: Color.Parse("#F2C36B"),
        Error: Color.Parse("#FF8A80"), Info: Color.Parse("#7CC4F0"),
        Disabled: Color.Parse("#5E7F7B"), Focus: Color.Parse("#65D0C1"));

    public static ThemePalette For(bool dark) => dark ? Dark : Light;

    // ---------- Typography (the ONLY font sizes the app may use) ----------
    public static class Type
    {
        public const string Family = "Inter, Segoe UI, Roboto, sans-serif";
        // Bundled Inter (Assets/Fonts). The URI uses the assembly this file is compiled into, so it works on desktop AND Android.
        public static FontFamily Font { get; } = new FontFamily("avares://" + typeof(FlexiTokens).Assembly.GetName().Name + "/Assets/Fonts#Inter");
        public const double PageTitle = 30,  PageTitleCompact = 26;
        public const double Section   = 21,  SectionCompact   = 19;
        public const double CardTitle = 16;
        public const double Body      = 14;
        public const double Support   = 13;
        public const double Caption   = 12;
        public const double Stat      = 34,  StatCompact      = 30;
        public const double Nav       = 13;
        public const double LineBody  = 20,  LineSupport = 18, LineTitle = 38, LineSection = 28;
        public static readonly FontWeight Regular  = FontWeight.Normal;
        public static readonly FontWeight Medium   = FontWeight.Medium;
        public static readonly FontWeight SemiBold = FontWeight.SemiBold;
    }

    // ---------- Spacing, radii, sizes ----------
    public static class Space { public const double S1 = 4, S2 = 8, S3 = 12, S4 = 16, S5 = 24, S6 = 32, S7 = 48; }
    public static class Radius { public const double Small = 6, Medium = 10, Large = 14; }
    public static class Size
    {
        public const double TouchTarget = 44;
        public const double SidebarWide = 248, SidebarCollapsed = 72;
        public const double ContentMaxWidth = 1180;
        public const double CompactBelow = 600, MediumBelow = 1024; // phone / tablet / desktop
    }

    // ---------- Motion ----------
    public static class Motion
    {
        public static readonly System.TimeSpan Fast       = System.TimeSpan.FromMilliseconds(150);
        public static readonly System.TimeSpan Standard   = System.TimeSpan.FromMilliseconds(220);
        public static readonly System.TimeSpan Navigation = System.TimeSpan.FromMilliseconds(260);
        public static readonly System.TimeSpan Large      = System.TimeSpan.FromMilliseconds(350);
    }

    // ---------- Shadows (Avalonia BoxShadow strings: x y blur spread color) ----------
    public static class Shadow
    {
        public const string Xs = "0 1 3 0 #0F125352";
        public const string Sm = "0 4 12 0 #14125352";
        public const string Md = "0 10 28 0 #1F125352";
        public const string Lg = "0 18 45 0 #29071F20";
    }
}
