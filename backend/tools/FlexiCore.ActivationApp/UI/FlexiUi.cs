using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Orientation = Avalonia.Layout.Orientation;
using Path = Avalonia.Controls.Shapes.Path;

namespace FlexiCore.ActivationApp;

internal static class FlexiUi
{
    public const string IconHome = "M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z";
    public const string IconPeople = "M16 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zM8 11a3 3 0 1 0 0-6 3 3 0 0 0 0 6zm0 2c-2.3 0-7 1.2-7 3.5V20h14v-3.5C15 14.2 10.3 13 8 13zm8 0c-.3 0-.6 0-1 .1 1.2.8 2 1.9 2 3.4V20h7v-3.5c0-2.3-4.7-3.5-8-3.5z";
    public const string IconBolt = "M12 2 3 13h7l-1 9 12-13h-7z";
    public const string IconKey = "M12.65 10C11.83 7.67 9.61 6 7 6c-3.31 0-6 2.69-6 6s2.69 6 6 6c2.61 0 4.83-1.67 5.65-4H17v4h4v-4h2v-4H12.65zM7 14c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2z";
    public const string IconDoc = "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z";
    public const string IconAdd = "M13 7h-2v4H7v2h4v4h2v-4h4v-2h-4V7zm-1-5C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8z";
    public const string IconGear = "M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z";
    public const string IconHelp = "M11 18h2v-2h-2v2zm1-16C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm0 18c-4.41 0-8-3.59-8-8s3.59-8 8-8 8 3.59 8 8-3.59 8-8 8zm0-14c-2.21 0-4 1.79-4 4h2c0-1.1.9-2 2-2s2 .9 2 2c0 2-3 1.75-3 5h2c0-2.25 3-2.5 3-5 0-2.21-1.79-4-4-4z";
    public const string IconSun = "M6.76 4.84l-1.8-1.79-1.41 1.41 1.79 1.79 1.42-1.41zM4 10.5H1v2h3v-2zm9-9.95h-2V3.5h2V.55zm7.45 3.91l-1.41-1.41-1.79 1.79 1.41 1.41 1.79-1.79zm-3.21 13.7l1.79 1.8 1.41-1.41-1.8-1.79-1.4 1.4zM20 10.5v2h3v-2h-3zm-8-5c-3.31 0-6 2.69-6 6s2.69 6 6 6 6-2.69 6-6-2.69-6-6-6zm-1 16.95h2V19.5h-2v2.95zm-7.45-3.91l1.41 1.41 1.79-1.8-1.41-1.41-1.79 1.8z";
    public const string IconMoon = "M9.37 5.51c-.18.64-.27 1.31-.27 1.99 0 4.08 3.32 7.4 7.4 7.4.68 0 1.35-.09 1.99-.27C17.45 17.19 14.93 19 12 19c-3.86 0-7-3.14-7-7 0-2.93 1.81-5.45 4.37-6.49zM12 3c-4.97 0-9 4.03-9 9s4.03 9 9 9 9-4.03 9-9c0-.46-.04-.92-.1-1.36-.98 1.37-2.58 2.26-4.4 2.26-2.98 0-5.4-2.42-5.4-5.4 0-1.81.89-3.42 2.26-4.4-.44-.06-.9-.1-1.36-.1z";
    public const string IconVerified = "M12 1 3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4zm-2 16-4-4 1.41-1.41L10 14.17l6.59-6.59L18 9l-8 8z";
    public const string IconShield = "M12 1 3 5v6c0 5.55 3.84 10.74 9 12 5.16-1.26 9-6.45 9-12V5l-9-4z";
    public const string IconCheck = "M9 16.17 4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z";

    public static string IconFor(string? title)
    {
        var t = (title ?? string.Empty).ToLowerInvariant();
        if (t.Contains("dashboard")) return IconHome;
        if (t.Contains("customer")) return IconPeople;
        if (t.Contains("activate")) return IconBolt;
        if (t.Contains("history") || t.Contains("ledger") || t.Contains("issuance")) return IconDoc;
        if (t.Contains("issue")) return IconAdd;
        if (t.Contains("licen")) return IconVerified;
        if (t.Contains("key")) return IconKey;
        if (t.Contains("setting")) return IconGear;
        if (t.Contains("help")) return IconHelp;
        return IconShield;
    }

    public static string IconForStat(string? label)
    {
        var t = (label ?? string.Empty).ToLowerInvariant();
        if (t.Contains("issued")) return IconDoc;
        if (t.Contains("active")) return IconCheck;
        if (t.Contains("key")) return IconKey;
        if (t.Contains("expir") || t.Contains("revok")) return IconShield;
        return IconVerified;
    }

    public static int AccentForStat(string? label)
    {
        var t = (label ?? string.Empty).ToLowerInvariant();
        if (t.Contains("issued")) return 1;
        if (t.Contains("active")) return 0;
        if (t.Contains("expir")) return 3;
        return 2;
    }

    public static Path Icon(string data, IBrush fill, double size)
    {
        Geometry geometry;
        try { geometry = Geometry.Parse(data); }
        catch { geometry = new EllipseGeometry(new Rect(4, 4, 16, 16)); }
        return new Path
        {
            Data = geometry,
            Fill = fill,
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    public static TextBlock Text(string? text, double size, FontWeight weight, IBrush foreground, bool wrap = true, double letterSpacing = 0) =>
        new()
        {
            Text = text ?? string.Empty,
            FontSize = size,
            FontWeight = weight,
            Foreground = foreground,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            LetterSpacing = letterSpacing
        };

    public static Color AccentColor(int index)
    {
        var dark = ActivationDesignSystem.IsDark;
        var palette = dark
            ? new[] { "#45D6C3", "#8FA6DA", "#C9B8E8", "#E6B86A" }
            : new[] { "#1D817B", "#4A66A3", "#7459A8", "#9A6A12" };
        return Color.Parse(palette[((index % 4) + 4) % 4]);
    }

    public static (IBrush Fill, IBrush Tint) Accent(int index)
    {
        var color = AccentColor(index);
        return (new SolidColorBrush(color), new SolidColorBrush(color, ActivationDesignSystem.IsDark ? 0.20 : 0.12));
    }

    public static Control BrandBlock()
    {
        var mark = FlexiLogo.Mark(40);
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(Text("FlexiCore", 17, FontWeight.Bold, ActivationDesignSystem.PrimaryText, false));
        titles.Children.Add(Text("Activation Engine", 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText, false));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(4, 4, 4, 18) };
        row.Children.Add(mark);
        row.Children.Add(titles);
        return row;
    }

    private static IBrush HeroGradient()
    {
        var stops = new GradientStops();
        stops.Add(new GradientStop(Color.Parse("#082C2C"), 0));
        stops.Add(new GradientStop(Color.Parse("#0F4A49"), 0.55));
        stops.Add(new GradientStop(Color.Parse("#176A67"), 1));
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops = stops
        };
    }

    public static Control Hero(bool compact, Control actions)
    {
        var chipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        chipRow.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = new SolidColorBrush(Color.Parse("#45D6C3")),
            VerticalAlignment = VerticalAlignment.Center
        });
        chipRow.Children.Add(Text("PRIVATE KEYS STAY ON THIS MACHINE", 11, FontWeight.Bold, Brushes.White, false, 0.8));
        var chip = new Border
        {
            Background = new SolidColorBrush(Colors.White, 0.16),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = chipRow
        };
        var title = Text("Activation, with confidence.", compact ? 24 : 30, FontWeight.Bold, Brushes.White);
        var subtitle = Text("Your signing keys, customer requests and issue ledger in one protected workspace.", 14,
            FontWeight.Normal, Brush.Parse("#EAF8F5"));
        subtitle.MaxWidth = 560;

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(chip);
        content.Children.Add(title);
        content.Children.Add(subtitle);
        content.Children.Add(actions);

        var decorA = new Ellipse
        {
            Width = 260,
            Height = 260,
            Fill = new SolidColorBrush(Colors.White, 0.06),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, -110, -90, 0),
            IsHitTestVisible = false
        };
        var decorB = new Ellipse
        {
            Width = 150,
            Height = 150,
            Fill = new SolidColorBrush(Colors.White, 0.05),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 70, -80),
            IsHitTestVisible = false
        };
        var shield = Icon(IconVerified, new SolidColorBrush(Colors.White, 0.13), compact ? 76 : 120);
        shield.HorizontalAlignment = HorizontalAlignment.Right;
        shield.VerticalAlignment = compact ? VerticalAlignment.Top : VerticalAlignment.Center;
        shield.Margin = new Thickness(0, compact ? 4 : 0, 4, 0);
        shield.IsHitTestVisible = false;

        var layers = new Grid();
        layers.Children.Add(decorA);
        layers.Children.Add(decorB);
        layers.Children.Add(shield);
        layers.Children.Add(content);

        return new Border
        {
            Background = HeroGradient(),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusXLarge),
            Padding = new Thickness(compact ? 20 : 28),
            BoxShadow = ActivationDesignSystem.ShadowMedium,
            BorderBrush = new SolidColorBrush(Color.Parse("#2E45D6C3")),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 6),
            ClipToBounds = true,
            Child = layers
        };
    }

    public static Control StatCard(string? label, string? value, string? caption, string iconData, int accentIndex, bool compact)
    {
        var (fill, tint) = Accent(accentIndex);
        var size = compact ? 38 : 48;
        var badge = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(compact ? 12 : 16),
            Background = tint,
            Child = Icon(iconData, fill, compact ? 20 : 24)
        };
        var texts = new StackPanel { Spacing = 2 };
        texts.Children.Add(Text(label, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText));
        texts.Children.Add(Text(value, compact ? 24 : 28, FontWeight.Bold, ActivationDesignSystem.PrimaryText));
        if (!string.IsNullOrWhiteSpace(caption))
            texts.Children.Add(Text(caption, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));

        Control body;
        if (compact)
        {
            badge.HorizontalAlignment = HorizontalAlignment.Left;
            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(badge);
            stack.Children.Add(texts);
            body = stack;
        }
        else
        {
            badge.Margin = new Thickness(0, 0, 14, 0);
            badge.VerticalAlignment = VerticalAlignment.Center;
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(texts, 1);
            texts.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(badge);
            grid.Children.Add(texts);
            body = grid;
        }

        return new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(compact ? 14 : 18),
            Margin = new Thickness(6),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = body
        };
    }

    public static Control ChartCard(string title, string? subtitle, Control body)
    {
        var header = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(Text(title, 16, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        if (!string.IsNullOrWhiteSpace(subtitle))
            header.Children.Add(Text(subtitle, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(body);
        return new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(18),
            Margin = new Thickness(6),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = stack
        };
    }

    public static Control BarChartCard(string title, string subtitle, IReadOnlyList<(string Label, int Count)> points, bool compact)
    {
        var max = 0;
        foreach (var point in points) if (point.Count > max) max = point.Count;
        Control body;
        if (max == 0)
        {
            var empty = Text("No licenses issued in this period yet.", 13, FontWeight.Normal, ActivationDesignSystem.SecondaryText);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            empty.VerticalAlignment = VerticalAlignment.Center;
            body = new Border { Height = 150, Child = empty };
        }
        else
        {
            var grid = new Grid
            {
                Height = 176,
                ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", points.Count))),
                RowDefinitions = new RowDefinitions("*,Auto")
            };
            for (var i = 0; i < points.Count; i++)
            {
                var (label, count) = points[i];
                var isLast = i == points.Count - 1;
                var barHeight = count == 0 ? 4.0 : Math.Max(8.0, 100.0 * count / max);
                IBrush fill = count == 0
                    ? ActivationDesignSystem.Border
                    : (IBrush)new SolidColorBrush(AccentColor(0), isLast ? 1.0 : 0.55);
                var column = new StackPanel
                {
                    Spacing = 4,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                var value = Text(count.ToString(), 12, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText, false);
                value.HorizontalAlignment = HorizontalAlignment.Center;
                column.Children.Add(value);
                column.Children.Add(new Border
                {
                    Width = compact ? 24 : 36,
                    Height = barHeight,
                    CornerRadius = new CornerRadius(8, 8, 4, 4),
                    Background = fill,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                Grid.SetColumn(column, i);
                grid.Children.Add(column);
                var month = Text(label, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText, false);
                month.HorizontalAlignment = HorizontalAlignment.Center;
                month.Margin = new Thickness(0, 8, 0, 0);
                Grid.SetColumn(month, i);
                Grid.SetRow(month, 1);
                grid.Children.Add(month);
            }
            body = grid;
        }
        return ChartCard(title, subtitle, body);
    }

    private static string ArcPath(double cx, double cy, double r, double startDeg, double sweepDeg)
    {
        if (sweepDeg >= 359.9)
            return FormattableString.Invariant($"M {cx},{cy - r} A {r},{r} 0 1 1 {cx},{cy + r} A {r},{r} 0 1 1 {cx},{cy - r}");
        var a1 = startDeg * Math.PI / 180.0;
        var a2 = (startDeg + sweepDeg) * Math.PI / 180.0;
        var x1 = cx + r * Math.Cos(a1);
        var y1 = cy + r * Math.Sin(a1);
        var x2 = cx + r * Math.Cos(a2);
        var y2 = cy + r * Math.Sin(a2);
        var large = sweepDeg > 180 ? 1 : 0;
        return FormattableString.Invariant($"M {x1:0.###},{y1:0.###} A {r},{r} 0 {large} 1 {x2:0.###},{y2:0.###}");
    }

    public static Control DonutCard(string title, int active, int expired, int pending, bool compact)
    {
        const double size = 150, center = 75, radius = 60, thickness = 16;
        var total = active + expired + pending;
        var canvas = new Canvas { Width = size, Height = size };
        canvas.Children.Add(new Path
        {
            Data = Geometry.Parse(ArcPath(center, center, radius, -90, 360)),
            Stroke = ActivationDesignSystem.Border,
            StrokeThickness = thickness
        });
        if (total > 0)
        {
            var parts = new[] { (Value: active, Accent: 0), (Value: pending, Accent: 1), (Value: expired, Accent: 3) };
            var visible = parts.Count(p => p.Value > 0);
            var gap = visible > 1 ? 4.0 : 0.0;
            var start = -90.0;
            foreach (var part in parts)
            {
                if (part.Value <= 0) continue;
                var span = 360.0 * part.Value / total;
                var sweep = Math.Max(1.0, span - gap);
                canvas.Children.Add(new Path
                {
                    Data = Geometry.Parse(ArcPath(center, center, radius, start + gap / 2, sweep)),
                    Stroke = new SolidColorBrush(AccentColor(part.Accent)),
                    StrokeThickness = thickness
                });
                start += span;
            }
        }
        var centerText = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var totalText = Text(total.ToString(), 28, FontWeight.Bold, ActivationDesignSystem.PrimaryText, false);
        totalText.HorizontalAlignment = HorizontalAlignment.Center;
        var totalCaption = Text(total == 1 ? "license" : "licenses", 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText, false);
        totalCaption.HorizontalAlignment = HorizontalAlignment.Center;
        centerText.Children.Add(totalText);
        centerText.Children.Add(totalCaption);
        var ring = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };
        ring.Children.Add(canvas);
        ring.Children.Add(centerText);

        var legend = new StackPanel { Spacing = 10, Margin = new Thickness(0, 16, 0, 0) };
        legend.Children.Add(LegendRow("Active", active, 0));
        legend.Children.Add(LegendRow("Not yet valid", pending, 1));
        legend.Children.Add(LegendRow("Expired", expired, 3));
        var body = new StackPanel();
        body.Children.Add(ring);
        body.Children.Add(legend);
        return ChartCard(title, "By recorded dates", body);
    }

    private static Control LegendRow(string label, int value, int accent)
    {
        var dot = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(AccentColor(accent)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };
        var name = Text(label, 13, FontWeight.Medium, ActivationDesignSystem.SecondaryText, false);
        name.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(name, 1);
        var number = Text(value.ToString(), 14, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText, false);
        number.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(number, 2);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        row.Children.Add(dot);
        row.Children.Add(name);
        row.Children.Add(number);
        return row;
    }

    public static Control ListCard(Control content) =>
        new Border
        {
            Background = ActivationDesignSystem.Surface,
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(ActivationDesignSystem.RadiusLarge),
            Padding = new Thickness(16, 4),
            Margin = new Thickness(0, 0, 0, 12),
            BoxShadow = ActivationDesignSystem.CardShadow,
            Child = content
        };

    public static Control ActivityRow(string? licensee, string? detail, string? when, int index, bool last, bool compact)
    {
        var (fill, tint) = Accent(index);
        var name = licensee ?? string.Empty;
        var initial = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, 1).ToUpperInvariant();
        var avatar = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            Background = tint,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 14, 0),
            Child = new TextBlock
            {
                Text = initial,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                Foreground = fill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        var details = new StackPanel { Spacing = 2 };
        details.Children.Add(Text(name, 15, FontWeight.SemiBold, ActivationDesignSystem.PrimaryText));
        details.Children.Add(Text(detail, 12, FontWeight.Normal, ActivationDesignSystem.SecondaryText));
        if (compact) details.Children.Add(Text(when, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText));

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(compact ? "Auto,*" : "Auto,*,Auto") };
        Grid.SetColumn(details, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(details);
        if (!compact)
        {
            var stamp = Text(when, 12, FontWeight.Medium, ActivationDesignSystem.SecondaryText, false);
            stamp.VerticalAlignment = VerticalAlignment.Top;
            stamp.Margin = new Thickness(12, 2, 0, 0);
            Grid.SetColumn(stamp, 2);
            grid.Children.Add(stamp);
        }

        return new Border
        {
            BorderBrush = ActivationDesignSystem.Border,
            BorderThickness = last ? new Thickness(0) : new Thickness(0, 0, 0, 1),
            Padding = new Thickness(0, 12),
            Child = grid
        };
    }
}
